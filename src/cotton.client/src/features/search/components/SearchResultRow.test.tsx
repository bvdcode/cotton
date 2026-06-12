import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { SearchResultRow } from "./SearchResultRow";
import type { SearchRow } from "../types";

type FileSearchRow = Extract<SearchRow, { kind: "file" }>;

const makeFileRow = (overrides: Partial<FileSearchRow["file"]> = {}): FileSearchRow => ({
  id: "file-file-1",
  kind: "file",
  path: "/Docs/report.pdf",
  file: {
    id: "file-1",
    nodeId: "node-1",
    ownerId: "owner-1",
    name: "report.pdf",
    contentType: "application/pdf",
    sizeBytes: 1024,
    metadata: {},
    createdAt: "2026-01-01T00:00:00.000Z",
    updatedAt: "2026-01-01T00:00:00.000Z",
    ...overrides,
  },
});

describe("SearchResultRow", () => {
  it("shows a direct download action for inline-viewable files", () => {
    const row = makeFileRow();
    const onDownloadFile = vi.fn();

    render(
      <SearchResultRow
        row={row}
        isLast={false}
        previewFailed={false}
        onPreviewError={vi.fn()}
        onActivate={vi.fn()}
        onShareFile={vi.fn()}
        onOpenFileFolder={vi.fn()}
        onDownloadFile={onDownloadFile}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "actions.downloadFile" }));

    expect(onDownloadFile).toHaveBeenCalledWith(row);
  });
});
