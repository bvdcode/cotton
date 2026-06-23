import { beforeEach, describe, expect, it, vi } from "vitest";
import { ClientEncryptionSizeLimitError } from "../crypto";

vi.mock("../upload/encryptExistingFileInPlace", () => ({
  encryptExistingFileInPlace: vi.fn(),
}));

vi.mock("../upload/decryptExistingFileInPlace", () => ({
  decryptExistingFileInPlace: vi.fn(),
}));

vi.mock("./taskManager", () => ({
  taskManager: {
    createTask: vi.fn(),
  },
}));

import { encryptExistingFileInPlace } from "../upload/encryptExistingFileInPlace";
import { decryptExistingFileInPlace } from "../upload/decryptExistingFileInPlace";
import { taskManager } from "./taskManager";
import {
  decryptExistingFileWithTask,
  encryptExistingFileWithTask,
} from "./encryptExistingFileTask";

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
    vi.mocked(encryptExistingFileInPlace).mockImplementation(
      async (options) => {
        options.onEncryptProgress?.(40, 100);
        options.onUploadProgress?.(20, 130);
        options.onFinalizing?.();
      },
    );

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

describe("decryptExistingFileWithTask", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(taskManager.createTask).mockReturnValue({
      id: "task-1",
      update,
      complete,
      fail,
    });
  });

  it("wraps existing-file decryption in a visible task", async () => {
    const encryptedFile = {
      ...file,
      name: "opaque-id",
      contentType: "application/octet-stream",
      metadata: { encrypted: "true" },
    };
    vi.mocked(decryptExistingFileInPlace).mockImplementation(
      async (options) => {
        options.onDecryptProgress?.(50, 100);
        options.onUploadProgress?.(30, 90);
        options.onFinalizing?.();
      },
    );

    await decryptExistingFileWithTask({
      file: encryptedFile,
      targetNodeId: "node-1",
      scopeLabel: "Plain",
      server,
    });

    expect(taskManager.createTask).toHaveBeenCalledWith({
      kind: "decrypt",
      label: "opaque-id",
      scopeLabel: "Plain",
      bytesTotal: 100,
    });
    expect(decryptExistingFileInPlace).toHaveBeenCalledWith(
      expect.objectContaining({
        file: encryptedFile,
        targetNodeId: "node-1",
        server,
      }),
    );
    expect(update).toHaveBeenCalledWith({ status: "running" });
    expect(update).toHaveBeenCalledWith({
      status: "running",
      bytesTotal: 100,
      bytesCompleted: 50,
    });
    expect(update).toHaveBeenCalledWith({
      status: "running",
      bytesTotal: 190,
      bytesCompleted: 130,
    });
    expect(update).toHaveBeenCalledWith({ status: "finalizing" });
    expect(complete).toHaveBeenCalled();
    expect(fail).not.toHaveBeenCalled();
  });

  it("marks the task as failed when decryption is blocked by the blob limit", async () => {
    vi.mocked(decryptExistingFileInPlace).mockRejectedValue(
      new ClientEncryptionSizeLimitError("decrypt", 200, 100),
    );

    await expect(
      decryptExistingFileWithTask({
        file: { ...file, metadata: {} },
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
