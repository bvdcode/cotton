import { render, screen } from "@testing-library/react";
import type { GridRenderCellParams } from "@mui/x-data-grid";
import { describe, expect, it, vi } from "vitest";
import { ENCRYPTED_FLAG_KEY } from "@shared/crypto";
import {
  createActionsColumn,
  type FileListRow,
} from "./fileListColumns";

const labels = {
  name: "Name",
  size: "Size",
  location: "Location",
  actionsTitle: "Actions",
  placeholder: "-",
  goToFolder: "Go to folder",
  rename: "Rename",
  delete: "Delete",
  restore: "Restore",
  download: "Download",
  versions: "Versions",
  share: "Share",
  cut: "Cut",
  encryptedFile: "Encrypted file",
  encryptedFolder: "Encrypted folder",
  enableEncryptionPolicy: "Enable E2E",
  disableEncryptionPolicy: "Disable E2E",
};

const fileOperations = {
  isRenaming: () => false,
  getRenamingName: () => "",
  onRenamingNameChange: vi.fn(),
  onDownload: vi.fn(),
  onShare: vi.fn(),
  onStartRename: vi.fn(),
};

const folderOperations = {
  isRenaming: () => false,
  getRenamingName: () => "",
  onRenamingNameChange: vi.fn(),
};

const fileRow = (metadata: Record<string, string> = {}): FileListRow => ({
  id: "file-1",
  type: "file",
  name: "report.pdf",
  sizeBytes: 128,
  contentType: "application/pdf",
  metadata,
});

function renderActions(row: FileListRow): void {
  const column = createActionsColumn({
    labels,
    readOnly: false,
    fileOperations,
    folderOperations,
  });

  render(
    column.renderCell?.({
      row,
    } as GridRenderCellParams<FileListRow>),
  );
}

describe("file list action column", () => {
  it("shows share action for plain files", () => {
    renderActions(fileRow());

    expect(screen.getByTitle("Share")).toBeInTheDocument();
  });

  it("hides share action for encrypted files", () => {
    renderActions(fileRow({ [ENCRYPTED_FLAG_KEY]: "true" }));

    expect(screen.queryByTitle("Share")).not.toBeInTheDocument();
  });
});
