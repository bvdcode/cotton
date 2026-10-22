import {
  TextLayer,
  type PDFDocumentProxy,
} from "pdfjs-dist/legacy/build/pdf.mjs";

export async function renderPdfPages(
  pdf: PDFDocumentProxy,
  container: HTMLDivElement,
  isCancelled: () => boolean,
  displayError: string,
): Promise<void> {
  container.innerHTML = "";
  const style = window.getComputedStyle(container);
  const paddingLeft = Number.parseFloat(style.paddingLeft) || 0;
  const paddingRight = Number.parseFloat(style.paddingRight) || 0;
  const measuredWidth = container.clientWidth;
  const availableWidth =
    (measuredWidth > 0 ? measuredWidth : window.innerWidth) -
    paddingLeft -
    paddingRight;

  if (availableWidth <= 0) {
    throw new Error("container width is 0");
  }
  const outputScale = window.devicePixelRatio || 1;

  for (let pageNumber = 1; pageNumber <= pdf.numPages; pageNumber += 1) {
    const page = await pdf.getPage(pageNumber);
    if (isCancelled()) {
      return;
    }

    const viewport = page.getViewport({ scale: 1 });
    const scale = availableWidth / viewport.width;
    const scaledViewport = page.getViewport({ scale });
    const renderViewport = page.getViewport({
      scale: scale * outputScale,
    });

    const pageWrapper = document.createElement("div");
    pageWrapper.className = "pdf-page";
    // pdf.js viewer CSS relies on this variable for proper text-layer sizing.
    pageWrapper.style.setProperty("--scale-factor", String(scale));
    pageWrapper.style.width = `${scaledViewport.width}px`;
    pageWrapper.style.height = `${scaledViewport.height}px`;

    const canvas = document.createElement("canvas");
    canvas.className = "pdf-page-canvas";
    const context = canvas.getContext("2d");
    if (!context) {
      throw new Error(displayError);
    }

    canvas.width = Math.ceil(renderViewport.width);
    canvas.height = Math.ceil(renderViewport.height);
    canvas.style.width = `${scaledViewport.width}px`;
    canvas.style.height = `${scaledViewport.height}px`;

    const textLayerDiv = document.createElement("div");
    textLayerDiv.className = "textLayer";
    textLayerDiv.style.width = `${scaledViewport.width}px`;
    textLayerDiv.style.height = `${scaledViewport.height}px`;

    pageWrapper.appendChild(canvas);
    pageWrapper.appendChild(textLayerDiv);
    container.appendChild(pageWrapper);

    const renderTask = page.render({
      canvas,
      canvasContext: context,
      viewport: renderViewport,
    });
    await renderTask.promise;

    if (isCancelled()) {
      return;
    }

    // Text layer enables selection/copy.
    const textContent = await page.getTextContent();
    if (isCancelled()) {
      return;
    }
    const textLayer = new TextLayer({
      textContentSource: textContent,
      container: textLayerDiv,
      viewport: scaledViewport,
    });
    await textLayer.render();

    if (isCancelled()) {
      return;
    }
  }
}
