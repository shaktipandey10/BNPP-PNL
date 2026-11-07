import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { EMPTY, Observable, expand, reduce } from 'rxjs';
import { FeedType, ImportResult, PnlRecord, PnlSummary } from './pnl.models';

@Injectable({ providedIn: 'root' })
export class PnlApiService {
  readonly baseUrl = 'http://localhost:5020';

  constructor(private readonly http: HttpClient) {}

  getValid(
    sourceSystem?: string,
    feedType?: FeedType,
    page = 1,
    pageSize = 100
  ): Observable<PnlRecord[]> {
    return this.http.get<PnlRecord[]>(`${this.baseUrl}/api/reports/valid`, {
      params: this.createParams(sourceSystem, feedType, page, pageSize)
    });
  }

  getInvalid(
    sourceSystem?: string,
    feedType?: FeedType,
    page = 1,
    pageSize = 100
  ): Observable<PnlRecord[]> {
    return this.http.get<PnlRecord[]>(`${this.baseUrl}/api/reports/invalid`, {
      params: this.createParams(sourceSystem, feedType, page, pageSize)
    });
  }

  getAllValid(sourceSystem?: string, feedType?: FeedType): Observable<PnlRecord[]> {
    return this.getAllPages((page, pageSize) =>
      this.getValid(sourceSystem, feedType, page, pageSize)
    );
  }

  getAllInvalid(sourceSystem?: string, feedType?: FeedType): Observable<PnlRecord[]> {
    return this.getAllPages((page, pageSize) =>
      this.getInvalid(sourceSystem, feedType, page, pageSize)
    );
  }

  getSummary(sourceSystem?: string, feedType?: FeedType): Observable<PnlSummary> {
    return this.http.get<PnlSummary>(`${this.baseUrl}/api/reports/summary`, {
      params: this.createParams(sourceSystem, feedType)
    });
  }

  upload(file: File): Observable<ImportResult> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<ImportResult>(`${this.baseUrl}/api/imports`, body);
  }

  private getAllPages(
    fetchPage: (page: number, pageSize: number) => Observable<PnlRecord[]>
  ): Observable<PnlRecord[]> {
    const pageSize = 500;
    return fetchPage(1, pageSize).pipe(
      expand((records, pageIndex) =>
        records.length < pageSize ? EMPTY : fetchPage(pageIndex + 2, pageSize)
      ),
      reduce((allRecords, records) => allRecords.concat(records), [] as PnlRecord[])
    );
  }

  private createParams(
    sourceSystem?: string,
    feedType?: FeedType,
    page?: number,
    pageSize?: number
  ): HttpParams {
    let params = new HttpParams();
    if (sourceSystem) {
      params = params.set('sourceSystem', sourceSystem);
    }
    if (feedType) {
      params = params.set('feedType', feedType);
    }
    if (page !== undefined) {
      params = params.set('page', page);
    }
    if (pageSize !== undefined) {
      params = params.set('pageSize', pageSize);
    }
    return params;
  }
}
