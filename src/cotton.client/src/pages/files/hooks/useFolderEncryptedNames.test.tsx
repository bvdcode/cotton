import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { nodesApi, type NodeResponse } from "@shared/api/nodesApi";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { useFolderEncryptedNames } from "./useFolderEncryptedNames";

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

it.each([
  { nodes: [createFolder({ metadata: { en: "opaque" } })], files: [] },
  {
    nodes: [],
    files: [createFile({ metadata: { isClientEncrypted: "true" } })],
  },
])(
  "uses an encrypted name in the loaded page without requests: %j",
  (items) => {
    const get = vi.spyOn(nodesApi, "getChildren");
    const { result } = renderHook(
      () =>
        useFolderEncryptedNames({
          nodeId: "root",
          enabled: true,
          firstPage: {
            response: {
              content: { ...createFolder(), ...items },
              totalCount: 100,
            },
            pageSize: 1,
          },
        }),
      { wrapper },
    );
    expect(result.current).toBe(true);
    expect(get).not.toHaveBeenCalled();
  },
);

it("does not read child folders when the current listing is complete", () => {
  const get = vi.spyOn(nodesApi, "getChildren");
  const { result } = renderHook(
    () =>
      useFolderEncryptedNames({
        nodeId: "root",
        enabled: true,
        firstPage: {
          response: {
            content: { ...createFolder(), nodes: [createFolder()], files: [] },
            totalCount: 1,
          },
          pageSize: 100,
        },
      }),
    { wrapper },
  );
  expect(result.current).toBe(false);
  expect(get).not.toHaveBeenCalled();
});

it.each([true, false])(
  "checks subsequent pages of this folder only: encrypted=%s",
  async (encrypted) => {
    const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue({
      content: {
        ...createFolder(),
        nodes: [],
        files: [
          createFile({
            metadata: { isClientEncrypted: String(encrypted) },
          }),
        ],
      },
      totalCount: 2,
    });
    const firstPage: NodeResponse = {
      content: {
        ...createFolder(),
        nodes: [createFolder({ id: "child" })],
        files: [],
      },
      totalCount: 2,
    };
    const { result } = renderHook(
      () =>
        useFolderEncryptedNames({
          nodeId: "root",
          enabled: true,
          firstPage: { response: firstPage, pageSize: 1 },
        }),
      { wrapper },
    );
    await waitFor(() => expect(result.current).toBe(encrypted));
    expect(get).toHaveBeenCalledExactlyOnceWith("root", {
      page: 2,
      pageSize: 1,
      signal: expect.any(AbortSignal),
    });
  },
);

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
    ({ id }) => useFolderEncryptedNames({ nodeId: id, enabled: true }),
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
