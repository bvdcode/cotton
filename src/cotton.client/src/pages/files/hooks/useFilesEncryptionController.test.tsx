import { QueryClientProvider } from "@tanstack/react-query";
import { cleanup, renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { nodesApi, type NodeContentDto } from "@shared/api/nodesApi";
import { queryClient } from "@shared/api/queries/queryClient";
import {
  encodeEnvelopePreference,
  ENVELOPE_PREFERENCE_KEY,
  useVault,
} from "@shared/crypto";
import { useUserPreferencesStore } from "@shared/store/userPreferencesStore";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { useFilesEncryptionController } from "./useFilesEncryptionController";

const wrapper = ({ children }: PropsWithChildren) => (
  <QueryClientProvider client={queryClient}>
    <MemoryRouter>{children}</MemoryRouter>
  </QueryClientProvider>
);

beforeEach(() => {
  useVault.getState().lock();
  useUserPreferencesStore.setState({
    preferences: {
      [ENVELOPE_PREFERENCE_KEY]: encodeEnvelopePreference(Uint8Array.of(1)),
    },
  });
});

afterEach(() => {
  cleanup();
  queryClient.clear();
  useUserPreferencesStore.getState().reset();
  vi.restoreAllMocks();
});

it("opens a large directory without reading any children for encryption notices", async () => {
  const root = createFolder({ id: "root" });
  const content: NodeContentDto = {
    ...root,
    nodes: Array.from({ length: 1_000 }, (_, index) =>
      createFolder({ id: `child-${index}`, parentId: root.id }),
    ),
    files: [createFile({ nodeId: root.id })],
  };
  const get = vi
    .spyOn(nodesApi, "getChildren")
    .mockImplementation(async (id) => {
      const page =
        id === root.id
          ? content
          : { ...createFolder({ id }), nodes: [], files: [] };
      return {
        content: page,
        totalCount: page.nodes.length + page.files.length,
      };
    });
  const { result, rerender } = renderHook(
    () =>
      useFilesEncryptionController({
        activeCurrentNode: root,
        ancestors: [],
        content,
        nodeId: root.id,
        showToast: vi.fn(),
      }),
    { wrapper },
  );
  await waitFor(() => expect(queryClient.isFetching()).toBe(0));
  rerender();
  expect(result.current.folderEncryptionPrompt).toBeNull();
  expect(get).not.toHaveBeenCalled();
});

it.each([
  { policy: false, encrypted: true, severity: "info" },
  { policy: true, encrypted: false, severity: "warning" },
])(
  "updates the notice from loaded files only: %j",
  async ({ policy, encrypted, severity }) => {
    const root = createFolder({
      id: "root",
      metadata: { isClientEncryptionEnabled: String(policy) },
    });
    const empty: NodeContentDto = { ...root, nodes: [], files: [] };
    const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue({
      content: empty,
      totalCount: 0,
    });
    const { result, rerender } = renderHook(
      ({ content }: { content: NodeContentDto }) =>
        useFilesEncryptionController({
          activeCurrentNode: root,
          ancestors: [],
          content,
          nodeId: root.id,
          showToast: vi.fn(),
        }),
      { initialProps: { content: empty }, wrapper },
    );
    await waitFor(() => expect(queryClient.isFetching()).toBe(0));
    expect(result.current.folderEncryptionPrompt).toBeNull();
    rerender({
      content: {
        ...empty,
        files: [
          createFile({ metadata: { isClientEncrypted: String(encrypted) } }),
        ],
      },
    });
    expect(result.current.folderEncryptionPrompt?.severity).toBe(severity);
    rerender({ content: empty });
    expect(result.current.folderEncryptionPrompt).toBeNull();
    expect(get).not.toHaveBeenCalled();
  },
);

it("does not use stale files while navigating to another folder", () => {
  const get = vi.spyOn(nodesApi, "getChildren");
  const { result } = renderHook(
    () =>
      useFilesEncryptionController({
        activeCurrentNode: createFolder({ id: "current" }),
        ancestors: [],
        content: {
          ...createFolder({ id: "previous" }),
          nodes: [],
          files: [createFile({ metadata: { isClientEncrypted: "true" } })],
        },
        nodeId: "current",
        showToast: vi.fn(),
      }),
    { wrapper },
  );
  expect(result.current.folderEncryptionPrompt).toBeNull();
  expect(get).not.toHaveBeenCalled();
});
