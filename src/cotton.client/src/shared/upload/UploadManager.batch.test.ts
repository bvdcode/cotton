import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { createFile } from "../../test/fileFixtures";
import { queryClient } from "../api/queries/queryClient";
import { UploadManager } from "./UploadManager";

const mocks = vi.hoisted(() => ({ quota: vi.fn(), upload: vi.fn() }));
vi.mock("../api/queries/serverSettings", () => ({
  getCachedServerSettings: () => ({
    maxChunkSizeBytes: 1024,
    supportedHashAlgorithm: "sha256",
  }),
}));
vi.mock("../api/storageQuotaApi", () => ({
  storageQuotaApi: { getCurrent: mocks.quota },
}));
vi.mock("../store/nodesActions", () => ({
  refreshNodeContent: () => Promise.resolve(),
}));
vi.mock("./uploadFileToNode", () => ({ uploadFileToNode: mocks.upload }));

let manager: UploadManager;
beforeEach(() => {
  queryClient.clear();
  manager = new UploadManager();
});
afterEach(() => {
  manager.destroy();
  queryClient.clear();
  vi.resetAllMocks();
});

it("publishes thousands of folder groups in one snapshot with correct totals and ordering", () => {
  mocks.quota.mockReturnValue(new Promise(() => {}));
  const notify = vi.fn();
  manager.subscribe(notify);
  const file = new File(["data"], "file.txt");
  const batches = Array.from({ length: 5000 }, (_, i) => ({
    files: [file],
    nodeId: `folder-${i}`,
    nodeLabel: `Files / Folder ${i}`,
  }));

  const ids = manager.enqueueBatch(batches);

  expect(ids).toHaveLength(batches.length);
  expect(new Set(ids).size).toBe(ids.length);
  expect(notify).toHaveBeenCalledOnce();
  expect(manager.getSnapshot().overall.bytesTotal).toBe(
    file.size * batches.length,
  );
  expect(manager.getSnapshot().tasks.map((task) => task.scopeLabel)).toEqual(
    batches.map((batch) => batch.nodeLabel).reverse(),
  );
});

it("retains per-folder encryption, replacement and completion options", async () => {
  mocks.quota.mockResolvedValue({
    usedBytes: 0,
    quotaBytes: null,
    availableBytes: null,
  });
  const uploaded = createFile();
  mocks.upload.mockResolvedValue(uploaded);
  const firstDone = vi.fn();
  const secondDone = vi.fn();
  const first = new File(["first"], "first.txt");
  const second = new File(["second"], "second.txt");
  manager.enqueueBatch([
    {
      files: [first],
      nodeId: "first",
      nodeLabel: "First",
      options: { encrypt: true, onFileUploaded: firstDone },
    },
    {
      files: [{ file: second, replaceNodeFileId: "replacement" }],
      nodeId: "second",
      nodeLabel: "Second",
      options: { onFileUploaded: secondDone },
    },
  ]);

  await vi.waitFor(() => expect(firstDone).toHaveBeenCalledWith(uploaded));
  expect(secondDone).toHaveBeenCalledWith(uploaded);
  expect(mocks.upload).toHaveBeenCalledWith(
    expect.objectContaining({ nodeId: "first", file: first, encrypt: true }),
  );
  expect(mocks.upload).toHaveBeenCalledWith(
    expect.objectContaining({
      nodeId: "second",
      file: second,
      replaceNodeFileId: "replacement",
      encrypt: false,
    }),
  );
});
