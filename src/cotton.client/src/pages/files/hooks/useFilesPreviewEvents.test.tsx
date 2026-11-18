import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, renderHook } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { queryKeys } from "../../../shared/api/queries/queryKeys";
import type { NodeResponse } from "../../../shared/api/nodesApi";
import type { PreviewUpdates } from "../../../shared/signalr/subscribeToPreviewGenerated";
import { useFilesRealtimeEvents } from "./useFilesRealtimeEvents";

const events = vi.hoisted(() => ({
  preview: null as ((previews: PreviewUpdates) => void) | null,
  unsubscribe: vi.fn(),
}));
vi.mock("../../../features/auth", () => ({
  useAuth: () => ({ isAuthenticated: true }),
}));
vi.mock("../../../shared/signalr", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../../shared/signalr")>()),
  useFileTreeRealtimeInvalidation: () => vi.fn(),
  subscribeToPreviewGenerated: (listener: typeof events.preview) => {
    events.preview = listener;
    return events.unsubscribe;
  },
}));
afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("preview generated events", () => {
  it("patches cached previews across navigation without reloading or creating absent caches", () => {
    const client = new QueryClient();
    const key = queryKeys.nodeChildren.overview("folder-1", "user-1", 1000);
    client.setQueryData<NodeResponse>(key, {
      content: {
        node: createFolder(),
        ...createFolder(),
        nodes: [],
        files: [createFile()],
      },
      totalCount: 1,
    });
    const otherKey = queryKeys.nodeChildren.complete("other-folder", "user-1");
    client.setQueryData<NodeResponse>(otherKey, {
      content: {
        node: createFolder({ id: "other-folder" }),
        ...createFolder({ id: "other-folder" }),
        nodes: [],
        files: [createFile({ id: "other-file", nodeId: "other-folder" })],
      },
      totalCount: 1,
    });
    const onInvalidate = vi.fn();
    const wrapper = ({ children }: PropsWithChildren) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    const { rerender, unmount } = renderHook(
      ({ nodeId }) => useFilesRealtimeEvents({ nodeId, onInvalidate }),
      { initialProps: { nodeId: "folder-1" }, wrapper },
    );
    events.preview?.(new Map([["folder-1", new Map([["file-1", "ready"]])]]));
    expect(
      client.getQueryData<NodeResponse>(key)?.content.files[0]
        .previewHashEncryptedHex,
    ).toBe("ready");
    events.preview?.(new Map([["folder-1", new Map([["off-page", "ready"]])]]));
    rerender({ nodeId: "other-folder" });
    events.preview?.(
      new Map([
        ["folder-1", new Map([["file-1", "new-preview"]])],
        ["other-folder", new Map([["other-file", "other-preview"]])],
        ["uncached-folder", new Map([["uncached-file", "unused"]])],
      ]),
    );
    rerender({ nodeId: "folder-1" });
    expect(
      client.getQueryData<NodeResponse>(key)?.content.files[0]
        .previewHashEncryptedHex,
    ).toBe("new-preview");
    expect(
      client.getQueryData<NodeResponse>(otherKey)?.content.files[0]
        .previewHashEncryptedHex,
    ).toBe("other-preview");
    events.preview?.(
      new Map([["other-folder", new Map([["other-file", "latest"]])]]),
    );
    expect(
      client.getQueryData<NodeResponse>(otherKey)?.content.files[0]
        .previewHashEncryptedHex,
    ).toBe("latest");
    expect(client.getQueryCache().getAll()).toHaveLength(2);
    expect(onInvalidate).not.toHaveBeenCalled();
    unmount();
    expect(events.unsubscribe).toHaveBeenCalledOnce();
    client.clear();
  });
});
