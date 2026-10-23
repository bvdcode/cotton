export function fitDocumentPages(container: HTMLElement): () => void {
  const document = container.ownerDocument;
  const wrapper = container.querySelector<HTMLElement>(".docx-wrapper");
  const pages = container.querySelectorAll<HTMLElement>("section.docx");
  if (!wrapper || pages.length === 0) {
    return () => {};
  }
  const pageWidth = Math.max(...Array.from(pages, (page) => page.offsetWidth));
  const style = document.createElement("style");
  container.append(style);
  const fit = () => {
    const computed = document.defaultView?.getComputedStyle(wrapper);
    if (!computed || pageWidth === 0) {
      return;
    }
    const available =
      wrapper.clientWidth -
      Number.parseFloat(computed.paddingLeft) -
      Number.parseFloat(computed.paddingRight);
    const scale = Math.min(1, Math.max(0, available) / pageWidth);
    style.textContent = `.docx-wrapper > section.docx { zoom: ${scale}; }`;
  };
  const observer = new ResizeObserver(fit);
  observer.observe(container);
  fit();
  return () => {
    observer.disconnect();
    style.remove();
  };
}
