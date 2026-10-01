import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { NodeDto } from "@shared/api/layoutsApi";
import { FolderInfoDialog } from "./FolderInfoDialog";

const mocks = vi.hoisted(() => ({ getFolderStats: vi.fn() }));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    i18n: { resolvedLanguage: "en" },
    t: (key: string, options?: { name?: string; date?: string; count?: number; size?: string }) =>
      `${key}${options?.name ?? options?.count ?? options?.size ?? ""}`,
  }),
}));
vi.mock("@shared/api/nodesApi", () => ({
  nodesApi: { getFolderStats: mocks.getFolderStats },
}));
vi.mock("@shared/store/authStore", () => ({
  useAuthStore: (selector: (state: { user: { id: string } }) => string) =>
    selector({ user: { id: "user-1" } }),
}));

const folder: NodeDto = {
  id: "folder-1",
  name: "Reports",
  parentId: "root-1",
  layoutId: "layout-1",
  createdAt: "2026-09-01T00:00:00Z",
  updatedAt: "2026-09-02T00:00:00Z",
  metadata: {},
};

const renderDialog = (selectedFolder: NodeDto | null) => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <FolderInfoDialog folder={selectedFolder} onClose={vi.fn()} />
    </QueryClientProvider>,
  );
};

describe("FolderInfoDialog", () => {
  it("starts the recursive request only after the folder is selected", async () => {
    mocks.getFolderStats.mockReset();
    mocks.getFolderStats.mockResolvedValue({ folders: 2, files: 3, encryptedFiles: 0, sizeBytes: 1024 });

    renderDialog(null);
    expect(mocks.getFolderStats).not.toHaveBeenCalled();

    renderDialog(folder);
    expect(screen.getByText("folderInfo.calculating")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText("folderInfo.files3")).toBeInTheDocument());
    expect(mocks.getFolderStats).toHaveBeenCalledWith("folder-1", true);
    expect(screen.getByText("folderInfo.folders2")).toBeInTheDocument();
  });

  it("offers retry if counting fails", async () => {
    mocks.getFolderStats.mockReset();
    mocks.getFolderStats.mockRejectedValueOnce(new Error("offline"));
    mocks.getFolderStats.mockResolvedValueOnce({ folders: 0, files: 0, encryptedFiles: 0, sizeBytes: 0 });

    renderDialog(folder);
    await waitFor(() => expect(screen.getByText("folderInfo.loadFailed")).toBeInTheDocument());
    fireEvent.click(screen.getByRole("button", { name: "common:actions.retry" }));
    await waitFor(() => expect(screen.getByText("folderInfo.files0")).toBeInTheDocument());
  });
});
