import { renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { NodeFileManifestDto } from "../../../api/nodesApi";
import { useTextFileContent } from "./useTextFileContent";

const mocks = vi.hoisted(() => ({
  getDownloadLink: vi.fn(),
  getReadableFileUrl: vi.fn(),
  isFileEncrypted: vi.fn(),
  revoke: vi.fn(),
  t: vi.fn((key: string, params?: Record<string, unknown>) =>
    params ? `${key}:${JSON.stringify(params)}` : key,
  ),
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: mocks.t,
  }),
}));

vi.mock("../../../api/filesApi", () => ({
  filesApi: {
    getDownloadLink: mocks.getDownloadLink,
  },
}));

vi.mock("../../../crypto", () => ({
  CLIENT_ENCRYPTION_BLOB_PIPELINE_MAX_BYTES: 512 * 1024 * 1024,
  getReadableFileUrl: mocks.getReadableFileUrl,
  isFileEncrypted: mocks.isFileEncrypted,
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
    name: "secret.txt",
    contentType: "text/plain",
    sizeBytes: 12,
    metadata: {},
    ...overrides,
  };
}

describe("useTextFileContent", () => {
  beforeEach(() => {
    mocks.getDownloadLink.mockReset();
    mocks.getReadableFileUrl.mockReset();
    mocks.isFileEncrypted.mockReset();
    mocks.revoke.mockReset();
    mocks.t.mockClear();
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response("hello", { status: 200 })),
    );
  });

  it("loads plain text through a normal signed download link", async () => {
    mocks.getDownloadLink.mockResolvedValue("https://example.test/plain.txt");
    mocks.isFileEncrypted.mockReturnValue(false);

    const { result } = renderHook(() => useTextFileContent("file-1", 5));

    await waitFor(() => expect(result.current.loading).toBe(false));

    expect(result.current.content).toBe("hello");
    expect(mocks.getDownloadLink).toHaveBeenCalledWith("file-1");
    expect(mocks.getReadableFileUrl).not.toHaveBeenCalled();
  });

  it("loads encrypted text through the readable-file decrypt pipeline", async () => {
    const sourceFile = createFile({ metadata: { encrypted: "true" } });
    mocks.isFileEncrypted.mockReturnValue(true);
    mocks.getReadableFileUrl.mockResolvedValue({
      url: "blob:decrypted-text",
      mimeType: "text/plain",
      revoke: mocks.revoke,
    });

    const { result } = renderHook(() =>
      useTextFileContent(sourceFile.id, sourceFile.sizeBytes, sourceFile),
    );

    await waitFor(() => expect(result.current.loading).toBe(false));

    expect(result.current.content).toBe("hello");
    expect(mocks.getReadableFileUrl).toHaveBeenCalledWith(sourceFile);
    expect(mocks.getDownloadLink).not.toHaveBeenCalled();
    expect(mocks.revoke).toHaveBeenCalledOnce();
  });
});
