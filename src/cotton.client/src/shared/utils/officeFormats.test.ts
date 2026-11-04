import { describe, expect, it } from "vitest";
import { getFileTypeInfo } from "./fileTypes";
import { resolveOfficeFormat } from "./officeFormats";

describe("Office format detection", () => {
  it.each(["DOCX", "xlsx", "xls", "pptx"])("opens %s through the Office viewer", (extension) => {
    expect(getFileTypeInfo("Report." + extension, "application/octet-stream")).toEqual({
      type: "office", supportsPreview: true, supportsInlineView: true,
    });
  });
  it("recognizes spreadsheet MIME types without an extension", () => {
    expect(resolveOfficeFormat("report", "application/vnd.ms-excel")).toBe("spreadsheet");
  });
  it.each(["doc", "ppt", "rtf", "odt", "docm"])("does not advertise unsupported %s rendering", (extension) => {
    expect(resolveOfficeFormat("report." + extension)).toBeNull();
  });
});
