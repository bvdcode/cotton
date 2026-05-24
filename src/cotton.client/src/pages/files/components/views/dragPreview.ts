import type React from "react";

const clampOffset = (value: number, max: number): number => {
  if (!Number.isFinite(value)) return Math.max(0, max / 2);
  return Math.max(0, Math.min(max, value));
};

const scheduleRemoval = (element: HTMLElement): void => {
  window.requestAnimationFrame(() => {
    element.remove();
  });
};

/**
 * Native browser drag previews are snapshots of live DOM. A tile with animated
 * overflow text can otherwise leak outside its card and visually grab neighbors.
 */
export const setClippedDragImage = (
  event: React.DragEvent,
  sourceElement: HTMLElement,
): void => {
  if (typeof event.dataTransfer.setDragImage !== "function") return;

  const rect = sourceElement.getBoundingClientRect();
  if (rect.width <= 0 || rect.height <= 0) return;

  const wrapper = document.createElement("div");
  wrapper.style.position = "fixed";
  wrapper.style.left = "-10000px";
  wrapper.style.top = "0";
  wrapper.style.width = `${Math.ceil(rect.width)}px`;
  wrapper.style.height = `${Math.ceil(rect.height)}px`;
  wrapper.style.overflow = "hidden";
  wrapper.style.pointerEvents = "none";
  wrapper.style.contain = "paint";

  const clone = sourceElement.cloneNode(true) as HTMLElement;
  clone.style.width = "100%";
  clone.style.height = "100%";
  clone.style.maxWidth = "100%";
  clone.style.maxHeight = "100%";
  clone.style.overflow = "hidden";
  clone.style.pointerEvents = "none";
  clone.style.boxSizing = "border-box";
  clone.style.contain = "paint";

  wrapper.appendChild(clone);
  document.body.appendChild(wrapper);

  const offsetX = clampOffset(event.clientX - rect.left, rect.width);
  const offsetY = clampOffset(event.clientY - rect.top, rect.height);
  event.dataTransfer.setDragImage(wrapper, offsetX, offsetY);
  scheduleRemoval(wrapper);
};
