import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { createInstance } from "i18next";
import { I18nextProvider } from "react-i18next";
import { MemoryRouter } from "react-router-dom";
import { VirtuosoMockContext } from "react-virtuoso";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { NodeFileManifestDto } from "../../shared/api/nodesApi";
import { SearchModal } from "./SearchModal";

const mocks = vi.hoisted(() => ({
  search: vi.fn(),
  download: vi.fn(),
  addHistory: vi.fn(),
}));

vi.mock("../../shared/api/layoutsApi", () => ({
  layoutsApi: { search: mocks.search },
}));

vi.mock("../../shared/api/queries/layouts", () => ({
  useRootNodeQuery: () => ({ data: { layoutId: "layout-1" } }),
}));

vi.mock("@shared/hooks/useFileInteractionHandlers", () => ({
  useFileInteractionHandlers: () => ({
    previewProps: {},
    lightboxProps: {},
    handleFileClick: vi.fn(),
    handleDownloadFile: mocks.download,
    handleShareFile: vi.fn(),
    lightboxOpen: false,
    mediaItems: [],
    handleMediaClick: vi.fn(),
  }),
}));

vi.mock("@shared/ui/preview", () => ({
  FilePreviewModal: () => null,
  MediaLightbox: () => null,
}));

vi.mock("./hooks/useSearchHistory", () => ({
  useSearchHistory: () => ({
    entries: [],
    addQuery: mocks.addHistory,
    removeQuery: vi.fn(),
    clear: vi.fn(),
  }),
}));

const translations = createInstance();
await translations.init({ lng: "en", resources: {} });

const file: NodeFileManifestDto = {
  id: "file-1",
  nodeId: "folder-1",
  ownerId: "owner-1",
  name: "report.pdf",
  contentType: "application/pdf",
  sizeBytes: 1024,
  metadata: {},
  createdAt: "2026-01-01T00:00:00.000Z",
  updatedAt: "2026-01-01T00:00:00.000Z",
};

describe("SearchModal downloads", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.download.mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it.each([
    { name: "report.pdf", contentType: "application/pdf" },
    { name: "report.zip", contentType: "application/zip" },
  ])("keeps the search open when downloading $name", async (overrides) => {
    const resultFile = { ...file, ...overrides };
    const onClose = vi.fn();
    mocks.search.mockResolvedValue({
      data: { nodes: [], files: [resultFile], nodePaths: {}, filePaths: {} },
      totalCount: 1,
    });

    render(
      <I18nextProvider i18n={translations}>
        <MemoryRouter>
          <VirtuosoMockContext.Provider
            value={{ viewportHeight: 500, itemHeight: 68 }}
          >
            <SearchModal open onClose={onClose} />
          </VirtuosoMockContext.Provider>
        </MemoryRouter>
      </I18nextProvider>,
    );

    const searchInput = screen.getByRole("textbox");
    fireEvent.change(searchInput, { target: { value: "report" } });
    const downloadButton = await screen.findByRole("button", {
      name: "actions.downloadFile",
    });

    fireEvent.click(downloadButton);

    await waitFor(() => {
      expect(mocks.download).toHaveBeenCalledExactlyOnceWith(
        resultFile.id,
        resultFile.name,
      );
    });
    expect(onClose).not.toHaveBeenCalled();
    expect(searchInput).toHaveValue("report");
    expect(screen.getByText(resultFile.name)).toBeVisible();
    expect(mocks.addHistory).toHaveBeenCalledWith("report");

    fireEvent.click(downloadButton);
    expect(mocks.download).toHaveBeenCalledTimes(2);
    expect(onClose).not.toHaveBeenCalled();

    fireEvent.click(
      screen.getByRole("button", { name: "actions.openContainingFolder" }),
    );
    expect(onClose).toHaveBeenCalledOnce();
  });
});
