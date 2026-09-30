import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, renderHook } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { queryKeys } from "../../../shared/api/queries/queryKeys";
import type { NodeResponse } from "../../../shared/api/nodesApi";
import { useFilesRealtimeEvents } from "./useFilesRealtimeEvents";

const events = vi.hoisted(() => ({
  preview: null as
    ((nodeId: string, fileId: string, hash: string) => void) | null,
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
  it("patches the active folder without scheduling a reload and follows navigation", () => {
    const client = new QueryClient();
    const key = queryKeys.nodeChildren.overview("folder-1", "user-1", 1000);
    client.setQueryData<NodeResponse>(key, {
      content: { ...createFolder(), nodes: [], files: [createFile()] },
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
    events.preview?.("folder-1", "file-1", "ready");
    expect(
      client.getQueryData<NodeResponse>(key)?.content.files[0]
        .previewHashEncryptedHex,
    ).toBe("ready");
    events.preview?.("folder-1", "off-page", "ready");
    rerender({ nodeId: "other-folder" });
    events.preview?.("folder-1", "file-1", "obsolete");
    expect(
      client.getQueryData<NodeResponse>(key)?.content.files[0]
        .previewHashEncryptedHex,
    ).toBe("ready");
    expect(onInvalidate).not.toHaveBeenCalled();
    unmount();
    expect(events.unsubscribe).toHaveBeenCalledOnce();
    client.clear();
  });
});
