import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { PdfPreview } from "./PdfPreview";
import { getCachedPdfUrl } from "./pdfPreviewSource";

const mocks = vi.hoisted(() => ({
  destroy: vi.fn().mockResolvedValue(undefined),
  document: vi.fn(),
  getDownloadLink: vi.fn(),
  t: (key: string) => key,
}));
vi.mock("react-i18next", () => ({ useTranslation: () => ({ t: mocks.t }) }));
vi.mock("../../api/filesApi", () => ({
  filesApi: { getDownloadLink: mocks.getDownloadLink },
}));
vi.mock("pdfjs-dist/legacy/build/pdf.mjs", () => ({
  GlobalWorkerOptions: {},
  TextLayer: vi.fn(),
  getDocument: mocks.document,
}));
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("caller-owned PDF sources", () => {
  it("uses the supplied URL directly without fetching, copying or caching plaintext", () => {
    const fetch = vi.fn();
    vi.stubGlobal("fetch", fetch);
    const create = vi.spyOn(URL, "createObjectURL");
    const source = { kind: "objectUrl" as const, url: "blob:decrypted-pdf" };
    const { unmount } = render(
      <PdfPreview source={source} fileName="secret.pdf" />,
    );
    expect(screen.getByTitle("secret.pdf")).toHaveAttribute("src", source.url);
    expect(fetch).not.toHaveBeenCalled();
    expect(create).not.toHaveBeenCalled();
    expect(mocks.getDownloadLink).not.toHaveBeenCalled();
    unmount();
    expect(
      getCachedPdfUrl({ kind: "fileId", fileId: "secret" }),
    ).toBeUndefined();
    render(
      <PdfPreview
        source={{ kind: "objectUrl", url: "blob:new-plaintext" }}
        fileName="secret.pdf"
      />,
    );
    expect(screen.getByTitle("secret.pdf")).toHaveAttribute(
      "src",
      "blob:new-plaintext",
    );
  });

  it("renders a mobile PDF in memory and destroys the document on close", async () => {
    vi.spyOn(navigator, "userAgent", "get").mockReturnValue("iPhone");
    const fetch = vi.fn().mockResolvedValue(new Response("pdf data"));
    vi.stubGlobal("fetch", fetch);
    const create = vi.spyOn(URL, "createObjectURL");
    mocks.document.mockReturnValue({
      promise: Promise.resolve({ numPages: 0 }),
      destroy: mocks.destroy,
    });
    const { unmount } = render(
      <PdfPreview
        source={{ kind: "objectUrl", url: "blob:mobile-pdf" }}
        fileName="mobile.pdf"
      />,
    );
    await waitFor(() => expect(mocks.document).toHaveBeenCalled());
    expect(fetch).toHaveBeenCalledExactlyOnceWith("blob:mobile-pdf");
    expect(create).not.toHaveBeenCalled();
    unmount();
    expect(mocks.destroy).toHaveBeenCalledOnce();
  });
});
