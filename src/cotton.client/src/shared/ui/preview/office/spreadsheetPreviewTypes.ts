import type { SpreadsheetRow } from "./spreadsheetRow";

export interface SpreadsheetRequest {
  bytes?: ArrayBuffer;
  sheetIndex: number;
}

export interface SpreadsheetPage {
  sheetIndex: number;
  sheetNames: string[];
  columns: string[];
  rows: SpreadsheetRow[];
  truncated: boolean;
}

export type SpreadsheetResponse = { page: SpreadsheetPage } | { failed: true };
