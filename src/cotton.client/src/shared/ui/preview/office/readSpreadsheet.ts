import { read, utils, type CellObject } from "xlsx";
import type { SpreadsheetPage } from "./spreadsheetPreviewTypes";

const MAX_ROWS = 5_000;
const MAX_COLUMNS = 200;
const MAX_CELLS = 100_000;

export function readSpreadsheet(
  bytes: ArrayBuffer,
  sheetIndex: number,
): SpreadsheetPage {
  const names = read(bytes, { bookSheets: true }).SheetNames;
  const name = names[sheetIndex];
  if (!name) {
    throw new Error("Spreadsheet has no selected sheet.");
  }
  const workbook = read(bytes, {
    sheets: name,
    sheetRows: MAX_ROWS + 1,
    cellHTML: false,
    cellStyles: false,
    sheetStubs: true,
  });
  const sheet = workbook.Sheets[name];
  const range = utils.decode_range(sheet["!ref"] ?? "A1");
  const fullRange = utils.decode_range(
    sheet["!fullref"] ?? sheet["!ref"] ?? "A1",
  );
  const columnCount = Math.min(range.e.c + 1, MAX_COLUMNS);
  const rowCount = Math.min(
    range.e.r + 1,
    MAX_ROWS,
    Math.floor(MAX_CELLS / columnCount),
  );
  const columns = Array.from({ length: columnCount }, (_, column) =>
    utils.encode_col(column),
  );
  const rows = Array.from({ length: rowCount }, (_, row) => ({
    id: row + 1,
    cells: columns.map((column) => {
      const cell: CellObject | undefined = sheet[column + (row + 1)];
      if (!cell) {
        return "";
      }
      if (cell.f && (cell.t === "z" || cell.v === undefined)) {
        return "=" + cell.f;
      }
      return utils.format_cell(cell);
    }),
  }));
  return {
    sheetIndex,
    sheetNames: names,
    columns,
    rows,
    truncated: fullRange.e.r + 1 > rowCount || fullRange.e.c + 1 > columnCount,
  };
}
