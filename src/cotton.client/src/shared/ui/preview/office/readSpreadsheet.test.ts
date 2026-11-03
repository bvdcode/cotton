import { describe, expect, it } from "vitest";
import { utils, write } from "xlsx";
import { readSpreadsheet } from "./readSpreadsheet";

function createWorkbook(bookType: "xlsx" | "biff8"): ArrayBuffer {
  const book = utils.book_new();
  const sheet = utils.aoa_to_sheet([["Region", "Revenue", "Growth"], ["North", 84200, 0.12]]);
  sheet.B2.z = "$#,##0";
  sheet.C2.z = "0%";
  sheet.A3 = { t: "n", v: 84200, f: "SUM(B2:B2)" };
  sheet["!ref"] = "A1:C3";
  utils.book_append_sheet(book, sheet, "Sales");
  utils.book_append_sheet(book, utils.aoa_to_sheet([["Заметки"]]), "Notes");
  const bytes: ArrayBuffer = write(book, { type: "array", bookType });
  return bytes;
}

describe("readSpreadsheet", () => {
  it.each(["xlsx", "biff8"] as const)("reads %s sheets and saved formatted values", (format) => {
    const bytes = createWorkbook(format);
    const page = readSpreadsheet(bytes, 0);
    expect(page.sheetNames).toEqual(["Sales", "Notes"]);
    expect(page.rows[1].cells).toEqual(["North", "$84,200", "12%"]);
    expect(page.rows[2].cells[0]).toBe("84200");
    expect(page.truncated).toBe(false);
    expect(readSpreadsheet(bytes, 1).rows[0].cells).toEqual(["Заметки"]);
  });

  it("bounds sparse worksheet ranges and reports omitted columns", () => {
    const book = utils.book_new();
    const sheet = utils.aoa_to_sheet([["Start"]]);
    sheet.GS800 = { t: "s", v: "End" };
    sheet["!ref"] = "A1:GS800";
    utils.book_append_sheet(book, sheet, "Large");
    const bytes: ArrayBuffer = write(book, { type: "array", bookType: "xlsx" });
    const page = readSpreadsheet(bytes, 0);
    expect(page.truncated).toBe(true);
    expect(page.columns.length).toBeLessThanOrEqual(200);
    expect(page.rows.length * page.columns.length).toBeLessThanOrEqual(100000);
    expect(page.rows[0].cells[0]).toBe("Start");
  });

  it("shows a formula when the workbook does not contain its saved result", () => {
    const book = utils.book_new();
    const sheet = utils.aoa_to_sheet([[1], [2]]);
    sheet.A3 = { t: "n", f: "SUM(A1:A2)" };
    sheet["!ref"] = "A1:A3";
    utils.book_append_sheet(book, sheet, "Formula");
    const bytes: ArrayBuffer = write(book, { type: "array", bookType: "xlsx" });
    expect(readSpreadsheet(bytes, 0).rows[2].cells[0]).toBe("=SUM(A1:A2)");
  });
});
