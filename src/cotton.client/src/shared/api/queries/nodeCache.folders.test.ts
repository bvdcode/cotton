import { QueryClient, type InfiniteData } from "@tanstack/react-query";
import { afterEach, expect, it } from "vitest";
import { createFolder } from "../../../test/fileFixtures";
import type { NodeResponse } from "../nodesApi";
import { addCachedFolder } from "./nodeCache";
import { queryKeys } from "./queryKeys";

const client = new QueryClient();
const parent = createFolder({ id: "parent" });
const child = createFolder({ id: "new", parentId: parent.id, name: "Photos" });
const listing: NodeResponse = {
  content: {
    ...parent,
    node: parent,
    nodes: [],
    files: [],
    stats: { folders: 0, files: 0, sizeBytes: 0 },
  },
  totalCount: 0,
};
afterEach(() => client.clear());

it("immediately inserts a new folder into complete visible tile and table listings", () => {
  const overview = queryKeys.nodeChildren.overview(parent.id, "user", 25_000);
  const tiles = queryKeys.nodeChildren.tiles(parent.id, "user");
  client.setQueryData(overview, listing);
  client.setQueryData<InfiniteData<NodeResponse, number>>(tiles, {
    pages: [listing],
    pageParams: [1],
  });
  addCachedFolder(client, "user", parent.id, child);
  addCachedFolder(client, "user", parent.id, child);
  const table = client.getQueryData<NodeResponse>(overview);
  expect(table?.content.nodes).toEqual([child]);
  expect(table?.totalCount).toBe(1);
  expect(table?.content.stats?.folders).toBe(1);
  expect(
    client.getQueryData<InfiniteData<NodeResponse, number>>(tiles)?.pages,
  ).toEqual([table]);
});

it("does not insert into incomplete, filtered or another account's listing", () => {
  const overview = queryKeys.nodeChildren.overview(parent.id, "user", 25_000);
  const other = queryKeys.nodeChildren.overview(parent.id, "other", 25_000);
  const filtered = [
    ...queryKeys.nodeChildren.all(parent.id),
    "user",
    "page",
    0,
    { name: "Other" },
  ];
  const partial = { ...listing, totalCount: 1 };
  client.setQueryData(overview, partial);
  client.setQueryData(other, listing);
  client.setQueryData(filtered, listing);
  addCachedFolder(client, "user", parent.id, child);
  expect(client.getQueryData(overview)).toEqual(partial);
  expect(client.getQueryData(other)).toEqual(listing);
  expect(client.getQueryData(filtered)).toEqual(listing);
});

it("does not append a new folder to already paginated tiles", () => {
  const key = queryKeys.nodeChildren.tiles(parent.id, "user");
  const data = { pages: [listing, listing], pageParams: [1, 2] };
  client.setQueryData(key, data);
  addCachedFolder(client, "user", parent.id, child);
  expect(client.getQueryData(key)).toEqual(data);
});
