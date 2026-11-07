import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, signal, WritableSignal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { forkJoin } from 'rxjs';
import { PnlApiService } from './core/pnl-api.service';
import { FeedType, PnlRecord, PnlSummary } from './core/pnl.models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit, OnDestroy {
  readonly validRecords = signal<PnlRecord[]>([]);
  readonly invalidRecords = signal<PnlRecord[]>([]);
  readonly summary = signal<PnlSummary>({ totalCount: 0, validCount: 0, invalidCount: 0 });
  readonly sourceFilter = signal('');
  readonly feedFilter = signal('');
  readonly uploadMessage = signal('');
  readonly connectionState = signal('Connecting');
  readonly isUploading = signal(false);

  private readonly hubConnection: HubConnection;
  private destroyed = false;
  private retryTimer: number | undefined;
  private refreshRequestId = 0;
  private realtimeRevision = 0;

  constructor(private readonly api: PnlApiService) {
    this.hubConnection = new HubConnectionBuilder()
      .withUrl(`${this.api.baseUrl}/hubs/pnl`)
      .withAutomaticReconnect()
      .build();
  }

  ngOnInit(): void {
    this.refresh();
    this.hubConnection.on('PnlRecordProcessed', (record: PnlRecord) => {
      const sourceSystem = this.sourceFilter().trim();
      const feedType = this.feedFilter();
      if ((sourceSystem && record.sourceSystem !== sourceSystem) || (feedType && record.feedType !== feedType)) {
        return;
      }

      const alreadyLoaded =
        this.validRecords().some((item) => item.id === record.id) ||
        this.invalidRecords().some((item) => item.id === record.id);
      if (record.isValid) {
        this.upsert(this.validRecords, true, record);
      } else {
        this.upsert(this.invalidRecords, true, record);
      }
      this.realtimeRevision++;
      if (!alreadyLoaded) {
        this.summary.update((counts) => ({
          totalCount: counts.totalCount + 1,
          validCount: counts.validCount + (record.isValid ? 1 : 0),
          invalidCount: counts.invalidCount + (record.isValid ? 0 : 1)
        }));
      }
    });

    this.hubConnection.onreconnecting(() => this.connectionState.set('Reconnecting'));
    this.hubConnection.onreconnected(() => {
      this.connectionState.set('Live');
      this.refresh();
    });
    this.hubConnection.onclose(() => {
      this.connectionState.set('Disconnected');
      this.scheduleReconnect();
    });

    void this.startConnection();
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    if (this.retryTimer !== undefined) {
      window.clearTimeout(this.retryTimer);
    }
    void this.hubConnection.stop();
  }

  refresh(): void {
    const requestId = ++this.refreshRequestId;
    const revisionAtRequest = this.realtimeRevision;
    const sourceSystem = this.sourceFilter().trim() || undefined;
    const feedType = this.feedFilter() as FeedType | undefined;

    forkJoin({
      valid: this.api.getAllValid(sourceSystem, feedType),
      invalid: this.api.getAllInvalid(sourceSystem, feedType),
      summary: this.api.getSummary(sourceSystem, feedType)
    }).subscribe({
      next: (data) => {
        if (requestId !== this.refreshRequestId) {
          return;
        }
        if (revisionAtRequest !== this.realtimeRevision) {
          this.refresh();
          return;
        }
        this.validRecords.set(data.valid);
        this.invalidRecords.set(data.invalid);
        this.summary.set(data.summary);
      },
      error: () => this.uploadMessage.set('Unable to load reports. Check that the API is running.')
    });
  }

  uploadFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    this.isUploading.set(true);
    this.uploadMessage.set('');
    this.api.upload(file).subscribe({
      next: (result) => {
        this.isUploading.set(false);
        this.uploadMessage.set(
          `Imported ${result.totalRecords} records: ${result.validRecords} valid, ${result.invalidRecords} invalid.`
        );
        this.refresh();
        input.value = '';
      },
      error: (error: { error?: { errors?: Array<{ rowNumber: number; message: string }> } }) => {
        this.isUploading.set(false);
        const errors = error.error?.errors ?? [];
        this.uploadMessage.set(
          errors.length
            ? errors.map((item) => `${item.rowNumber ? `Row ${item.rowNumber}: ` : ''}${item.message}`).join(' ')
            : 'The upload failed. Check the file format and API availability.'
        );
      }
    });
  }

  private async startConnection(): Promise<void> {
    if (this.destroyed || this.hubConnection.state !== HubConnectionState.Disconnected) {
      return;
    }
    try {
      await this.hubConnection.start();
      this.connectionState.set('Live');
      this.refresh();
    } catch {
      this.connectionState.set('Disconnected');
      this.scheduleReconnect();
    }
  }

  private scheduleReconnect(): void {
    if (this.destroyed || this.retryTimer !== undefined) {
      return;
    }
    this.retryTimer = window.setTimeout(() => {
      this.retryTimer = undefined;
      void this.startConnection();
    }, 5000);
  }

  private upsert(target: WritableSignal<PnlRecord[]>, shouldInclude: boolean, record: PnlRecord): void {
    const existing = target().filter((item) => item.id !== record.id);
    if (shouldInclude) {
      target.set([record, ...existing]);
    } else {
      target.set(existing);
    }
  }
}
