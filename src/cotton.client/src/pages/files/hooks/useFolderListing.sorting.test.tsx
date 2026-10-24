import { QueryClientProvider } from "@tanstack/react-query";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import type { PropsWithChildren } from "react";
import { afterEach, expect, it, vi } from "vitest";
import { createFile, createFolder } from "../../../test/fileFixtures";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import { nodesApi, type NodeResponse } from "../../../shared/api/nodesApi";
import { queryClient } from "../../../shared/api/queries/queryClient";
import { useFolderListing } from "./useFolderListing";

const wrapper = ({ children }: PropsWithChildren) => (
  <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
);
const response = (encryptedFiles = 0): NodeResponse => ({
  content: {
    ...createFolder(),
    nodes: [],
    files: Array.from({ length: 201 }, (_, index) =>
      createFile({
        id: `file-${index}`,
        metadata:
          encryptedFiles > 0 && index === 0
            ? { isClientEncrypted: "true" }
            : {},
      }),
    ),
    stats: { files: 201, folders: 0, sizeBytes: 12345 },
  },
  totalCount: 201,
});
afterEach(() => {
  cleanup();
  queryClient.clear();
  vi.restoreAllMocks();
});

it("requests ordering and filtering before pagination and keeps unfiltered folder totals", async () => {
  const get = vi
    .spyOn(nodesApi, "getChildren")
    .mockImplementation(async (_, options) => ({
      ...response(0),
      totalCount: options?.listing?.filterValue ? 1 : 201,
    }));
  const { result } = renderHook(
    () => useFolderListing("folder-1", "user-1", InterfaceLayoutType.List),
    { wrapper },
  );
  await waitFor(() =>
    expect(result.current.pagination?.query?.namesEnabled).toBe(true),
  );
  act(() =>
    result.current.pagination?.onPaginationModelChange({
      page: 1,
      pageSize: 100,
    }),
  );
  expect(result.current.content?.files[0].id).toBe("file-100");
  expect(get).toHaveBeenCalledTimes(1);
  act(() =>
    result.current.pagination?.query?.onSortModelChange([
      { field: "name", sort: "desc" },
    ]),
  );
  await waitFor(() =>
    expect(get).toHaveBeenLastCalledWith("folder-1", {
      page: 1,
      pageSize: 25000,
      signal: expect.any(AbortSignal),
      listing: { sortBy: "Name", descending: true },
    }),
  );
  act(() =>
    result.current.pagination?.query?.onFilterModelChange({
      items: [{ field: "name", operator: "contains", value: " report " }],
    }),
  );
  await waitFor(() => expect(result.current.pagination?.totalCount).toBe(1));
  expect(get).toHaveBeenLastCalledWith("folder-1", {
    page: 1,
    pageSize: 25000,
    signal: expect.any(AbortSignal),
    listing: {
      sortBy: "Name",
      descending: true,
      filterBy: "Name",
      filterOperator: "Contains",
      filterValue: "report",
    },
  });
  expect(result.current.stats.files).toBe(201);
  expect(result.current.stats.sizeBytes).toBe(12345);
});

it.each([
  { policy: true, encrypted: 0 },
  { policy: false, encrypted: 1 },
])(
  "disables name operations and loads the complete listing for size sorting: %j",
  async ({ policy, encrypted }) => {
    const get = vi
      .spyOn(nodesApi, "getChildren")
      .mockResolvedValue(response(encrypted));
    const { result } = renderHook(
      () =>
        useFolderListing(
          "folder-1",
          "user-1",
          InterfaceLayoutType.List,
          policy,
        ),
      { wrapper },
    );
    await waitFor(() => expect(result.current.content).toBeDefined());
    expect(result.current.pagination?.query?.namesEnabled).toBe(false);
    act(() =>
      result.current.pagination?.query?.onFilterModelChange({
        items: [{ field: "name", operator: "contains", value: "private" }],
      }),
    );
    expect(get).toHaveBeenCalledTimes(1);
    act(() =>
      result.current.pagination?.query?.onSortModelChange([
        { field: "sizeBytes", sort: "desc" },
      ]),
    );
    await waitFor(() =>
      expect(get).toHaveBeenLastCalledWith("folder-1", {
        page: 1,
        pageSize: 25_000,
        signal: expect.any(AbortSignal),
      }),
    );
    expect(result.current.pagination?.mode).toBe("client");
  },
);
