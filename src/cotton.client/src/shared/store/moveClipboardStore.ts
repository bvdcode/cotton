import { create } from "zustand";

export type MoveClipboardKind = "folder" | "file";
export type FileTransferOperation = "move" | "copy";

export interface MoveClipboardFileSnapshot {
  name: string;
  contentType: string;
  sizeBytes: number;
  metadata: Record<string, string>;
}

export interface MoveClipboardItem {
  id: string;
  kind: MoveClipboardKind;
  /** The parent node ID the item belonged to at the moment it was cut. */
  sourceParentId: string;
  file?: MoveClipboardFileSnapshot;
}

interface MoveClipboardState {
  operation: FileTransferOperation;
  items: ReadonlyArray<MoveClipboardItem>;
  setItems: (
    items: ReadonlyArray<MoveClipboardItem>,
    operation?: FileTransferOperation,
  ) => void;
  clear: () => void;
}

export const useMoveClipboardStore = create<MoveClipboardState>((set) => ({
  operation: "move",
  items: [],
  setItems: (items, operation = "move") => set({ items, operation }),
  clear: () => set({ items: [], operation: "move" }),
}));
