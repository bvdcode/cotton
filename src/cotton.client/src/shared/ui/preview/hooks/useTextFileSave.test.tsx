import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { NodeFileManifestDto } from "../../../api/nodesApi";
import { ENCRYPTED_FLAG_KEY } from "../../../crypto";
import { uploadFileToNode } from "../../../upload/uploadFileToNode";
import { useTextFileSave } from "./useTextFileSave";

const mocks = vi.hoisted(() => ({
  serverSettings: {
    maxChunkSizeBytes: 1024,
    supportedHashAlgorithm: "sha256" as const,
  },
}));

vi.mock("../../../store/useServerSettings", () => ({
  useServerSettings: () => ({ data: mocks.serverSettings }),
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock("../../../upload/uploadFileToNode", () => ({
  uploadFileToNode: vi.fn(),
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
    name: "note.md",
    contentType: "text/markdown",
    sizeBytes: 12,
    metadata: {},
    ...overrides,
  };
}

describe("useTextFileSave", () => {
  beforeEach(() => {
    vi.mocked(uploadFileToNode).mockReset();
    vi.mocked(uploadFileToNode).mockResolvedValue(createFile());
  });

  it("re-encrypts encrypted text before replacing its content", async () => {
    const sourceFile = createFile({
      metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
    });
    const setOriginalContent = vi.fn();
    const onSaved = vi.fn();
    const { result } = renderHook(() =>
      useTextFileSave(
        sourceFile.id,
        sourceFile.name,
        "before",
        setOriginalContent,
        onSaved,
        sourceFile,
      ),
    );

    await act(async () => {
      await result.current.handleSave("after");
    });

    expect(uploadFileToNode).toHaveBeenCalledWith({
      file: expect.any(File),
      nodeId: sourceFile.nodeId,
      replaceNodeFileId: sourceFile.id,
      server: mocks.serverSettings,
      encrypt: true,
    });
    const uploadedFile = vi.mocked(uploadFileToNode).mock.calls[0]?.[0].file;
    expect(uploadedFile.name).toBe("note.md");
    expect(uploadedFile.type).toBe("text/markdown");
    await expect(uploadedFile.text()).resolves.toBe("after");
    expect(setOriginalContent).toHaveBeenCalledWith("after");
    expect(onSaved).toHaveBeenCalledOnce();
  });

  it("keeps ordinary text replacements unencrypted", async () => {
    const sourceFile = createFile();
    const { result } = renderHook(() =>
      useTextFileSave(
        sourceFile.id,
        sourceFile.name,
        "before",
        vi.fn(),
        undefined,
        sourceFile,
      ),
    );

    await act(async () => {
      await result.current.handleSave("after");
    });

    expect(uploadFileToNode).toHaveBeenCalledWith(
      expect.objectContaining({ encrypt: false }),
    );
  });

  it("allows clearing a text file", async () => {
    const sourceFile = createFile();
    const { result } = renderHook(() =>
      useTextFileSave(
        sourceFile.id,
        sourceFile.name,
        "before",
        vi.fn(),
        undefined,
        sourceFile,
      ),
    );

    await act(async () => {
      await result.current.handleSave("");
    });

    const uploadedFile = vi.mocked(uploadFileToNode).mock.calls[0]?.[0].file;
    await expect(uploadedFile.text()).resolves.toBe("");
  });
});
