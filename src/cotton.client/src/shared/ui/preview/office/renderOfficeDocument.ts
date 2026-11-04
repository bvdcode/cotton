import { fitDocumentPages } from "./fitDocumentPages";

export async function renderOfficeDocument(
  bytes: ArrayBuffer,
  format: "docx" | "pptx",
  container: HTMLElement,
  signal: AbortSignal,
): Promise<() => void> {
  switch (format) {
    case "docx": {
      const { renderAsync } = await import("docx-preview");
      signal.throwIfAborted();
      await renderAsync(bytes, container, container, {
        renderAltChunks: false,
        useBase64URL: true,
      });
      signal.throwIfAborted();
      const stopFitting = fitDocumentPages(container);
      return () => {
        stopFitting();
        container.remove();
      };
    }
    case "pptx": {
      const { PptxViewer, RECOMMENDED_ZIP_LIMITS } =
        await import("@aiden0z/pptx-renderer/browser");
      signal.throwIfAborted();
      const viewer = await PptxViewer.open(bytes, container, {
        signal,
        externalMedia: "block",
        zipLimits: RECOMMENDED_ZIP_LIMITS,
        lazySlides: true,
        lazyMedia: true,
        listOptions: { windowed: true },
        pdfjs: false,
      });
      return () => viewer.destroy();
    }
  }
}
