export type FeedType = 'File' | 'Messaging';

export interface PnlRecord {
  id: number;
  sourceSystem: string;
  accountNumber: number;
  pnlAmount: number;
  isValid: boolean;
  validationReason: string | null;
  feedType: FeedType;
  processedAtUtc: string;
  messageId: string | null;
}

export interface PnlSummary {
  totalCount: number;
  validCount: number;
  invalidCount: number;
}

export interface ImportResult extends PnlSummary {
  succeeded: boolean;
  errors: Array<{ rowNumber: number; message: string }>;
  totalRecords: number;
  validRecords: number;
  invalidRecords: number;
}
