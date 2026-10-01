import type { DragEvent } from "react";
import { vi } from "vitest";

export class FakeDataTransfer implements DataTransfer {
  private store = new Map<string, string>();
  effectAllowed: DataTransfer["effectAllowed"] = "uninitialized";
  dropEffect: DataTransfer["dropEffect"] = "none";
  items: DataTransferItemList = Object.assign(new Array<DataTransferItem>(), {
    add: () => null,
    clear: () => {},
    remove: () => {},
  });
  setDragImage = vi.fn<DataTransfer["setDragImage"]>();

  get files(): FileList {
    const input = document.createElement("input");
    input.type = "file";
    if (input.files === null) {
      throw new Error("File inputs must provide a FileList");
    }
    return input.files;
  }

  setData(format: string, value: string): void {
    this.store.set(format, value);
  }

  getData(format: string): string {
    return this.store.get(format) ?? "";
  }

  clearData(format?: string): void {
    if (format !== undefined) {
      this.store.delete(format);
    } else {
      this.store.clear();
    }
  }

  get types(): readonly string[] {
    return Array.from(this.store.keys());
  }
}

export const makeDragEvent = (
  dataTransfer: DataTransfer,
  currentTarget = document.createElement("div"),
  relatedTarget: EventTarget | null = null,
): DragEvent<HTMLDivElement> => {
  const nativeEvent = Object.assign(
    new MouseEvent("dragstart", { relatedTarget }),
    { dataTransfer },
  );
  return {
    dataTransfer,
    nativeEvent,
    currentTarget,
    target: currentTarget,
    relatedTarget,
    altKey: false,
    button: 0,
    buttons: 0,
    clientX: 0,
    clientY: 0,
    ctrlKey: false,
    metaKey: false,
    movementX: 0,
    movementY: 0,
    pageX: 0,
    pageY: 0,
    screenX: 0,
    screenY: 0,
    shiftKey: false,
    getModifierState: () => false,
    detail: 0,
    bubbles: true,
    cancelable: true,
    defaultPrevented: false,
    eventPhase: 2,
    isTrusted: false,
    timeStamp: nativeEvent.timeStamp,
    type: nativeEvent.type,
    view: { document, styleMedia: { type: "screen", matchMedium: () => true } },
    preventDefault: vi.fn(),
    stopPropagation: vi.fn(),
    persist: vi.fn(),
    isDefaultPrevented: () => false,
    isPropagationStopped: () => false,
  };
};
