import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { filesApi } from "../api/filesApi";
import type { NodeFileManifestDto } from "../api/nodesApi";
import {
  ENCRYPTED_FLAG_KEY,
  ORIGINAL_CONTENT_TYPE_KEY,
  encryptFileToBlob,
} from "./fileCipher";
import { NoKeyError } from "./errors";
import { generateMasterKey } from "./keys";
import { downloadReadableFile, getReadableFileUrl } from "./downloadDecrypt";
import { DISPLAY_META_KEY, encryptDisplayMeta } from "./displayMeta";
import { useVault } from "./vault";

vi.mock("../api/filesApi", () => ({
  filesApi: {
    getDownloadLink: vi.fn(),
  },
}));

const getDownloadLinkMock = vi.mocked(filesApi.getDownloadLink);
const originalCreateObjectURL = URL.createObjectURL;
const originalRevokeObjectURL = URL.revokeObjectURL;

function createFile(
  overrides: Partial<NodeFileManifestDto> = {},
): NodeFileManifestDto {
  return {
    id: "file-1",
    createdAt: "2026-01-01T00:00:00.000Z",
    updatedAt: "2026-01-01T00:00:00.000Z",
    nodeId: "node-1",
    ownerId: "owner-1",
    name: "report.txt",
    contentType: "text/plain",
    sizeBytes: 12,
    metadata: {},
    ...overrides,
  };
}

function readBlobBytes(blob: Blob): Promise<number[]> {
  return blob.arrayBuffer().then((buffer) => Array.from(new Uint8Array(buffer)));
}

describe("downloadDecrypt", () => {
  beforeEach(() => {
    useVault.getState().lock();
    getDownloadLinkMock.mockReset();
    vi.stubGlobal("fetch", vi.fn());
    Object.defineProperty(URL, "createObjectURL", {
      configurable: true,
      value: vi.fn(() => "blob:decrypted"),
    });
    Object.defineProperty(URL, "revokeObjectURL", {
      configurable: true,
      value: vi.fn(),
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    Object.defineProperty(URL, "createObjectURL", {
      configurable: true,
      value: originalCreateObjectURL,
    });
    Object.defineProperty(URL, "revokeObjectURL", {
      configurable: true,
      value: originalRevokeObjectURL,
    });
    useVault.getState().lock();
  });

  it("returns the signed URL for a plain file", async () => {
    getDownloadLinkMock.mockResolvedValue("https://files.example/download");

    const handle = await getReadableFileUrl(createFile(), 30);

    expect(handle).toEqual({
      url: "https://files.example/download",
      mimeType: "text/plain",
      revoke: expect.any(Function),
    });
    expect(getDownloadLinkMock).toHaveBeenCalledWith("file-1", 30);
    expect(fetch).not.toHaveBeenCalled();

    handle.revoke();
    expect(URL.revokeObjectURL).not.toHaveBeenCalled();
  });

  it("requires an unlocked vault before loading encrypted bytes", async () => {
    const file = createFile({
      metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
    });

    await expect(getReadableFileUrl(file)).rejects.toBeInstanceOf(NoKeyError);

    expect(getDownloadLinkMock).not.toHaveBeenCalled();
    expect(fetch).not.toHaveBeenCalled();
  });

  it("decrypts an encrypted file into a revocable blob URL", async () => {
    const masterKey = await generateMasterKey();
    const plaintext = new Uint8Array([1, 2, 3, 4]);
    const encrypted = await encryptFileToBlob(
      new Blob([plaintext as BlobPart], { type: "image/png" }),
      masterKey,
      4096,
    );
    const file = createFile({
      contentType: "application/octet-stream",
      metadata: {
        [ENCRYPTED_FLAG_KEY]: "true",
        [ORIGINAL_CONTENT_TYPE_KEY]: "image/png",
      },
    });
    const fetchMock = vi.mocked(fetch);

    useVault.getState().unlock(masterKey);
    getDownloadLinkMock.mockResolvedValue("https://files.example/encrypted");
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: "OK",
      blob: async () => encrypted,
    } as Response);

    const handle = await getReadableFileUrl(file);

    expect(handle.url).toBe("blob:decrypted");
    expect(handle.mimeType).toBe("image/png");
    expect(getDownloadLinkMock).toHaveBeenCalledWith("file-1", undefined);
    expect(fetchMock).toHaveBeenCalledWith("https://files.example/encrypted");

    const decryptedBlob = vi.mocked(URL.createObjectURL).mock.calls[0]?.[0] as Blob;
    expect(decryptedBlob.type).toBe("image/png");
    await expect(readBlobBytes(decryptedBlob)).resolves.toEqual([1, 2, 3, 4]);

    handle.revoke();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:decrypted");
  });

  it("uses encrypted display metadata for the decrypted blob content type", async () => {
    const masterKey = await generateMasterKey();
    const plaintext = new Uint8Array([5, 6, 7, 8]);
    const encrypted = await encryptFileToBlob(
      new Blob([plaintext as BlobPart], { type: "application/pdf" }),
      masterKey,
      4096,
    );
    const fetchMock = vi.mocked(fetch);

    useVault.getState().unlock(masterKey);
    const displayMeta = await encryptDisplayMeta({
      name: "private.pdf",
      contentType: "application/pdf",
    });
    const file = createFile({
      contentType: "application/octet-stream",
      metadata: {
        [ENCRYPTED_FLAG_KEY]: "true",
        [DISPLAY_META_KEY]: displayMeta,
      },
    });

    getDownloadLinkMock.mockResolvedValue("https://files.example/encrypted");
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: "OK",
      blob: async () => encrypted,
    } as Response);

    await getReadableFileUrl(file);

    const decryptedBlob = vi.mocked(URL.createObjectURL).mock.calls[0]?.[0] as Blob;
    expect(decryptedBlob.type).toBe("application/pdf");
  });

  it("uses encrypted display metadata for stale encrypted download names", async () => {
    const masterKey = await generateMasterKey();
    const encrypted = await encryptFileToBlob(
      new Blob([new Uint8Array([9, 10]) as BlobPart], {
        type: "application/pdf",
      }),
      masterKey,
      4096,
    );
    const fetchMock = vi.mocked(fetch);
    let clickedDownloadName: string | null = null;

    vi.useFakeTimers();
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(
      function click(this: HTMLAnchorElement) {
        clickedDownloadName = this.download;
      },
    );
    useVault.getState().unlock(masterKey);
    const displayMeta = await encryptDisplayMeta({
      name: "private.pdf",
      contentType: "application/pdf",
    });
    const file = createFile({
      name: "11111111-2222-4333-8444-555555555555",
      contentType: "application/octet-stream",
      metadata: {
        [ENCRYPTED_FLAG_KEY]: "true",
        [DISPLAY_META_KEY]: displayMeta,
      },
    });

    getDownloadLinkMock.mockResolvedValue("https://files.example/encrypted");
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      statusText: "OK",
      blob: async () => encrypted,
    } as Response);

    await downloadReadableFile(file);
    vi.runOnlyPendingTimers();
    vi.useRealTimers();

    expect(clickedDownloadName).toBe("private.pdf");
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:decrypted");
  });

  it("rejects failed encrypted file fetches", async () => {
    const masterKey = await generateMasterKey();
    const file = createFile({
      metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
    });

    useVault.getState().unlock(masterKey);
    getDownloadLinkMock.mockResolvedValue("https://files.example/encrypted");
    vi.mocked(fetch).mockResolvedValue({
      ok: false,
      status: 403,
      statusText: "Forbidden",
    } as Response);

    await expect(getReadableFileUrl(file)).rejects.toThrow(
      "Failed to fetch encrypted file: 403 Forbidden",
    );

    expect(URL.createObjectURL).not.toHaveBeenCalled();
  });
});
