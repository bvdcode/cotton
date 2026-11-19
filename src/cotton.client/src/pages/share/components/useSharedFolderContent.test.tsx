import { act, cleanup, renderHook } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { PropsWithChildren } from "react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { sharedFoldersApi } from "@shared/api/sharedFoldersApi";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { useSharedFolderContent } from "./useSharedFolderContent";
import type { FolderStatsDto } from "@shared/api/nodesApi";

let queryClient: QueryClient;
const wrapper = ({ children }: PropsWithChildren) => (
  <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
);

const translate = (key: string) => key;
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: translate }),
}));

const folder = createFolder();
const response = {
  content: {
    id: folder.id,
    createdAt: folder.createdAt,
    updatedAt: folder.updatedAt,
    nodes: [],
    files: [createFile({ previewHashEncryptedHex: null })],
  },
  totalCount: 1,
};

const advance = (milliseconds: number) =>
  act(async () => {
    await vi.advanceTimersByTimeAsync(milliseconds);
  });

beforeEach(() => {
  vi.useFakeTimers();
  queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity } },
  });
  vi.spyOn(sharedFoldersApi, "getChildren").mockResolvedValue(response);
  vi.spyOn(sharedFoldersApi, "getStats").mockResolvedValue({
    folders: 2, files: 3, sizeBytes: 10_000,
  });
});

afterEach(() => {
  cleanup();
  queryClient.clear();
  vi.useRealTimers();
  vi.restoreAllMocks();
});

it("fetches delayed previews after upload with a bounded, coalesced refresh sequence", async () => {
  const { result } = renderHook(() =>
    useSharedFolderContent("public", folder.id),
    { wrapper },
  );
  await act(async () => {});
  expect(sharedFoldersApi.getChildren).toHaveBeenCalledTimes(1);
  act(() => {
    result.current.reload();
    result.current.reload();
  });
  await advance(300);
  expect(sharedFoldersApi.getChildren).toHaveBeenCalledTimes(2);
  vi.mocked(sharedFoldersApi.getChildren).mockResolvedValue({
    ...response,
    content: {
      ...response.content,
      files: [createFile({ previewHashEncryptedHex: "generated-preview" })],
    },
  });
  await advance(1_700);
  expect(result.current.content?.files[0].previewHashEncryptedHex).toBe(
    "generated-preview",
  );
  await advance(6_000);
  expect(sharedFoldersApi.getChildren).toHaveBeenCalledTimes(4);
  await advance(60_000);
  expect(sharedFoldersApi.getChildren).toHaveBeenCalledTimes(4);
});

it.each([
  { token: "public", nodeId: "another-folder" },
  { token: "another-share", nodeId: folder.id },
])(
  "cancels pending refreshes and ignores old upload callbacks after navigation to %s",
  async (destination) => {
    const { result, rerender } = renderHook(
      ({ token, nodeId }) => useSharedFolderContent(token, nodeId),
      { initialProps: { token: "public", nodeId: folder.id }, wrapper },
    );
    await act(async () => {});
    const oldReload = result.current.reload;
    act(oldReload);
    rerender(destination);
    await act(async () => {});
    act(oldReload);
    await advance(60_000);
    expect(sharedFoldersApi.getChildren).toHaveBeenCalledTimes(2);
    expect(sharedFoldersApi.getChildren).toHaveBeenLastCalledWith(
      destination.token,
      {
        nodeId: destination.nodeId,
        page: 1,
        pageSize: 1000,
      },
    );
  },
);

it("cancels pending refreshes when the public viewer is closed", async () => {
  const { result, unmount } = renderHook(() =>
    useSharedFolderContent("public", folder.id),
    { wrapper },
  );
  await act(async () => {});
  act(result.current.reload);
  unmount();
  await advance(60_000);
  expect(sharedFoldersApi.getChildren).toHaveBeenCalledTimes(1);
});

it("shows content before recursive statistics finish", async () => {
  let finishStats: ((stats: FolderStatsDto) => void) | undefined;
  vi.mocked(sharedFoldersApi.getStats).mockImplementation(
    () => new Promise((resolve) => { finishStats = resolve; }),
  );
  const { result } = renderHook(
    () => useSharedFolderContent("public", folder.id),
    { wrapper },
  );
  await advance(1);
  expect(result.current.content).toEqual(response.content);
  expect(result.current.loading).toBe(false);
  expect(result.current.isSizeCalculating).toBe(true);
  expect(result.current.stats).toBeUndefined();
  act(() => finishStats?.({ folders: 2, files: 3, sizeBytes: 10_000 }));
  await advance(1);
  expect(result.current.stats?.sizeBytes).toBe(10_000);
  expect(result.current.isSizeCalculating).toBe(false);
});

it.each([
  { token: "public", nodeId: "child-folder" },
  { token: "other-share", nodeId: folder.id },
])("does not carry a previous folder or share size into %s", async (destination) => {
  const { result, rerender } = renderHook(
    ({ token, nodeId }) => useSharedFolderContent(token, nodeId),
    { initialProps: { token: "public", nodeId: folder.id }, wrapper },
  );
  await advance(1);
  expect(result.current.stats?.sizeBytes).toBe(10_000);
  vi.mocked(sharedFoldersApi.getStats).mockResolvedValue({
    folders: 0, files: 1, sizeBytes: 200,
  });
  rerender(destination);
  expect(result.current.stats).toBeUndefined();
  await advance(1);
  expect(sharedFoldersApi.getStats).toHaveBeenLastCalledWith(
    destination.token, destination.nodeId, expect.any(AbortSignal),
  );
  expect(result.current.stats?.sizeBytes).toBe(200);
});

it("keeps the recursive size during the coalesced upload refresh", async () => {
  const { result } = renderHook(
    () => useSharedFolderContent("public", folder.id),
    { wrapper },
  );
  await advance(1);
  vi.mocked(sharedFoldersApi.getStats).mockImplementation(
    () => new Promise(() => {}),
  );
  act(() => { result.current.reload(); result.current.reload(); });
  await advance(300);
  expect(result.current.stats?.sizeBytes).toBe(10_000);
  expect(result.current.isSizeCalculating).toBe(true);
  expect(sharedFoldersApi.getStats).toHaveBeenCalledTimes(2);
});
