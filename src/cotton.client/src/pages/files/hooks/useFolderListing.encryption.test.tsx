import { QueryClientProvider } from "@tanstack/react-query";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { afterEach, expect, it, vi } from "vitest";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import { nodesApi, type NodeResponse } from "../../../shared/api/nodesApi";
import { queryClient } from "../../../shared/api/queries/queryClient";
import { queryKeys } from "../../../shared/api/queries/queryKeys";
import {
  encryptDisplayMeta,
  generateMasterKey,
  useVault,
} from "../../../shared/crypto";
import { useFolderListing } from "./useFolderListing";

const wrapper = ({ children }: PropsWithChildren) => (
  <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
);
afterEach(() => {
  cleanup();
  queryClient.clear();
  useVault.getState().lock();
  vi.restoreAllMocks();
});

it.each([InterfaceLayoutType.List, InterfaceLayoutType.Tiles])(
  "updates loaded names on unlock in layout %s without refetching",
  async (layout) => {
    const key = await generateMasterKey();
    useVault.getState().unlock(key, { persistToSession: false });
    const en = await encryptDisplayMeta({
      name: "photo.jpg",
      contentType: "image/jpeg",
    });
    useVault.getState().lock();
    const response: NodeResponse = {
      content: {
        ...createFolder({ id: "folder-1" }),
        nodes: [],
        files: [
          createFile({
            name: "opaque-id",
            contentType: "application/octet-stream",
            metadata: { en, isClientEncrypted: "true" },
          }),
        ],
        stats: { folders: 0, files: 1, encryptedFiles: 1, sizeBytes: 10 },
      },
      totalCount: 1,
    };
    const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue(response);
    const { result } = renderHook(
      () => useFolderListing("folder-1", "user-1", layout),
      { wrapper },
    );
    await waitFor(() =>
      expect(result.current.content?.files[0].name).toBe("opaque-id"),
    );
    act(() => useVault.getState().unlock(key, { persistToSession: false }));
    await waitFor(() =>
      expect(result.current.content?.files[0]).toMatchObject({
        name: "photo.jpg",
        contentType: "image/jpeg",
      }),
    );
    expect(
      queryClient.getQueryData<NodeResponse>(
        queryKeys.nodeChildren.overview(
          "folder-1",
          "user-1",
          layout === InterfaceLayoutType.List ? 100 : 1000,
        ),
      )?.content.files[0].name,
    ).toBe("opaque-id");
    expect(get).toHaveBeenCalledTimes(1);
    act(() => useVault.getState().lock());
    expect(result.current.content?.files[0].name).toBe("opaque-id");
  },
);
