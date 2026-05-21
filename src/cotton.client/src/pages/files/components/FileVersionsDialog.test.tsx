import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { FileVersionsDialog } from "./FileVersionsDialog";
import type { FileVersionDto } from "../../../shared/api/filesApi";

const mocks = vi.hoisted(() => ({
  confirm: vi.fn(),
  deleteVersion: vi.fn(),
  getDownloadLink: vi.fn(),
  getVersionDownloadLink: vi.fn(),
  listVersions: vi.fn(),
  openDownloadLink: vi.fn(),
  restoreVersion: vi.fn(),
  t: vi.fn((key: string, options?: { version?: number }) =>
    key === "fileVersions.versionLabel" ? `Version ${options?.version ?? ""}` : key,
  ),
}));

vi.mock("material-ui-confirm", () => ({
  useConfirm: () => mocks.confirm,
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: mocks.t }),
}));

vi.mock("../../../shared/api/filesApi", () => ({
  filesApi: {
    deleteVersion: mocks.deleteVersion,
    getDownloadLink: mocks.getDownloadLink,
    getVersionDownloadLink: mocks.getVersionDownloadLink,
    listVersions: mocks.listVersions,
    restoreVersion: mocks.restoreVersion,
  },
}));

vi.mock("../../../shared/utils/fileHandlers", () => ({
  openDownloadLink: mocks.openDownloadLink,
}));

const versions: FileVersionDto[] = [
  {
    id: "file-1",
    nodeFileId: "file-1",
    fileManifestId: "manifest-2",
    name: "document.txt",
    contentType: "text/plain",
    sizeBytes: 12,
    createdAt: "2026-05-21T00:01:00Z",
    versionNumber: 3,
    isCurrent: true,
    isOriginal: false,
    canDelete: false,
  },
  {
    id: "version-2",
    nodeFileId: "file-1",
    fileManifestId: "manifest-2",
    name: "document.txt",
    contentType: "text/plain",
    sizeBytes: 8,
    createdAt: "2026-05-21T00:00:30Z",
    versionNumber: 2,
    isCurrent: false,
    isOriginal: false,
    canDelete: true,
  },
  {
    id: "version-1",
    nodeFileId: "file-1",
    fileManifestId: "manifest-1",
    name: "document.txt",
    contentType: "text/plain",
    sizeBytes: 5,
    createdAt: "2026-05-21T00:00:00Z",
    versionNumber: 1,
    isCurrent: false,
    isOriginal: true,
    canDelete: false,
  },
];

function renderDialog(onRestored = vi.fn()): void {
  render(
    <FileVersionsDialog
      fileId="file-1"
      fileName="document.txt"
      onClose={vi.fn()}
      onRestored={onRestored}
      open
    />,
  );
}

describe("FileVersionsDialog", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.confirm.mockResolvedValue({ confirmed: true });
    mocks.deleteVersion.mockResolvedValue(undefined);
    mocks.getDownloadLink.mockResolvedValue("/download/current");
    mocks.getVersionDownloadLink.mockResolvedValue("/download/version");
    mocks.listVersions.mockResolvedValue(versions);
    mocks.restoreVersion.mockResolvedValue({});
  });

  it("loads and displays file versions", async () => {
    renderDialog();

    expect(await screen.findByText("Version 3")).toBeInTheDocument();
    expect(screen.getByText("Version 2")).toBeInTheDocument();
    expect(screen.getByText("Version 1")).toBeInTheDocument();
    expect(mocks.listVersions).toHaveBeenCalledWith("file-1");
  });

  it("downloads a historical version through the version link", async () => {
    renderDialog();

    await screen.findByText("Version 2");
    fireEvent.click(screen.getAllByRole("button", { name: "common:actions.download" })[1]);

    await waitFor(() =>
      expect(mocks.getVersionDownloadLink).toHaveBeenCalledWith(
        "file-1",
        "version-2",
      ),
    );
    expect(mocks.openDownloadLink).toHaveBeenCalledWith(
      "/download/version",
      "document.txt",
    );
  });

  it("restores a historical version after confirmation", async () => {
    const onRestored = vi.fn();
    renderDialog(onRestored);

    await screen.findByText("Version 2");
    fireEvent.click(screen.getAllByRole("button", { name: "common:actions.restore" })[0]);

    await waitFor(() =>
      expect(mocks.restoreVersion).toHaveBeenCalledWith("file-1", "version-2"),
    );
    expect(onRestored).toHaveBeenCalled();
  });

  it("deletes a removable historical version after confirmation", async () => {
    renderDialog();

    await screen.findByText("Version 2");
    fireEvent.click(screen.getByRole("button", { name: "common:actions.delete" }));

    await waitFor(() =>
      expect(mocks.deleteVersion).toHaveBeenCalledWith("file-1", "version-2"),
    );
    expect(mocks.listVersions).toHaveBeenCalledTimes(2);
  });
});
