import { create } from "zustand";

export type MoveClipboardKind = "folder" | "file";

export interface MoveClipboardItem {
  id: string;
  kind: MoveClipboardKind;
  name: string;
  /** The parent node ID the item belonged to at the moment it was cut. */
  sourceParentId: string;
}

interface MoveClipboardState {
  items: ReadonlyArray<MoveClipboardItem>;
  setItems: (items: ReadonlyArray<MoveClipboardItem>) => void;
  clear: () => void;
  hasItem: (id: string) => boolean;
  count: () => number;
}

export const useMoveClipboardStore = create<MoveClipboardState>((set, get) => ({
  items: [],
  setItems: (items) => set({ items }),
  clear: () => set({ items: [] }),
  hasItem: (id) => get().items.some((item) => item.id === id),
  count: () => get().items.length,
}));
