import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createFile } from "../../../test/fileFixtures";
import { DashboardRecentFilesWidget } from "./DashboardRecentFilesWidget";

const actions = vi.hoisted(() => ({ open: vi.fn(), download: vi.fn() }));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock("../../../shared/api/queries/layouts", () => ({
  useRecentFilesQuery: () => ({
    data: [createFile({ name: "archive.zip" })],
    isPending: false,
    isError: false,
  }),
}));
vi.mock("@shared/hooks/useFileInteractionHandlers", () => ({
  useFileInteractionHandlers: () => ({
    previewState: { isOpen: false },
    handleFileClick: actions.open,
    handleDownloadFile: actions.download,
    lightboxOpen: false,
  }),
}));
vi.mock("@shared/ui/preview", () => ({
  FilePreviewModal: () => null,
  MediaLightbox: () => null,
}));
const Location = () => {
  const location = useLocation();
  return (
    <output>
      {location.pathname}:{JSON.stringify(location.state)}
    </output>
  );
};
afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("dashboard file actions", () => {
  it("opens the file on the dashboard and navigates only through the separate folder button", () => {
    render(
      <MemoryRouter initialEntries={["/"]}>
        <DashboardRecentFilesWidget
          enabled
          layoutId="layout-1"
          size={1}
          widgetId="recentFiles"
        />
        <Location />
      </MemoryRouter>,
    );
    fireEvent.click(screen.getByRole("button", { name: /archive.zip/ }));
    expect(actions.open).toHaveBeenCalledExactlyOnceWith(
      "file-1",
      "archive.zip",
      1,
    );
    expect(screen.getByRole("status").textContent).toBe("/:null");
    fireEvent.click(
      screen.getByRole("button", { name: "files:actions.goToFolder" }),
    );
    expect(screen.getByRole("status").textContent).toBe("/files/folder-1:null");
    expect(actions.open).toHaveBeenCalledTimes(1);
    expect(actions.download).not.toHaveBeenCalled();
  });
});
