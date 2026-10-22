import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { nodesApi } from "../../../shared/api/nodesApi";
import { queryKeys } from "../../../shared/api/queries/queryKeys";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { useFolderEncryptionPresence } from "./useFolderEncryptionPresence";

let client: QueryClient;
const wrapper = ({ children }: PropsWithChildren) => (
  <QueryClientProvider client={client}>{children}</QueryClientProvider>
);
beforeEach(() => {
  client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
});
afterEach(() => {
  cleanup();
  client.clear();
  vi.restoreAllMocks();
});

it("uses a visible match without another request", () => {
  const get = vi.spyOn(nodesApi, "getChildren");
  const { result } = renderHook(
    () =>
      useFolderEncryptionPresence({
        nodeId: "root",
        match: "plain-files",
        enabled: true,
        knownFiles: [createFile()],
      }),
    { wrapper },
  );
  expect(result.current).toBe(true);
  expect(get).not.toHaveBeenCalled();
});

it("cancels the previous folder on navigation and the current folder on unmount", async () => {
  const signals: AbortSignal[] = [];
  vi.spyOn(nodesApi, "getChildren").mockImplementation(
    (_, options) =>
      new Promise((_, reject) => {
        const signal = options?.signal;
        if (!signal) {
          throw new Error("Missing cancellation signal");
        }
        signals.push(signal);
        signal.addEventListener("abort", () => reject(signal.reason), {
          once: true,
        });
      }),
  );
  const { rerender, unmount, result } = renderHook(
    ({ id }) =>
      useFolderEncryptionPresence({
        nodeId: id,
        match: "plain-files",
        enabled: true,
      }),
    { initialProps: { id: "first" }, wrapper },
  );
  await waitFor(() => expect(signals).toHaveLength(1));
  rerender({ id: "second" });
  await waitFor(() => expect(signals).toHaveLength(2));
  expect(signals[0].aborted).toBe(true);
  expect(result.current).toBeUndefined();
  unmount();
  expect(signals[1].aborted).toBe(true);
});

it("refreshes the result after folder content is invalidated", async () => {
  const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue({
    content: {
      ...createFolder({ id: "root" }),
      nodes: [],
      files: [createFile()],
    },
    totalCount: 1,
  });
  const { result } = renderHook(
    () =>
      useFolderEncryptionPresence({
        nodeId: "root",
        match: "plain-files",
        enabled: true,
      }),
    { wrapper },
  );
  await waitFor(() => expect(result.current).toBe(true));
  get.mockResolvedValue({
    content: { ...createFolder({ id: "root" }), nodes: [], files: [] },
    totalCount: 0,
  });
  await act(() =>
    client.invalidateQueries({ queryKey: queryKeys.nodeChildren.all("root") }),
  );
  await waitFor(() => expect(result.current).toBe(false));
});
