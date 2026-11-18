import { afterEach, describe, expect, it, vi } from "vitest";
import { createFile } from "../../test/fileFixtures";
import { UploadListingUpdates } from "./UploadListingUpdates";

const mocks = vi.hoisted(() => ({
  refresh: vi.fn(async () => {}),
  upsert: vi.fn(),
}));
vi.mock("../store/nodesActions", () => ({ refreshNodeContent: mocks.refresh }));
vi.mock("../store/nodesStore", () => ({
  useNodesStore: { getState: () => ({ upsertFilesInCache: mocks.upsert }) },
}));
afterEach(() => {
  vi.useRealTimers();
  vi.clearAllMocks();
});

describe("upload listing updates", () => {
  it("patches each folder once per burst with the latest committed file", async () => {
    vi.useFakeTimers();
    const updates = new UploadListingUpdates();
    const first = createFile({ id: "first" });
    const second = createFile({ id: "second" });
    updates.enqueue("folder", { ...first, name: "old" });
    updates.enqueue("folder", first);
    updates.enqueue("folder", second);
    updates.enqueue("another-folder", undefined);
    expect(mocks.upsert).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(300);
    expect(mocks.upsert).toHaveBeenCalledExactlyOnceWith("folder", [
      first,
      second,
    ]);
    expect(mocks.refresh.mock.calls).toEqual([["folder"], ["another-folder"]]);
    updates.destroy();
  });

  it("releases queued DTOs and prevents late cache writes on disposal", async () => {
    vi.useFakeTimers();
    const updates = new UploadListingUpdates();
    updates.enqueue("folder", createFile());
    updates.destroy();
    await vi.advanceTimersByTimeAsync(1000);
    expect(mocks.upsert).not.toHaveBeenCalled();
    expect(mocks.refresh).not.toHaveBeenCalled();
  });
});
