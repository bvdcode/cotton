import { QueryClientProvider } from "@tanstack/react-query";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { createFolder } from "../../../test/fileFixtures";
import { InterfaceLayoutType, type NodeDto } from "@shared/api/layoutsApi";
import { nodesApi, type NodeResponse } from "@shared/api/nodesApi";
import { queryClient } from "@shared/api/queries/queryClient";
import { folderOverviewOptions } from "@shared/api/queries/folderOverview";
import { useAuthStore } from "@shared/store/authStore";
import { useNodesStore } from "@shared/store/nodesStore";
import { loadNode } from "@shared/store/nodesActions";
import { useVault } from "@shared/crypto";
import { uploadManager } from "@shared/upload/UploadManager";
import { useFilesData } from "./useFilesData";
import { useFileUpload } from "./useFileUpload";

vi.mock("@shared/upload/UploadManager", () => ({
  uploadManager: { enqueue: vi.fn() },
}));
vi.mock("../utils/lookupUploadNames", () => ({
  lookupUploadNames: vi.fn(async () => ({
    nodes: [],
    files: [],
    takenNameKeys: [],
  })),
}));

const parent = createFolder({ id: "parent" });
const oldFolder = createFolder({ id: "child", parentId: parent.id });
const encryptedFolder = createFolder({
  ...oldFolder,
  updatedAt: "2026-05-18T00:00:00Z",
  metadata: { isClientEncryptionEnabled: "true" },
});
const response = (node: NodeDto): NodeResponse => ({
  content: {
    ...node,
    node,
    nodes: [],
    files: [],
    stats: { files: 0, folders: 0, sizeBytes: 0 },
  },
  totalCount: 0,
});
const wrapper = ({ children }: PropsWithChildren) => (
  <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
);
const files = [new File(["content"], "document.txt")];

function deferredResponse() {
  let resolve!: (value: NodeResponse) => void;
  let reject!: (error: Error) => void;
  const promise = new Promise<NodeResponse>((onResolve, onReject) => {
    resolve = onResolve;
    reject = onReject;
  });
  return { promise, resolve, reject };
}

beforeEach(() => {
  useAuthStore.setState({
    user: {
      id: "user",
      username: "user",
      role: 1,
      createdAt: "",
      updatedAt: "",
    },
  });
  useNodesStore.setState({
    cacheOwnerUserId: "user",
    currentNode: parent,
    rootNodeId: parent.id,
    ancestors: [],
    ancestorsByNodeId: {},
    contentByNodeId: {},
    loading: false,
    error: null,
  });
  useVault.setState({ isUnlocked: true });
  queryClient.setQueryDefaults(["nodeChildren"], { retry: false });
  queryClient.setQueryData(folderOverviewOptions(parent.id, "user").queryKey, {
    ...response(parent),
    content: { ...response(parent).content, nodes: [oldFolder] },
  });
  vi.spyOn(nodesApi, "getNode").mockResolvedValue(encryptedFolder);
  vi.spyOn(nodesApi, "getAncestors").mockResolvedValue([parent]);
});
afterEach(() => {
  cleanup();
  queryClient.clear();
  useNodesStore.getState().reset(null);
  useAuthStore.setState({ user: null });
  useVault.setState({ isUnlocked: false });
  vi.restoreAllMocks();
  vi.clearAllMocks();
});

it.each([InterfaceLayoutType.List, InterfaceLayoutType.Tiles])(
  "waits for the navigation listing and encrypts an immediate upload in layout %s",
  async (layoutType) => {
    const request = deferredResponse();
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockReturnValue(request.promise);
    queryClient.setQueryData(
      folderOverviewOptions(oldFolder.id, "user").queryKey,
      response(oldFolder),
      { updatedAt: 1 },
    );
    const { result } = renderHook(
      () => ({
        data: useFilesData({ nodeId: oldFolder.id, layoutType, loadNode }),
        upload: useFileUpload(oldFolder.id, []),
      }),
      { wrapper },
    );
    await waitFor(() =>
      expect(useNodesStore.getState().currentNode?.id).toBe(oldFolder.id),
    );
    let upload: Promise<void>;
    act(() => {
      upload = result.current.upload.handleUploadFiles(files);
    });
    expect(uploadManager.enqueue).not.toHaveBeenCalled();
    await act(async () => {
      request.resolve(response(encryptedFolder));
      await upload;
    });
    await waitFor(() =>
      expect(useNodesStore.getState().currentNode).toEqual(encryptedFolder),
    );
    expect(uploadManager.enqueue).toHaveBeenCalledWith(
      [{ file: files[0] }],
      oldFolder.id,
      expect.any(String),
      expect.objectContaining({ encrypt: true }),
    );
    expect(get).toHaveBeenCalledTimes(1);
    expect(nodesApi.getNode).not.toHaveBeenCalled();
    expect(nodesApi.getAncestors).not.toHaveBeenCalled();
    await act(async () => {
      await result.current.upload.handleUploadFiles(files);
    });
    expect(get).toHaveBeenCalledTimes(1);
  },
);

it("uses an unencrypted folder's completed listing without another request", async () => {
  queryClient.setQueryData(
    folderOverviewOptions(oldFolder.id, "user").queryKey,
    response(oldFolder),
  );
  const get = vi.spyOn(nodesApi, "getChildren");
  const { result } = renderHook(() => useFileUpload(oldFolder.id, []));
  await act(async () => {
    await result.current.handleUploadFiles(files);
  });
  expect(get).not.toHaveBeenCalled();
  expect(uploadManager.enqueue).toHaveBeenCalledWith(
    [{ file: files[0] }],
    oldFolder.id,
    expect.any(String),
    expect.objectContaining({ encrypt: false }),
  );
});

it("does not upload using stale data after a failed background refresh", async () => {
  const request = deferredResponse();
  vi.spyOn(nodesApi, "getChildren").mockReturnValue(request.promise);
  queryClient.setQueryData(
    folderOverviewOptions(oldFolder.id, "user").queryKey,
    response(oldFolder),
    { updatedAt: 1 },
  );
  const { result } = renderHook(
    () => ({
      data: useFilesData({
        nodeId: oldFolder.id,
        layoutType: InterfaceLayoutType.List,
        loadNode,
      }),
      upload: useFileUpload(oldFolder.id, []),
    }),
    { wrapper },
  );
  const pending = result.current.upload.handleUploadFiles(files);
  const rejected = expect(pending).rejects.toThrow("offline");
  await act(async () => {
    request.reject(new Error("offline"));
    await rejected;
  });
  await expect(result.current.upload.handleUploadFiles(files)).rejects.toThrow(
    "offline",
  );
  expect(uploadManager.enqueue).not.toHaveBeenCalled();
});

it("waits for an explicit refresh even when cached data is still within staleTime", async () => {
  const options = folderOverviewOptions(oldFolder.id, "user");
  queryClient.setQueryData(options.queryKey, response(oldFolder));
  const request = deferredResponse();
  const get = vi
    .spyOn(nodesApi, "getChildren")
    .mockReturnValue(request.promise);
  const refresh = queryClient.fetchQuery({ ...options, staleTime: 0 });
  const { result } = renderHook(() => useFileUpload(oldFolder.id, []));
  const upload = result.current.handleUploadFiles(files);
  expect(uploadManager.enqueue).not.toHaveBeenCalled();
  await act(async () => {
    request.resolve(response(encryptedFolder));
    await refresh;
    await upload;
  });
  expect(get).toHaveBeenCalledTimes(1);
  expect(uploadManager.enqueue).toHaveBeenCalledWith(
    [{ file: files[0] }],
    oldFolder.id,
    expect.any(String),
    expect.objectContaining({ encrypt: true }),
  );
});

it("blocks an encrypted upload if the vault is locked", async () => {
  useVault.setState({ isUnlocked: false });
  queryClient.setQueryData(
    folderOverviewOptions(oldFolder.id, "user").queryKey,
    response(encryptedFolder),
  );
  const onToast = vi.fn();
  const { result } = renderHook(() =>
    useFileUpload(oldFolder.id, [], { onToast }),
  );
  await act(async () => {
    await result.current.handleUploadFiles(files);
  });
  expect(uploadManager.enqueue).not.toHaveBeenCalled();
  expect(onToast).toHaveBeenCalledWith(expect.any(String), "error");
});

it("rejects a pending upload after the account changes", async () => {
  const request = deferredResponse();
  vi.spyOn(nodesApi, "getChildren").mockReturnValue(request.promise);
  const { result } = renderHook(() => useFileUpload(oldFolder.id, []));
  const upload = result.current.handleUploadFiles(files);
  useAuthStore.setState({ user: null });
  request.resolve(response(encryptedFolder));
  await expect(upload).rejects.toThrow("account changed");
  expect(useNodesStore.getState().currentNode).toBe(parent);
  expect(uploadManager.enqueue).not.toHaveBeenCalled();
});

it("keeps a newer local metadata update when an older listing is cached", async () => {
  queryClient.setQueryData(
    folderOverviewOptions(oldFolder.id, "user").queryKey,
    response(oldFolder),
  );
  useNodesStore.setState({ currentNode: encryptedFolder, ancestors: [parent] });
  const { result } = renderHook(
    () => ({
      data: useFilesData({
        nodeId: oldFolder.id,
        layoutType: InterfaceLayoutType.List,
        loadNode,
      }),
      upload: useFileUpload(oldFolder.id, []),
    }),
    { wrapper },
  );
  await act(async () => {
    await result.current.upload.handleUploadFiles(files);
  });
  expect(useNodesStore.getState().currentNode).toEqual(encryptedFolder);
  expect(uploadManager.enqueue).toHaveBeenCalledWith(
    [{ file: files[0] }],
    oldFolder.id,
    expect.any(String),
    expect.objectContaining({ encrypt: true }),
  );
});

it("does not switch the current folder back when a previous listing finishes late", async () => {
  const other = createFolder({ id: "other", parentId: parent.id });
  const request = deferredResponse();
  vi.mocked(nodesApi.getNode).mockResolvedValue(other);
  vi.spyOn(nodesApi, "getChildren").mockImplementation((id) => {
    if (id === oldFolder.id) {
      return request.promise;
    }
    return Promise.resolve(response(other));
  });
  const { rerender } = renderHook(
    ({ nodeId }) =>
      useFilesData({ nodeId, layoutType: InterfaceLayoutType.List, loadNode }),
    { wrapper, initialProps: { nodeId: oldFolder.id } },
  );
  await waitFor(() =>
    expect(useNodesStore.getState().currentNode?.id).toBe(oldFolder.id),
  );
  rerender({ nodeId: other.id });
  await waitFor(() =>
    expect(useNodesStore.getState().currentNode?.id).toBe(other.id),
  );
  await act(async () => {
    request.resolve(response(encryptedFolder));
    await request.promise;
  });
  expect(useNodesStore.getState().currentNode).toEqual(other);
});
