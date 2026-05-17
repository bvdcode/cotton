import { create } from "zustand";

export type MoveClipboardKind = "folder" | "file";

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
  items: ReadonlyArray<MoveClipboardItem>;
  setItems: (items: ReadonlyArray<MoveClipboardItem>) => void;
  clear: () => void;
}

export const useMoveClipboardStore = create<MoveClipboardState>((set) => ({
  items: [],
  setItems: (items) => set({ items }),
  clear: () => set({ items: [] }),
}));
