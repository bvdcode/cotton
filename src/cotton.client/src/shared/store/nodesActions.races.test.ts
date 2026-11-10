import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { createFolder } from "../../test/fileFixtures";
import { getCurrentNode } from "../../test/nodeCacheFixtures";
import { layoutsApi } from "../api/layoutsApi";
import type { NodeDto } from "../api/layoutsApi";
import { nodesApi } from "../api/nodesApi";
import { queryClient } from "../api/queries/queryClient";
import { queryKeys } from "../api/queries/queryKeys";
import { loadNode, loadRoot } from "./nodesActions";
import { useNodesStore } from "./nodesStore";

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: Error) => void;
  const promise = new Promise<T>((onResolve, onReject) => {
    resolve = onResolve;
    reject = onReject;
  });
  return { promise, resolve, reject };
}

beforeEach(() => {
  useNodesStore.getState().reset("first-user");
  vi.spyOn(nodesApi, "getAncestors").mockResolvedValue([]);
});
afterEach(() => {
  useNodesStore.getState().reset(null);
  vi.restoreAllMocks();
});

it("keeps the latest navigation when older metadata resolves last", async () => {
  const first = deferred<NodeDto>();
  const second = createFolder({ id: "second" });
  vi.spyOn(nodesApi, "getNode")
    .mockReturnValueOnce(first.promise)
    .mockResolvedValueOnce(second);
  const older = loadNode("first", { loadChildren: false });
  await loadNode(second.id, { loadChildren: false });
  first.resolve(createFolder({ id: "first" }));
  await older;
  expect(getCurrentNode()).toEqual(second);
  expect(useNodesStore.getState()).toMatchObject({
    currentNodeId: second.id,
    loading: false,
    error: null,
  });
});

it("ignores a failure from an older navigation", async () => {
  const first = deferred<NodeDto>();
  const second = createFolder({ id: "second" });
  vi.spyOn(nodesApi, "getNode")
    .mockReturnValueOnce(first.promise)
    .mockResolvedValueOnce(second);
  const older = loadNode("first", { loadChildren: false });
  await loadNode(second.id, { loadChildren: false });
  first.reject(new Error("Old request failed"));
  await older;
  expect(getCurrentNode()).toEqual(second);
  expect(useNodesStore.getState().error).toBeNull();
});

it("cancels the old account's request and keeps its late response out of the cache", async () => {
  const first = deferred<NodeDto>();
  const second = createFolder({ id: "second" });
  const get = vi
    .spyOn(nodesApi, "getNode")
    .mockReturnValueOnce(first.promise)
    .mockResolvedValueOnce(second);
  const older = loadNode("first", { loadChildren: false });
  const signal = get.mock.calls[0][1];
  useNodesStore.getState().reset("second-user");
  await loadNode(second.id, { loadChildren: false });
  first.resolve(createFolder({ id: "first" }));
  await older;
  expect(signal?.aborted).toBe(true);
  expect(getCurrentNode()).toEqual(second);
  expect(
    queryClient.getQueryData(
      queryKeys.nodeNavigation.view("first", "first-user"),
    ),
  ).toBeUndefined();
  expect(useNodesStore.getState().cacheOwnerUserId).toBe("second-user");
});

it("ignores an old root resolution after a new navigation", async () => {
  const root = deferred<NodeDto>();
  const child = createFolder({ id: "child" });
  vi.spyOn(layoutsApi, "resolve").mockReturnValue(root.promise);
  vi.spyOn(nodesApi, "getNode").mockResolvedValue(child);
  const older = loadRoot({ loadChildren: false });
  await loadNode(child.id, { loadChildren: false });
  root.resolve(createFolder({ id: "root" }));
  expect(await older).toBeNull();
  expect(getCurrentNode()).toEqual(child);
});

it("starts a fresh forced reload instead of joining an older pending request", async () => {
  const first = deferred<NodeDto>();
  const fresh = createFolder({ id: "folder", name: "Fresh" });
  const get = vi
    .spyOn(nodesApi, "getNode")
    .mockReturnValueOnce(first.promise)
    .mockResolvedValueOnce(fresh);
  const older = loadNode("folder", { loadChildren: false });
  await loadNode("folder", { loadChildren: false, force: true });
  first.resolve(createFolder({ id: "folder", name: "Stale" }));
  await older;
  expect(get).toHaveBeenCalledTimes(2);
  expect(getCurrentNode()).toEqual(fresh);
});
