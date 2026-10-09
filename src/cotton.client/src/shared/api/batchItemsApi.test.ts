import { afterEach, describe, expect, it, vi } from "vitest";
import { httpClient } from "./httpClient";
import {
  batchItemsApi,
  RESTORE_BATCH_SIZE,
  type BatchItem,
} from "./batchItemsApi";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("batchItemsApi", () => {
  it.each([false, true])(
    "submits all deletions in one request, skipTrash=%s",
    async (skipTrash) => {
      const items: BatchItem[] = Array.from({ length: 65 }, (_, index) => ({
        id: `file-${index}`,
        kind: "File",
      }));
      const results = items.map((item) => ({
        ...item,
        deleted: true,
        failed: false,
      }));
      const post = vi
        .spyOn(httpClient, "post")
        .mockResolvedValue({ data: results });

      expect(await batchItemsApi.delete(items, skipTrash)).toEqual(results);
      expect(post).toHaveBeenCalledOnce();
      expect(post).toHaveBeenCalledWith(
        "/items/delete",
        { items, skipTrash },
        { timeout: 0 },
      );
    },
  );

  it("allows a full restore batch to wait for a large folder", async () => {
    const items: BatchItem[] = Array.from(
      { length: RESTORE_BATCH_SIZE },
      (_, index) => ({
        id: `folder-${index}`,
        kind: "Folder",
      }),
    );
    const results = items.map((item) => ({
      ...item,
      deleted: false,
      failed: false,
      restoreOutcome: { status: "Restored" },
    }));
    const post = vi
      .spyOn(httpClient, "post")
      .mockResolvedValue({ data: results });

    expect(await batchItemsApi.restore(items)).toEqual(results);
    expect(post).toHaveBeenCalledWith(
      "/items/restore",
      { items },
      { timeout: 0 },
    );
  });

  it.each([0, RESTORE_BATCH_SIZE + 1])(
    "rejects %s restore items before sending",
    async (count) => {
      const post = vi.spyOn(httpClient, "post");
      const items: BatchItem[] = Array.from({ length: count }, (_, index) => ({
        id: `${index}`,
        kind: "File",
      }));
      await expect(batchItemsApi.restore(items)).rejects.toThrow(RangeError);
      expect(post).not.toHaveBeenCalled();
    },
  );
});
