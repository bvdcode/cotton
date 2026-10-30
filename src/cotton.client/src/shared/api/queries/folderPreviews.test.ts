import { QueryClient, type InfiniteData } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import { createFile, createFolder } from "../../../test/fileFixtures";
import type { NodeResponse } from "../nodesApi";
import { queryKeys } from "./queryKeys";
import { updateFolderFilePreview } from "./folderPreviews";

const page = (fileId: string): NodeResponse => ({
  content: {
    ...createFolder(),
    nodes: [],
    files: [createFile({ id: fileId })],
  },
  totalCount: 2,
});

describe("folder preview updates", () => {
  it("updates every loaded copy and preserves other pages, totals and statistics", () => {
    const client = new QueryClient();
    const overviewKey = queryKeys.nodeChildren.overview(
      "folder-1",
      "user-1",
      1000,
    );
    const pageKey = queryKeys.nodeChildren.page("folder-1", "user-1", 2);
    const tilesKey = queryKeys.nodeChildren.tiles("folder-1", "user-1");
    const statsKey = queryKeys.nodeChildren.folderInfo("folder-1", "user-1");
    const otherKey = queryKeys.nodeChildren.overview("other", "user-1", 1000);
    client.setQueryData(overviewKey, page("target"));
    client.setQueryData(pageKey, page("target"));
    client.setQueryData<InfiniteData<NodeResponse, number>>(tilesKey, {
      pages: [page("first"), page("target")],
      pageParams: [1, 2],
    });
    client.setQueryData(statsKey, { files: 2, folders: 0, sizeBytes: 2 });
    client.setQueryData(otherKey, page("target"));
    const before = client.getQueryData<InfiniteData<NodeResponse>>(tilesKey);
    const stats = client.getQueryData(statsKey);
    const other = client.getQueryData(otherKey);

    updateFolderFilePreview(client, "folder-1", "target", "ready");

    expect(
      client.getQueryData<NodeResponse>(overviewKey)?.content.files[0]
        .previewHashEncryptedHex,
    ).toBe("ready");
    expect(
      client.getQueryData<NodeResponse>(pageKey)?.content.files[0]
        .previewHashEncryptedHex,
    ).toBe("ready");
    const after = client.getQueryData<InfiniteData<NodeResponse>>(tilesKey);
    expect(after?.pages[1].content.files[0].previewHashEncryptedHex).toBe(
      "ready",
    );
    expect(after?.pages[0]).toBe(before?.pages[0]);
    expect(after?.pages[1].content.nodes).toBe(before?.pages[1].content.nodes);
    expect(after?.pages[1].totalCount).toBe(2);
    expect(after?.pageParams).toBe(before?.pageParams);
    expect(client.getQueryData(statsKey)).toBe(stats);
    expect(client.getQueryData(otherKey)).toBe(other);
    client.clear();
  });

  it("keeps references for off-page files and repeated events without invalidating queries", () => {
    const client = new QueryClient();
    const key = queryKeys.nodeChildren.tiles("folder-1", "user-1");
    client.setQueryData(key, { pages: [page("target")], pageParams: [1] });
    const before = client.getQueryData(key);
    updateFolderFilePreview(client, "folder-1", "missing", "ready");
    expect(client.getQueryData(key)).toBe(before);
    updateFolderFilePreview(client, "folder-1", "target", "ready");
    const updated = client.getQueryData(key);
    updateFolderFilePreview(client, "folder-1", "target", "ready");
    expect(client.getQueryData(key)).toBe(updated);
    expect(client.getQueryState(key)?.isInvalidated).toBe(false);
    expect(client.getQueryCache().getAll()).toHaveLength(1);
    client.clear();
  });
});
