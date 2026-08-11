import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { NodeDto } from "../../../shared/api/layoutsApi";

vi.mock("../../../shared/api/nodesApi", () => ({
  nodesApi: {
    updateNodeMetadata: vi.fn(),
  },
}));

vi.mock("../../../shared/utils/clientEncryptionFolderScan", () => ({
  collectFoldersInFoldersForClientEncryption: vi.fn(),
}));

import { nodesApi } from "../../../shared/api/nodesApi";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { collectFoldersInFoldersForClientEncryption } from "../../../shared/utils/clientEncryptionFolderScan";
import { useFolderEncryptionPolicy } from "./useFolderEncryptionPolicy";

const makeNode = (id: string, parentId: string | null = null): NodeDto => ({
  id,
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
  layoutId: "layout-1",
  parentId,
  name: id,
  metadata: {},
});

describe("useFolderEncryptionPolicy", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("updates the selected folder policy and every affected descendant", async () => {
    const parentNode = makeNode("node-1");
    const childNode = makeNode("node-2", "node-1");
    vi.mocked(collectFoldersInFoldersForClientEncryption).mockResolvedValue({
      folders: [childNode],
      files: [],
      scannedFolders: 1,
      truncated: false,
    });
    vi.mocked(nodesApi.updateNodeMetadata)
      .mockResolvedValueOnce({
        ...parentNode,
        metadata: { isClientEncryptionEnabled: "true" },
      })
      .mockResolvedValueOnce({
        ...childNode,
        metadata: { isClientEncryptionEnabled: "true" },
      });
    const updateNode = vi.spyOn(useNodesStore.getState(), "updateNode");
    const onToast = vi.fn();
    const { result } = renderHook(() =>
      useFolderEncryptionPolicy({ onToast }),
    );

    await act(async () => {
      await result.current.toggleFolderEncryptionPolicy("node-1", false);
    });

    expect(collectFoldersInFoldersForClientEncryption).toHaveBeenCalledWith([
      "node-1",
    ]);
    expect(nodesApi.updateNodeMetadata).toHaveBeenNthCalledWith(1, "node-1", {
      isClientEncryptionEnabled: "true",
    });
    expect(nodesApi.updateNodeMetadata).toHaveBeenNthCalledWith(2, "node-2", {
      isClientEncryptionEnabled: "true",
    });
    expect(updateNode).toHaveBeenCalledTimes(2);
    expect(onToast).toHaveBeenCalledWith(
      "clientEncryption.toasts.policyEnabled",
    );
  });

  it("reports failures without mutating the node store", async () => {
    vi.mocked(collectFoldersInFoldersForClientEncryption).mockRejectedValue(
      new Error("scan failed"),
    );
    const updateNode = vi.spyOn(useNodesStore.getState(), "updateNode");
    const onToast = vi.fn();
    const { result } = renderHook(() =>
      useFolderEncryptionPolicy({ onToast }),
    );

    await act(async () => {
      await result.current.toggleFolderEncryptionPolicy("node-1", false);
    });

    expect(updateNode).not.toHaveBeenCalled();
    expect(onToast).toHaveBeenCalledWith(
      "clientEncryption.toasts.policyToggleFailed",
      "error",
    );
  });
});
