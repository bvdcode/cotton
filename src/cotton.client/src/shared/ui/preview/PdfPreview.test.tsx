import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { PdfPreview } from "./PdfPreview";
import { getCachedPdfUrl } from "./pdfPreviewSource";

const mocks = vi.hoisted(() => ({
  destroy: vi.fn().mockResolvedValue(undefined),
  document: vi.fn(),
  getDownloadLink: vi.fn(),
  textRender: vi.fn(),
  textLayer: vi.fn(),
  t: (key: string) => key,
}));
vi.mock("react-i18next", () => ({ useTranslation: () => ({ t: mocks.t }) }));
vi.mock("../../api/filesApi", () => ({
  filesApi: { getDownloadLink: mocks.getDownloadLink },
}));
vi.mock("pdfjs-dist/legacy/build/pdf.mjs", () => ({
  GlobalWorkerOptions: {},
  TextLayer: class {
    constructor(options: object) {
      mocks.textLayer(options);
    }
    render = mocks.textRender;
  },
  getDocument: mocks.document,
}));
beforeEach(() => {
  vi.clearAllMocks();
  mocks.textRender.mockResolvedValue(undefined);
});
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

function setupMobilePages() {
  vi.spyOn(navigator, "userAgent", "get").mockReturnValue("iPhone");
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("pdf data")));
  vi.stubGlobal("CanvasRenderingContext2D", class {});
  vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(
    new CanvasRenderingContext2D(),
  );
  vi.spyOn(HTMLElement.prototype, "clientWidth", "get").mockReturnValue(640);
  vi.stubGlobal("devicePixelRatio", 2);
  const page = {
    getViewport: vi.fn(({ scale }: { scale: number }) => ({
      width: 400 * scale,
      height: 600 * scale,
    })),
    render: vi.fn().mockReturnValue({ promise: Promise.resolve() }),
    getTextContent: vi.fn().mockResolvedValue({ items: [] }),
  };
  const getPage = vi.fn().mockResolvedValue(page);
  mocks.document.mockReturnValue({
    promise: Promise.resolve({ numPages: 2, getPage }),
    destroy: mocks.destroy,
  });
  return { page, getPage };
}

describe("mobile PDF pages", () => {
  it("renders each page with selectable text and scales the canvas for pixel density", async () => {
    const { page, getPage } = setupMobilePages();
    const { container } = render(
      <PdfPreview
        source={{ kind: "objectUrl", url: "blob:pages" }}
        fileName="pages.pdf"
      />,
    );
    await waitFor(() => expect(mocks.textRender).toHaveBeenCalledTimes(2));
    expect(getPage.mock.calls).toEqual([[1], [2]]);
    const canvases = container.querySelectorAll("canvas");
    expect(canvases).toHaveLength(2);
    const canvas = canvases[0];
    expect(canvas.width).toBe(Number.parseFloat(canvas.style.width) * 2);
    expect(canvas.height).toBe(Number.parseFloat(canvas.style.height) * 2);
    expect(container.querySelectorAll(".textLayer")).toHaveLength(2);
    expect(page.getTextContent).toHaveBeenCalledTimes(2);
    await waitFor(() => expect(screen.queryByRole("progressbar")).toBeNull());
  });

  it.each(["page", "canvas", "text", "layer"] as const)(
    "stops after closing during the %s stage without continuing to the next page",
    async (stage) => {
      const { page, getPage } = setupMobilePages();
      let complete = () => {};
      const pending = new Promise<void>((resolve) => {
        complete = resolve;
      });
      switch (stage) {
        case "page":
          getPage.mockImplementation(async () => {
            await pending;
            return page;
          });
          break;
        case "canvas":
          page.render.mockReturnValue({ promise: pending });
          break;
        case "text":
          page.getTextContent.mockImplementation(async () => {
            await pending;
            return { items: [] };
          });
          break;
        case "layer":
          mocks.textRender.mockReturnValue(pending);
          break;
      }
      const { unmount } = render(
        <PdfPreview
          source={{ kind: "objectUrl", url: "blob:cancel-pages" }}
          fileName="pages.pdf"
        />,
      );
      const stages = {
        page: getPage,
        canvas: page.render,
        text: page.getTextContent,
        layer: mocks.textRender,
      };
      await waitFor(() => expect(stages[stage]).toHaveBeenCalledOnce());
      const callsBeforeClose = Object.values(stages).map(
        (operation) => operation.mock.calls.length,
      );
      unmount();
      await act(async () => complete());
      expect(mocks.destroy).toHaveBeenCalledOnce();
      expect(
        Object.values(stages).map((operation) => operation.mock.calls.length),
      ).toEqual(callsBeforeClose);
      expect(getPage).toHaveBeenCalledOnce();
    },
  );

  it("shows a display error and stops when a page cannot be rendered", async () => {
    const { page, getPage } = setupMobilePages();
    page.render.mockImplementation(() => {
      throw new Error("render failed");
    });
    render(
      <PdfPreview
        source={{ kind: "objectUrl", url: "blob:broken-pages" }}
        fileName="pages.pdf"
      />,
    );
    expect(
      await screen.findByText("preview.errors.pdfDisplayFailed"),
    ).toBeVisible();
    expect(screen.queryByRole("progressbar")).toBeNull();
    expect(page.getTextContent).not.toHaveBeenCalled();
    expect(getPage).toHaveBeenCalledOnce();
  });
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
