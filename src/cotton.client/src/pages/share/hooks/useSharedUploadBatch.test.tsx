import { act, cleanup, renderHook } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import type { AppTask } from "@shared/tasks/types";
import { useSharedUploadBatch } from "./useSharedUploadBatch";

const store = vi.hoisted(() => ({
  tasks: [] as AppTask[],
  listeners: new Set<() => void>(),
}));
vi.mock("@shared/upload/UploadManager", () => ({
  uploadManager: {
    subscribe: (listener: () => void) => {
      store.listeners.add(listener);
      return () => store.listeners.delete(listener);
    },
    getSnapshot: () => ({ tasks: store.tasks }),
  },
}));
afterEach(() => {
  cleanup();
  store.tasks = [];
  store.listeners.clear();
});
const task = (id: string): AppTask => ({
  id,
  kind: "upload",
  label: id,
  scopeLabel: "folder",
  bytesTotal: 10,
  bytesCompleted: 0,
  progress01: 0,
  status: "running",
});
const publish = (tasks: AppTask[]) =>
  act(() => {
    store.tasks = tasks;
    store.listeners.forEach((listener) => listener());
  });

it("tracks only the selected batch, retains completed files after pruning and resets for another upload", () => {
  const { result } = renderHook(useSharedUploadBatch);
  store.tasks = [task("mine"), task("other")];
  act(() => result.current.onQueued(["mine"]));
  publish([
    { ...task("mine"), status: "completed", bytesCompleted: 10, progress01: 1 },
    task("other"),
  ]);
  expect(result.current.finished).toBe(true);
  expect(result.current.completed).toBe(1);
  expect(result.current.progress).toBe(1);
  publish([]);
  expect(result.current.completed).toBe(1);
  act(() => result.current.reset());
  expect(result.current.tasks).toEqual([]);
  expect(result.current.finished).toBe(false);
});

it("does not mark a failed batch successful and includes files added during an active upload", () => {
  const { result } = renderHook(useSharedUploadBatch);
  store.tasks = [task("first")];
  act(() => result.current.onQueued(["first"]));
  store.tasks = [task("first"), task("second")];
  act(() => result.current.onQueued(["second"]));
  publish(store.tasks.map((entry) => ({ ...entry, status: "failed" })));
  expect(result.current.finished).toBe(true);
  expect(result.current.failed).toBe(2);
  expect(result.current.completed).toBe(0);
});
