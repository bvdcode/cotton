import { QueryClient } from "@tanstack/react-query";
import { afterEach, expect, it, vi } from "vitest";
import { nodesApi } from "@shared/api/nodesApi";
import { queryKeys } from "@shared/api/queries/queryKeys";
import { calculateFolderSizes } from "./useFolderSizes";

afterEach(() => vi.restoreAllMocks());
it.each([0, 1])(
  "requests visible trash folder sizes in one batch at depth %i",
  async (depth) => {
    const get = vi.spyOn(nodesApi, "getChildFolderStats").mockResolvedValue({
      deleted: { sizeBytes: 42, folders: 1, files: 2 },
    });
    const children = vi.spyOn(nodesApi, "getChildren");
    const client = new QueryClient();
    const signal = new AbortController().signal;
    await expect(
      calculateFolderSizes(client, "trash", "user", signal, {
        nodeType: "trash",
        depth,
      }),
    ).resolves.toBe(42);
    expect(get).toHaveBeenCalledExactlyOnceWith("trash", {
      nodeType: "trash",
      depth,
      signal,
    });
    expect(children).not.toHaveBeenCalled();
    expect(
      client.getQueryData(queryKeys.nodeChildren.folderInfo("deleted", "user")),
    ).toEqual({ sizeBytes: 42, folders: 1, files: 2 });
    client.clear();
  },
);
