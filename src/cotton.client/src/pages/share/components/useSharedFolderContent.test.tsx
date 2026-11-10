import { act, cleanup, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { sharedFoldersApi } from "@shared/api/sharedFoldersApi";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { useSharedFolderContent } from "./useSharedFolderContent";

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
  vi.spyOn(sharedFoldersApi, "getChildren").mockResolvedValue(response);
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.restoreAllMocks();
});

it("fetches delayed previews after upload with a bounded, coalesced refresh sequence", async () => {
  const { result } = renderHook(() =>
    useSharedFolderContent("public", folder.id),
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
      { initialProps: { token: "public", nodeId: folder.id } },
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
  );
  await act(async () => {});
  act(result.current.reload);
  unmount();
  await advance(60_000);
  expect(sharedFoldersApi.getChildren).toHaveBeenCalledTimes(1);
});
