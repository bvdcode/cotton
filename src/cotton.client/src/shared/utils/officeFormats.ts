export type OfficeFormat = "docx" | "spreadsheet" | "pptx";

const formats: ReadonlyArray<{
  extension: string;
  contentType: string;
  format: OfficeFormat;
}> = [
  {
    extension: "docx",
    contentType:
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    format: "docx",
  },
  {
    extension: "xlsx",
    contentType:
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    format: "spreadsheet",
  },
  {
    extension: "xls",
    contentType: "application/vnd.ms-excel",
    format: "spreadsheet",
  },
  {
    extension: "pptx",
    contentType:
      "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    format: "pptx",
  },
];

export function resolveOfficeFormat(
  fileName: string,
  contentType?: string | null,
): OfficeFormat | null {
  const extension = fileName.toLowerCase().split(".").pop();
  const mime = contentType?.split(";")[0]?.trim().toLowerCase();
  return (
    formats.find(
      (item) => item.extension === extension || item.contentType === mime,
    )?.format ?? null
  );
}
