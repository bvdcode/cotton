import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, renderHook, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { nodesApi } from "@shared/api/nodesApi";
import { createFolder } from "../../../test/fileFixtures";
import { calculateFolderSizes, useFolderSizes } from "./useFolderSizes";

vi.mock("@shared/store/authStore", () => ({
  useAuthStore: (select: (state: { user: { id: string } }) => string) =>
    select({ user: { id: "user" } }),
}));
vi.mock("@shared/ui/notifications", () => ({ toast: { error: vi.fn() } }));
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

it.each([0, 1])(
  "calculates visible trash folders at depth %i",
  async (depth) => {
    const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue({
      content: {
        ...createFolder(),
        nodes: [createFolder({ id: "deleted" })],
        files: [],
      },
      totalCount: 1,
    });
    const stats = vi
      .spyOn(nodesApi, "getFolderStats")
      .mockResolvedValue({ sizeBytes: 42, folders: 1, files: 2 });
    const client = new QueryClient();
    const signal = new AbortController().signal;
    await expect(
      calculateFolderSizes(client, "trash", "user", signal, undefined, {
        nodeType: "trash",
        depth,
      }),
    ).resolves.toBe(42);
    expect(get).toHaveBeenCalledWith("trash", {
      nodeType: "trash",
      depth,
      page: 1,
      pageSize: 25_000,
      signal,
    });
    expect(stats).toHaveBeenCalledWith(
      "deleted",
      true,
      expect.any(AbortSignal),
    );
    client.clear();
  },
);

it("cancels an active trash calculation on leaving and never starts the next folder", async () => {
  vi.spyOn(nodesApi, "getChildren").mockResolvedValue({
    content: {
      ...createFolder(),
      nodes: [createFolder({ id: "one" }), createFolder({ id: "two" })],
      files: [],
    },
    totalCount: 2,
  });
  let activeSignal: AbortSignal | undefined;
  const stats = vi
    .spyOn(nodesApi, "getFolderStats")
    .mockImplementation((_id, _recursive, signal) => {
      activeSignal = signal;
      return new Promise((_resolve, reject) =>
        signal?.addEventListener("abort", () => reject(signal.reason)),
      );
    });
  const client = new QueryClient();
  const { unmount } = renderHook(
    () => useFolderSizes("trash", true, { nodeType: "trash", depth: 1 }),
    {
      wrapper: ({ children }) => (
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
      ),
    },
  );
  await waitFor(() => expect(stats).toHaveBeenCalledTimes(1));
  unmount();
  expect(activeSignal?.aborted).toBe(true);
  expect(stats).toHaveBeenCalledTimes(1);
  client.clear();
});
