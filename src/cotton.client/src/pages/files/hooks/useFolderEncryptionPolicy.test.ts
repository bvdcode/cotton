import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { NodeDto } from "../../../shared/api/layoutsApi";

vi.mock("../../../shared/api/nodesApi", () => ({
  nodesApi: {
    bulkUpdateNodeMetadata: vi.fn(),
  },
}));

import { nodesApi } from "../../../shared/api/nodesApi";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { useFolderEncryptionPolicy } from "./useFolderEncryptionPolicy";
import { queryClient } from "@shared/api/queries/queryClient";
import { folderOverviewOptions } from "@shared/api/queries/folderOverview";
import type { NodeResponse } from "@shared/api/nodesApi";

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
    queryClient.clear();
    useNodesStore.getState().reset(null);
    vi.restoreAllMocks();
  });

  it("updates cached folder metadata without replacing its children or another account's data", async () => {
    const node = makeNode("folder");
    const content = {
      ...node,
      node,
      nodes: [makeNode("child", node.id)],
      files: [],
    };
    const page: NodeResponse = { content, totalCount: 1 };
    const ownKey = folderOverviewOptions(node.id, "owner").queryKey;
    const otherKey = folderOverviewOptions(node.id, "other").queryKey;
    queryClient.setQueryData(ownKey, page);
    queryClient.setQueryData(otherKey, page);
    useNodesStore.setState({ cacheOwnerUserId: "owner" });
    const updated = {
      ...node,
      metadata: { isClientEncryptionEnabled: "true" },
    };
    vi.mocked(nodesApi.bulkUpdateNodeMetadata).mockResolvedValue([updated]);
    const { result } = renderHook(() =>
      useFolderEncryptionPolicy({ onToast: vi.fn() }),
    );
    await act(async () => {
      await result.current.toggleFolderEncryptionPolicy(node.id, false);
    });
    expect(
      queryClient.getQueryData<NodeResponse>(ownKey)?.content.node,
    ).toEqual(updated);
    expect(queryClient.getQueryData<NodeResponse>(ownKey)?.content.nodes).toBe(
      content.nodes,
    );
    expect(queryClient.getQueryData(otherKey)).toEqual(page);
    expect(queryClient.getQueryCache().getAll()).toHaveLength(2);
  });

  it("updates the selected folder policy and every affected descendant", async () => {
    const parentNode = makeNode("node-1");
    const childNode = makeNode("node-2", "node-1");
    vi.mocked(nodesApi.bulkUpdateNodeMetadata).mockResolvedValue([
      {
        ...parentNode,
        metadata: { isClientEncryptionEnabled: "true" },
      },
      {
        ...childNode,
        metadata: { isClientEncryptionEnabled: "true" },
      },
    ]);
    const updateNode = vi.spyOn(useNodesStore.getState(), "updateNode");
    const onToast = vi.fn();
    const { result } = renderHook(() => useFolderEncryptionPolicy({ onToast }));

    await act(async () => {
      await result.current.toggleFolderEncryptionPolicy("node-1", false);
    });

    expect(nodesApi.bulkUpdateNodeMetadata).toHaveBeenCalledExactlyOnceWith(
      "node-1",
      { isClientEncryptionEnabled: "true" },
      true,
    );
    expect(updateNode).toHaveBeenCalledTimes(2);
    expect(onToast).toHaveBeenCalledWith(
      "clientEncryption.toasts.policyEnabled",
    );
  });

  it("reports failures without mutating the node store", async () => {
    vi.mocked(nodesApi.bulkUpdateNodeMetadata).mockRejectedValue(
      new Error("update failed"),
    );
    const updateNode = vi.spyOn(useNodesStore.getState(), "updateNode");
    const onToast = vi.fn();
    const { result } = renderHook(() => useFolderEncryptionPolicy({ onToast }));

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
