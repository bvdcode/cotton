import {
  seedNodeNavigation,
  getCurrentNode,
  getCurrentAncestors,
} from "../../test/nodeCacheFixtures";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { createFolder } from "../../test/fileFixtures";
import { nodesApi, type NodeResponse } from "../api/nodesApi";
import { queryClient } from "../api/queries/queryClient";
import { queryKeys } from "../api/queries/queryKeys";
import { loadNode } from "./nodesActions";
import { useNodesStore } from "./nodesStore";

const parent = createFolder({ id: "parent", parentId: null });
const child = createFolder({ id: "child", parentId: parent.id });
const response: NodeResponse = {
  content: {
    node: parent,
    ...parent,
    nodes: [child],
    files: [],
  },
  totalCount: 1,
};
beforeEach(() => {
  useNodesStore.setState({
    cacheOwnerUserId: "user-1",
    rootNodeId: parent.id,
    loading: false,
    error: null,
  });
  seedNodeNavigation(parent, []);

  vi.spyOn(nodesApi, "getNode").mockResolvedValue(child);
  vi.spyOn(nodesApi, "getAncestors").mockResolvedValue([parent]);
  vi.spyOn(nodesApi, "getChildren").mockResolvedValue(response);
});
afterEach(() => {
  queryClient.clear();
  useNodesStore.setState({
    cacheOwnerUserId: null,
    rootNodeId: null,
    loading: false,
    error: null,
  });
  seedNodeNavigation(null, []);

  vi.restoreAllMocks();
});

it.each([
  { key: queryKeys.nodeChildren.overview(parent.id, "user-1", 25000) },
  { key: queryKeys.nodeChildren.page(parent.id, "user-1", 1) },
  { key: queryKeys.nodeChildren.complete(parent.id, "user-1") },
])("uses the cached listing and known parent path: %j", async ({ key }) => {
  queryClient.setQueryData(key, response);
  await loadNode(child.id, { loadChildren: false });
  expect(getCurrentNode()).toEqual(child);
  expect(getCurrentAncestors()).toEqual([parent]);
  expect(nodesApi.getNode).not.toHaveBeenCalled();
  expect(nodesApi.getAncestors).not.toHaveBeenCalled();
  expect(nodesApi.getChildren).not.toHaveBeenCalled();
});

it("resolves a child from a later tile page", async () => {
  queryClient.setQueryData(queryKeys.nodeChildren.tiles(parent.id, "user-1"), {
    pages: [
      { ...response, content: { ...response.content, nodes: [] } },
      response,
    ],
    pageParams: [1, 2],
  });
  await loadNode(child.id, { loadChildren: false });
  expect(getCurrentNode()).toEqual(child);
  expect(nodesApi.getNode).not.toHaveBeenCalled();
  expect(nodesApi.getAncestors).not.toHaveBeenCalled();
});

it("does not reuse another user's cached listing", async () => {
  queryClient.setQueryData(
    queryKeys.nodeChildren.overview(parent.id, "other-user", 25000),
    response,
  );
  await loadNode(child.id, { loadChildren: false });
  expect(nodesApi.getNode).toHaveBeenCalledExactlyOnceWith(
    child.id,
    expect.any(AbortSignal),
  );
  expect(nodesApi.getAncestors).toHaveBeenCalledExactlyOnceWith(child.id, {
    signal: expect.any(AbortSignal),
  });
});

it("still requests fresh metadata on a forced reload", async () => {
  queryClient.setQueryData(
    queryKeys.nodeChildren.overview(parent.id, "user-1", 25000),
    response,
  );
  await loadNode(child.id, { loadChildren: false, force: true });
  expect(nodesApi.getNode).toHaveBeenCalledExactlyOnceWith(
    child.id,
    expect.any(AbortSignal),
  );
  expect(nodesApi.getAncestors).toHaveBeenCalledExactlyOnceWith(child.id, {
    signal: expect.any(AbortSignal),
  });
});
