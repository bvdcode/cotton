import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createFile, createFolder } from "../../test/fileFixtures";
import { nodesApi } from "../api/nodesApi";
import { filesApi } from "../api/filesApi";
import { useVault } from "../crypto";
import { useMoveClipboardStore } from "../store/moveClipboardStore";
import { taskManager } from "../tasks/taskManager";
import { ConflictAction } from "../types/nameConflict";
import { useMoveOperations } from "./useMoveOperations";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock("../store/nodesActions", () => ({
  refreshNodeContent: vi.fn(async () => {}),
}));
vi.mock("@shared/ui/notifications", () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

const target = createFolder({ id: "target", name: "Photos" });
const files = Array.from({ length: 196 }, (_, index) =>
  createFile({
    id: `file-${index}`,
    name: `photo-${index}.jpg`,
    nodeId: "source",
  }),
);

const createDeferred = <T,>() => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((complete) => {
    resolve = complete;
  });
  return { promise, resolve };
};

describe("paste task progress", () => {
  beforeEach(() => {
    taskManager.clearFinished();
    useVault.setState({ isUnlocked: false, masterKey: null });
    useMoveClipboardStore.getState().setItems(
      files.map((file) => ({
        kind: "file",
        id: file.id,
        sourceParentId: file.nodeId,
        file,
      })),
    );
    vi.spyOn(nodesApi, "getNode").mockResolvedValue(target);
    vi.spyOn(nodesApi, "getAncestors").mockResolvedValue([]);
    vi.spyOn(filesApi, "moveFile").mockImplementation(async (id) => {
      const file = files.find((item) => item.id === id);
      if (!file) {
        throw new Error("Unexpected file.");
      }
      return { ...file, nodeId: target.id };
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    taskManager.clearFinished();
    useMoveClipboardStore.getState().clear();
  });

  it("starts before loading the target and reports each completed item", async () => {
    const targetGate = createDeferred<typeof target>();
    const secondFileGate = createDeferred<void>();
    vi.mocked(nodesApi.getNode).mockReturnValue(targetGate.promise);
    vi.mocked(filesApi.moveFile).mockImplementation(async (id) => {
      if (id === files[1].id) {
        await secondFileGate.promise;
      }
      const file = files.find((item) => item.id === id);
      if (!file) {
        throw new Error("Unexpected file.");
      }
      return { ...file, nodeId: target.id };
    });
    const { result } = renderHook(() =>
      useMoveOperations({
        confirmConflict: async () => ConflictAction.Skip,
      }),
    );

    let paste!: Promise<void>;
    act(() => {
      paste = result.current.pasteInto(target.id);
    });

    expect(taskManager.getSnapshot().tasks[0]).toMatchObject({
      label: "operations.move",
      status: "running",
      bytesTotal: 196,
      bytesCompleted: 0,
    });
    expect(filesApi.moveFile).not.toHaveBeenCalled();
    await act(async () => {
      targetGate.resolve(target);
    });
    await vi.waitFor(() => {
      expect(taskManager.getSnapshot().tasks[0].bytesCompleted).toBe(1);
    });

    await act(async () => {
      secondFileGate.resolve();
      await paste;
    });

    expect(filesApi.moveFile).toHaveBeenCalledTimes(196);
    expect(taskManager.getSnapshot().tasks[0]).toMatchObject({
      status: "completed",
      bytesCompleted: 196,
    });
    expect(useMoveClipboardStore.getState().items).toEqual([]);
  });

  it.each(["move", "copy"] as const)(
    "labels a %s task and fails it when the target cannot be loaded",
    async (operation) => {
      useMoveClipboardStore
        .getState()
        .setItems(useMoveClipboardStore.getState().items, operation);
      vi.mocked(nodesApi.getNode).mockRejectedValue(new Error("Unavailable"));
      const { result } = renderHook(() =>
        useMoveOperations({
          confirmConflict: async () => ConflictAction.Skip,
        }),
      );

      await act(() => result.current.pasteInto(target.id));

      expect(taskManager.getSnapshot().tasks[0]).toMatchObject({
        label: `operations.${operation}`,
        status: "failed",
      });
      expect(useMoveClipboardStore.getState().items).toHaveLength(196);
      expect(filesApi.moveFile).not.toHaveBeenCalled();
    },
  );
});
