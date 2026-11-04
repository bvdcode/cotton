import { readSpreadsheet } from "./readSpreadsheet";
import type {
  SpreadsheetRequest,
  SpreadsheetResponse,
} from "./spreadsheetPreviewTypes";

let documentBytes: ArrayBuffer | undefined;

self.onmessage = (event: MessageEvent<SpreadsheetRequest>) => {
  documentBytes = event.data.bytes ?? documentBytes;
  if (!documentBytes) {
    return;
  }
  let response: SpreadsheetResponse;
  try {
    response = { page: readSpreadsheet(documentBytes, event.data.sheetIndex) };
  } catch {
    response = { failed: true };
  }
  self.postMessage(response);
};
