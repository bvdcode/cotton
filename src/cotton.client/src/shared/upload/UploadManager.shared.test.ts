import { afterEach, describe, expect, it, vi } from "vitest";
import { createFile } from "../../test/fileFixtures";

const mocks = vi.hoisted(() => ({
  settings: vi.fn(() => {
    throw new Error("Private settings must not be read");
  }),
  quota: vi.fn(() => {
    throw new Error("Private quota must not be read");
  }),
  refresh: vi.fn(),
  upload: vi.fn(),
}));
vi.mock("../api/queries/serverSettings", () => ({
  getCachedServerSettings: mocks.settings,
}));
vi.mock("../api/storageQuotaApi", () => ({
  storageQuotaApi: { getCurrent: mocks.quota },
}));
vi.mock("../store/nodesActions", () => ({ refreshNodeContent: mocks.refresh }));
vi.mock("./uploadFileToNode", () => ({ uploadFileToNode: mocks.upload }));

import { UploadManager } from "./UploadManager";
import { useNodesStore } from "../store/nodesStore";

let manager: UploadManager | null = null;
afterEach(() => {
  manager?.destroy();
  manager = null;
  vi.clearAllMocks();
});

describe("public folder upload queue", () => {
  it("uses public parameters, reports completion, and avoids private quota and listing requests", async () => {
    manager = new UploadManager();
    const onUploaded = vi.fn();
    const access = {
      token: "public",
      sessionId: "session",
      server: { maxChunkSizeBytes: 1024, supportedHashAlgorithm: "sha256" },
    };
    const file = createFile({
      id: "public-upload",
      nodeId: "public-folder",
      name: "file.txt",
    });
    mocks.upload.mockResolvedValue(file);
    const before = useNodesStore.getState().contentByNodeId;
    manager.enqueue(
      [new File(["hello"], "file.txt")],
      "public-folder",
      "Uploads",
      { sharedUpload: access, onFileUploaded: onUploaded },
    );
    await vi.waitFor(() =>
      expect(manager?.getSnapshot().tasks[0]?.status).toBe("completed"),
    );
    expect(mocks.upload).toHaveBeenCalledWith(
      expect.objectContaining({ sharedUpload: access, server: access.server }),
    );
    expect(onUploaded).toHaveBeenCalledWith(file);
    await new Promise((resolve) => setTimeout(resolve, 350));
    expect(mocks.settings).not.toHaveBeenCalled();
    expect(mocks.quota).not.toHaveBeenCalled();
    expect(mocks.refresh).not.toHaveBeenCalled();
    expect(useNodesStore.getState().contentByNodeId).toBe(before);
  });
});
