import { beforeEach, describe, expect, it, vi } from "vitest";
import { ClientEncryptionSizeLimitError } from "../crypto";

vi.mock("../upload/encryptExistingFileInPlace", () => ({
  encryptExistingFileInPlace: vi.fn(),
}));

vi.mock("./taskManager", () => ({
  taskManager: {
    createTask: vi.fn(),
  },
}));

import { encryptExistingFileInPlace } from "../upload/encryptExistingFileInPlace";
import { taskManager } from "./taskManager";
import { encryptExistingFileWithTask } from "./encryptExistingFileTask";

const update = vi.fn();
const complete = vi.fn();
const fail = vi.fn();

const file = {
  id: "file-1",
  name: "plain.txt",
  contentType: "text/plain",
  sizeBytes: 100,
};

const server = {
  maxChunkSizeBytes: 1024,
  supportedHashAlgorithm: "SHA-256",
};

describe("encryptExistingFileWithTask", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(taskManager.createTask).mockReturnValue({
      id: "task-1",
      update,
      complete,
      fail,
    });
  });

  it("wraps existing-file encryption in a visible task", async () => {
    vi.mocked(encryptExistingFileInPlace).mockImplementation(async (options) => {
      options.onEncryptProgress?.(40, 100);
      options.onUploadProgress?.(20, 130);
      options.onFinalizing?.();
    });

    await encryptExistingFileWithTask({
      file,
      targetNodeId: "node-1",
      scopeLabel: "Vault",
      server,
    });

    expect(taskManager.createTask).toHaveBeenCalledWith({
      kind: "encrypt",
      label: "plain.txt",
      scopeLabel: "Vault",
      bytesTotal: 100,
    });
    expect(encryptExistingFileInPlace).toHaveBeenCalledWith(
      expect.objectContaining({
        file,
        targetNodeId: "node-1",
        server,
      }),
    );
    expect(update).toHaveBeenCalledWith({ status: "running" });
    expect(update).toHaveBeenCalledWith({
      status: "running",
      bytesTotal: 100,
      bytesCompleted: 40,
    });
    expect(update).toHaveBeenCalledWith({
      status: "running",
      bytesTotal: 230,
      bytesCompleted: 120,
    });
    expect(update).toHaveBeenCalledWith({ status: "finalizing" });
    expect(complete).toHaveBeenCalled();
    expect(fail).not.toHaveBeenCalled();
  });

  it("marks the task as failed when encryption is blocked by the blob limit", async () => {
    vi.mocked(encryptExistingFileInPlace).mockRejectedValue(
      new ClientEncryptionSizeLimitError("encrypt", 200, 100),
    );

    await expect(
      encryptExistingFileWithTask({
        file,
        targetNodeId: "node-1",
        server,
      }),
    ).rejects.toBeInstanceOf(ClientEncryptionSizeLimitError);

    expect(fail).toHaveBeenCalledWith({
      message: expect.any(String),
      key: "clientEncryptionFileTooLarge",
      params: { maxSize: "100 B" },
    });
    expect(complete).not.toHaveBeenCalled();
  });
});
