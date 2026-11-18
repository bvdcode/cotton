import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import { createFile, createFolder } from "../../../test/fileFixtures";
import type { NodeResponse } from "../nodesApi";
import { upsertCachedFiles } from "./nodeCache";
import { queryKeys } from "./queryKeys";

it("adds one batch to complete listings while preserving partial-page membership", () => {
  const client = new QueryClient();
  const node = createFolder({ id: "folder" });
  const original = createFile({ id: "first", nodeId: node.id });
  const replacement = { ...original, name: "updated" };
  const added = createFile({ id: "second", nodeId: node.id });
  const completeKey = queryKeys.nodeChildren.complete(node.id, "user");
  const pageKey = queryKeys.nodeChildren.page(node.id, "user", 1);
  const otherKey = queryKeys.nodeChildren.complete(node.id, "other");
  const complete: NodeResponse = {
    content: { ...node, node, nodes: [], files: [original] },
    totalCount: 1,
  };
  client.setQueryData(completeKey, complete);
  client.setQueryData(pageKey, { ...complete, totalCount: 100 });
  client.setQueryData(otherKey, complete);
  expect(
    upsertCachedFiles(client, "user", node.id, [
      replacement,
      { ...added, name: "old" },
      added,
    ]),
  ).toBe(true);
  expect(client.getQueryData<NodeResponse>(completeKey)?.content.files).toEqual(
    [replacement, added],
  );
  expect(client.getQueryData<NodeResponse>(completeKey)?.totalCount).toBe(2);
  expect(client.getQueryData<NodeResponse>(pageKey)?.content.files).toEqual([
    replacement,
  ]);
  expect(client.getQueryData<NodeResponse>(pageKey)?.totalCount).toBe(100);
  expect(client.getQueryData(otherKey)).toBe(complete);
  client.clear();
});

describe("uploaded-file batches", () => {
  it("does not add absent files to incomplete listings or alter another folder", () => {
    const client = new QueryClient();
    const node = createFolder({ id: "folder" });
    const key = queryKeys.nodeChildren.overview(node.id, "user", 25000);
    const page: NodeResponse = {
      content: { ...node, node, nodes: [], files: [] },
      totalCount: 30000,
    };
    client.setQueryData(key, page);
    expect(upsertCachedFiles(client, "user", node.id, [createFile()])).toBe(
      false,
    );
    expect(upsertCachedFiles(client, "user", "different", [createFile()])).toBe(
      false,
    );
    expect(client.getQueryData(key)).toBe(page);
    client.clear();
  });
});
