import { beforeEach, describe, expect, it } from "vitest";
import {
  useMoveClipboardStore,
  type MoveClipboardItem,
} from "./moveClipboardStore";

const makeItem = (
  id: string,
  overrides: Partial<MoveClipboardItem> = {},
): MoveClipboardItem => ({
  id,
  kind: "file",
  sourceParentId: "parent-1",
  ...overrides,
});

describe("moveClipboardStore", () => {
  beforeEach(() => {
    useMoveClipboardStore.getState().clear();
  });

  it("starts empty", () => {
    expect(useMoveClipboardStore.getState().items).toEqual([]);
  });

  it("setItems replaces the clipboard contents", () => {
    const { setItems } = useMoveClipboardStore.getState();

    setItems([makeItem("a"), makeItem("b", { kind: "folder" })]);
    expect(useMoveClipboardStore.getState().items.map((item) => item.id)).toEqual([
      "a",
      "b",
    ]);

    setItems([makeItem("c")]);
    expect(useMoveClipboardStore.getState().items.map((item) => item.id)).toEqual([
      "c",
    ]);
  });

  it("keeps the provided readonly item array reference", () => {
    const items = [makeItem("a")] as const;

    useMoveClipboardStore.getState().setItems(items);

    expect(useMoveClipboardStore.getState().items).toBe(items);
  });

  it("clear empties the clipboard", () => {
    const store = useMoveClipboardStore.getState();
    store.setItems([makeItem("a"), makeItem("b")]);

    store.clear();

    expect(useMoveClipboardStore.getState().items).toEqual([]);
  });
});
