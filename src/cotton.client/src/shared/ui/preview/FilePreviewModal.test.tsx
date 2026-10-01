import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { FilePreviewModal } from "./FilePreviewModal";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock("./PdfPreview", () => ({ PdfPreview: () => null }));
vi.mock("./TextPreview", () => ({ TextPreview: () => null }));
vi.mock("./ModelPreview", () => ({ ModelPreview: () => null }));
afterEach(cleanup);

describe("unavailable file preview", () => {
  it("shows the file name and downloads only after an explicit click", () => {
    const download = vi.fn().mockResolvedValue(undefined);
    render(
      <FilePreviewModal
        isOpen
        fileId="file-1"
        fileName="archive.zip"
        fileType="other"
        fileSizeBytes={12}
        onClose={() => {}}
        onDownload={download}
      />,
    );
    expect(screen.getByRole("dialog")).toHaveTextContent("archive.zip");
    expect(screen.getByText("share:unsupported")).toBeVisible();
    expect(download).not.toHaveBeenCalled();
    fireEvent.click(
      screen.getByRole("button", { name: "common:actions.download" }),
    );
    expect(download).toHaveBeenCalledExactlyOnceWith("file-1", "archive.zip");
  });
});
