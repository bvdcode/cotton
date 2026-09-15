import { fireEvent, render, screen } from "@testing-library/react";
import type { GridRenderCellParams } from "@mui/x-data-grid";
import { describe, expect, it, vi } from "vitest";
import {
  ENCRYPTED_FLAG_KEY,
  FOLDER_ENCRYPTION_POLICY_KEY,
} from "@shared/crypto";
import { createActionsColumn, type FileListRow } from "./fileListColumns";
import type { ColumnOptions } from "./fileListColumnTypes";

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
  pin: "Pin",
  unpin: "Unpin",
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

function renderActions(
  row: FileListRow,
  overrides: Partial<ColumnOptions> = {},
): void {
  const column = createActionsColumn({
    labels,
    readOnly: false,
    fileOperations,
    folderOperations,
    ...overrides,
  });

  render(
    column.renderCell?.({
      row,
    } as GridRenderCellParams<FileListRow>),
  );
}

describe("file list action column", () => {
  it("reserves enough width for the full row action set", () => {
    const column = createActionsColumn({
      labels,
      readOnly: false,
      fileOperations,
      folderOperations,
    });

    expect(column.minWidth).toBeGreaterThanOrEqual(220);
  });

  it("shows share action for plain files", () => {
    renderActions(fileRow());

    expect(screen.getByTitle("Share")).toBeInTheDocument();
  });

  it("hides share action for encrypted files", () => {
    renderActions(fileRow({ [ENCRYPTED_FLAG_KEY]: "true" }));

    expect(screen.queryByTitle("Share")).not.toBeInTheDocument();
  });
});

const createFolderOperations = () => ({
  ...folderOperations,
  onDownload: vi.fn(),
  onTogglePin: vi.fn(),
  isPinned: vi.fn(() => true),
  onStartRename: vi.fn(),
  onShare: vi.fn(),
  onCut: vi.fn(),
  onToggleEncryptionPolicy: vi.fn(),
  onRestore: vi.fn(),
  onDelete: vi.fn(),
});

const folderRow: FileListRow = {
  id: "folder-1",
  type: "folder",
  name: "Reports",
  sizeBytes: null,
};

describe("folder list actions", () => {
  it("preserves action order and passes the folder to each operation", () => {
    const operations = createFolderOperations();
    renderActions(folderRow, { folderOperations: operations });

    const titles = [
      "Download",
      "Unpin",
      "Rename",
      "Share",
      "Cut",
      "Enable E2E",
      "Restore",
      "Delete",
    ];
    expect(screen.getAllByRole("button").map((button) => button.title)).toEqual(
      titles,
    );
    for (const title of titles) {
      fireEvent.click(screen.getByTitle(title));
    }

    expect(operations.onDownload).toHaveBeenCalledWith("folder-1", "Reports");
    expect(operations.onTogglePin).toHaveBeenCalledWith("folder-1");
    expect(operations.onStartRename).toHaveBeenCalledWith(
      "folder-1",
      "Reports",
    );
    expect(operations.onShare).toHaveBeenCalledWith("folder-1", "Reports");
    expect(operations.onCut).toHaveBeenCalledWith("folder-1");
    expect(operations.onToggleEncryptionPolicy).toHaveBeenCalledWith(
      "folder-1",
      false,
    );
    expect(operations.onRestore).toHaveBeenCalledWith("folder-1", "Reports");
    expect(operations.onDelete).toHaveBeenCalledWith("folder-1", "Reports");
  });

  it("only offers download in read-only mode", () => {
    renderActions(folderRow, {
      readOnly: true,
      folderOperations: createFolderOperations(),
    });

    expect(screen.getAllByRole("button").map((button) => button.title)).toEqual(
      ["Download"],
    );
  });

  it("does not offer actions without callbacks", () => {
    renderActions(folderRow);
    expect(screen.queryAllByRole("button")).toHaveLength(0);
  });

  it("hides the encryption toggle for inherited encryption", () => {
    renderActions(
      {
        ...folderRow,
        encryptionPolicy: {
          explicitEnabled: false,
          inheritedEnabled: true,
          effectiveEnabled: true,
        },
      },
      { folderOperations: createFolderOperations() },
    );

    expect(screen.queryByTitle("Enable E2E")).not.toBeInTheDocument();
    expect(screen.queryByTitle("Disable E2E")).not.toBeInTheDocument();
  });

  it.each([false, true])(
    "uses explicit encryption state %s over metadata",
    (enabled) => {
      const operations = createFolderOperations();
      renderActions(
        {
          ...folderRow,
          metadata: { [FOLDER_ENCRYPTION_POLICY_KEY]: String(!enabled) },
          encryptionPolicy: {
            explicitEnabled: enabled,
            inheritedEnabled: false,
            effectiveEnabled: enabled,
          },
        },
        { folderOperations: operations },
      );

      fireEvent.click(
        screen.getByTitle(enabled ? "Disable E2E" : "Enable E2E"),
      );
      expect(operations.onToggleEncryptionPolicy).toHaveBeenCalledWith(
        "folder-1",
        enabled,
      );
    },
  );

  it("uses folder metadata when resolved encryption state is unavailable", () => {
    const operations = createFolderOperations();
    renderActions(
      {
        ...folderRow,
        metadata: { [FOLDER_ENCRYPTION_POLICY_KEY]: "true" },
      },
      { folderOperations: operations },
    );

    fireEvent.click(screen.getByTitle("Disable E2E"));
    expect(operations.onToggleEncryptionPolicy).toHaveBeenCalledWith(
      "folder-1",
      true,
    );
  });
});
