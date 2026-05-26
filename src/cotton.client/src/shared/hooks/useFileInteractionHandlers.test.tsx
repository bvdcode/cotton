import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { NodeFileManifestDto } from "../api/nodesApi";
import {
  CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES,
  ClientEncryptionSizeLimitError,
} from "../crypto";
import { ENCRYPTED_FLAG_KEY } from "../crypto/fileCipher";
import { NoKeyError } from "../crypto/errors";
import { useFileInteractionHandlers } from "./useFileInteractionHandlers";

const mocks = vi.hoisted(() => ({
  downloadFile: vi.fn(),
  downloadReadableFile: vi.fn(),
  toastError: vi.fn(),
  openAudio: vi.fn(),
  openPreview: vi.fn(),
  closePreview: vi.fn(),
  openMediaLightbox: vi.fn(),
  setLightboxOpen: vi.fn(),
  mediaLightboxInputs: [] as Array<ReadonlyArray<{ id: string }>>,
  taskHandle: {
    id: "task-1",
    update: vi.fn(),
    complete: vi.fn(),
    fail: vi.fn(),
  },
  createTask: vi.fn(),
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

vi.mock("@shared/ui/notifications", () => ({
  toast: {
    error: mocks.toastError,
  },
}));

vi.mock("../utils/fileHandlers", () => ({
  downloadFile: mocks.downloadFile,
}));

vi.mock("../store/audioPlayerStore", () => ({
  useAudioPlayerStore: (
    selector: (state: { openFromSelection: typeof mocks.openAudio }) => unknown,
  ) => selector({ openFromSelection: mocks.openAudio }),
}));

vi.mock("./useFilePreview", () => ({
  useFilePreview: () => ({
    previewState: {
      isOpen: false,
      fileId: null,
      fileName: null,
      fileType: null,
      fileSizeBytes: undefined,
      file: null,
    },
    openPreview: mocks.openPreview,
    closePreview: mocks.closePreview,
  }),
}));

vi.mock("./useMediaLightbox", () => ({
  useMediaLightbox: (files: ReadonlyArray<{ id: string }>) => {
    mocks.mediaLightboxInputs.push(files);

    return {
      lightboxOpen: false,
      lightboxIndex: 0,
      mediaItems: [],
      getSignedMediaUrl: vi.fn(),
      getDownloadUrl: vi.fn(),
      handleMediaClick: mocks.openMediaLightbox,
      setLightboxOpen: mocks.setLightboxOpen,
      setLightboxIndex: vi.fn(),
    };
  },
}));

vi.mock("../crypto", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../crypto")>();

  return {
    ...actual,
    downloadReadableFile: mocks.downloadReadableFile,
  };
});

vi.mock("../tasks", () => ({
  taskManager: {
    createTask: mocks.createTask,
  },
}));

function createFile(
  overrides: Partial<NodeFileManifestDto> = {},
): NodeFileManifestDto {
  return {
    id: "file-1",
    createdAt: "2026-01-01T00:00:00.000Z",
    updatedAt: "2026-01-01T00:00:00.000Z",
    nodeId: "node-1",
    ownerId: "owner-1",
    name: "file.txt",
    contentType: "text/plain",
    sizeBytes: 12,
    metadata: {},
    ...overrides,
  };
}

describe("useFileInteractionHandlers", () => {
  beforeEach(() => {
    mocks.downloadFile.mockReset();
    mocks.downloadReadableFile.mockReset();
    mocks.downloadReadableFile.mockResolvedValue(undefined);
    mocks.toastError.mockReset();
    mocks.openAudio.mockReset();
    mocks.openPreview.mockReset();
    mocks.openPreview.mockReturnValue(true);
    mocks.closePreview.mockReset();
    mocks.openMediaLightbox.mockReset();
    mocks.setLightboxOpen.mockReset();
    mocks.mediaLightboxInputs = [];
    mocks.taskHandle.update.mockReset();
    mocks.taskHandle.complete.mockReset();
    mocks.taskHandle.fail.mockReset();
    mocks.createTask.mockReset();
    mocks.createTask.mockReturnValue(mocks.taskHandle);
  });

  it("keeps encrypted files out of inline media and downloads them from media clicks", () => {
    const plainImage = createFile({
      id: "plain-image",
      name: "photo.jpg",
      contentType: "image/jpeg",
    });
    const encryptedImage = createFile({
      id: "encrypted-image",
      name: "secret.png",
      contentType: "application/octet-stream",
      metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
    });

    const { result } = renderHook(() =>
      useFileInteractionHandlers({
        sortedFiles: [plainImage, encryptedImage],
      }),
    );

    expect(mocks.mediaLightboxInputs.at(-1)?.map((file) => file.id)).toEqual([
      "plain-image",
    ]);

    act(() => {
      result.current.handleMediaClick("encrypted-image");
    });

    expect(mocks.downloadReadableFile).toHaveBeenCalledWith(
      encryptedImage,
      undefined,
      expect.objectContaining({
        onDecryptProgress: expect.any(Function),
        onDecryptComplete: expect.any(Function),
      }),
    );
    expect(mocks.openMediaLightbox).not.toHaveBeenCalled();
  });

  it("preserves legacy direct downloads for plain files", async () => {
    const plainFile = createFile({ id: "plain-file", name: "plain.txt" });
    const encryptedFile = createFile({
      id: "encrypted-file",
      name: "secret.txt",
      metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
    });
    const { result } = renderHook(() =>
      useFileInteractionHandlers({
        sortedFiles: [plainFile, encryptedFile],
      }),
    );

    await result.current.handleDownloadFile("plain-file", "plain.txt");
    await result.current.handleDownloadFile("encrypted-file", "secret.txt");

    expect(mocks.downloadFile).toHaveBeenCalledWith("plain-file", "plain.txt");
    expect(mocks.downloadReadableFile).toHaveBeenCalledWith(
      encryptedFile,
      undefined,
      expect.objectContaining({
        onDecryptProgress: expect.any(Function),
        onDecryptComplete: expect.any(Function),
      }),
    );
  });

  it("publishes decrypt progress to the task manager", async () => {
    const encryptedFile = createFile({
      id: "encrypted-file",
      name: "secret.txt",
      sizeBytes: 12,
      metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
    });
    mocks.downloadReadableFile.mockImplementationOnce(
      async (_file, _fileName, callbacks) => {
        callbacks?.onDecryptProgress?.(4, 12);
        callbacks?.onDecryptComplete?.();
      },
    );
    const { result } = renderHook(() =>
      useFileInteractionHandlers({
        sortedFiles: [encryptedFile],
      }),
    );

    await result.current.handleDownloadFile("encrypted-file", "secret.txt");

    expect(mocks.createTask).toHaveBeenCalledWith({
      kind: "decrypt",
      label: "secret.txt",
      scopeLabel: "",
      bytesTotal: 12,
    });
    expect(mocks.taskHandle.update).toHaveBeenCalledWith({
      status: "running",
      bytesTotal: 12,
      bytesCompleted: 4,
    });
    expect(mocks.taskHandle.complete).toHaveBeenCalledOnce();
  });

  it("does not create a decrypt task when validation fails before decrypting", async () => {
    const encryptedFile = createFile({
      id: "encrypted-file",
      name: "secret.txt",
      metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
    });
    mocks.downloadReadableFile.mockRejectedValueOnce(
      new NoKeyError("Vault is locked."),
    );
    const { result } = renderHook(() =>
      useFileInteractionHandlers({
        sortedFiles: [encryptedFile],
      }),
    );

    await result.current.handleDownloadFile("encrypted-file", "secret.txt");

    expect(mocks.createTask).not.toHaveBeenCalled();
  });

  it("opens encrypted text files in the preview instead of downloading immediately", () => {
    const encryptedText = createFile({
      id: "encrypted-text",
      name: "secret.txt",
      contentType: "text/plain",
      metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
    });
    const { result } = renderHook(() =>
      useFileInteractionHandlers({
        sortedFiles: [encryptedText],
      }),
    );

    act(() => {
      result.current.handleFileClick("encrypted-text", "secret.txt", 4);
    });

    expect(mocks.openPreview).toHaveBeenCalledWith(
      "encrypted-text",
      "secret.txt",
      4,
      "text/plain",
      encryptedText,
    );
    expect(mocks.downloadReadableFile).not.toHaveBeenCalled();
  });

  it("downloads encrypted audio clicks instead of adding ciphertext to the player", () => {
    const encryptedAudio = createFile({
      id: "encrypted-audio",
      name: "song.mp3",
      contentType: "audio/mpeg",
      metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
    });
    const { result } = renderHook(() =>
      useFileInteractionHandlers({
        sortedFiles: [encryptedAudio],
      }),
    );

    act(() => {
      result.current.handleFileClick("encrypted-audio", "song.mp3", 4);
    });

    expect(mocks.downloadReadableFile).toHaveBeenCalledWith(
      encryptedAudio,
      undefined,
      expect.objectContaining({
        onDecryptProgress: expect.any(Function),
        onDecryptComplete: expect.any(Function),
      }),
    );
    expect(mocks.openAudio).not.toHaveBeenCalled();
    expect(mocks.openPreview).not.toHaveBeenCalled();
  });

  it("shows a targeted unlock message when an encrypted download has no key", async () => {
    const encryptedFile = createFile({
      id: "encrypted-file",
      name: "secret.txt",
      metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
    });
    mocks.downloadReadableFile.mockRejectedValueOnce(
      new NoKeyError("Vault is locked."),
    );
    const { result } = renderHook(() =>
      useFileInteractionHandlers({
        sortedFiles: [encryptedFile],
      }),
    );

    await result.current.handleDownloadFile("encrypted-file", "secret.txt");

    expect(mocks.toastError).toHaveBeenCalledWith(
      "common:clientEncryption.vaultLockedForDownload",
    );
  });

  it("shows a targeted size message for oversized encrypted downloads", async () => {
    const encryptedFile = createFile({
      id: "encrypted-file",
      name: "huge.bin",
      metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
    });
    mocks.downloadReadableFile.mockRejectedValueOnce(
      new ClientEncryptionSizeLimitError(
        "decrypt",
        CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES + 1,
        CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES,
      ),
    );
    const { result } = renderHook(() =>
      useFileInteractionHandlers({
        sortedFiles: [encryptedFile],
      }),
    );

    await result.current.handleDownloadFile("encrypted-file", "huge.bin");

    expect(mocks.toastError).toHaveBeenCalledWith(
      "common:clientEncryption.fileTooLargeForDownload",
    );
  });
});
