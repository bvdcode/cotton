import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { NodeFileManifestDto } from "../api/nodesApi";
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
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

vi.mock("react-toastify", () => ({
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
      "secret.png",
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
      "secret.txt",
    );
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
      "song.mp3",
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
});
