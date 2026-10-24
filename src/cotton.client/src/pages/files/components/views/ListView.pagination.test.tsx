import { QueryClientProvider } from "@tanstack/react-query";
import {
  cleanup,
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { queryClient } from "../../../../shared/api/queries/queryClient";
import { nodesApi } from "../../../../shared/api/nodesApi";
import { InterfaceLayoutType } from "../../../../shared/api/layoutsApi";
import { createFile, createFolder } from "../../../../test/fileFixtures";
import { useFolderListing } from "../../hooks/useFolderListing";
import { useContentTiles } from "../../../../shared/hooks/useContentTiles";
import { ListView } from "./ListView";
import { queryKeys } from "@shared/api/queries/queryKeys";

vi.mock("@shared/store/authStore", () => ({
  useAuthStore: (select: (state: { user: { id: string } }) => string) =>
    select({ user: { id: "user-1" } }),
}));

afterEach(() => {
  cleanup();
  queryClient.clear();
  vi.restoreAllMocks();
});

function FolderTable() {
  const listing = useFolderListing(
    "folder-1",
    "user-1",
    InterfaceLayoutType.List,
  );
  const { tiles } = useContentTiles(listing.content, { sortMode: "server" });
  return (
    <ListView
      tiles={tiles}
      pagination={listing.pagination}
      isCreatingFolder={false}
      newFolderName=""
      onNewFolderNameChange={() => {}}
      onConfirmNewFolder={async () => {}}
      onCancelNewFolder={() => {}}
      folderNamePlaceholder=""
      fileNamePlaceholder=""
      folderOperations={{
        isRenaming: () => false,
        getRenamingName: () => "",
        onRenamingNameChange: () => {},
        onClick: () => {},
      }}
      fileOperations={{
        isRenaming: () => false,
        getRenamingName: () => "",
        onRenamingNameChange: () => {},
        onClick: () => {},
      }}
    />
  );
}

it("keeps the requested page after the grid processes its controlled sort and filter models", async () => {
  const get = vi.spyOn(nodesApi, "getChildren").mockResolvedValue({
    totalCount: 201,
    content: {
      ...createFolder(),
      nodes: [],
      files: Array.from({ length: 201 }, (_, index) =>
        createFile({
          id: `file-${index}`,
          name:
            index % 100 === 0
              ? `page-${Math.floor(index / 100) + 1}.txt`
              : `file-${index}.txt`,
        }),
      ),
      stats: { folders: 0, files: 201, sizeBytes: 201 },
    },
  });
  render(
    <QueryClientProvider client={queryClient}>
      <FolderTable />
    </QueryClientProvider>,
  );
  await screen.findByText("page-1.txt");
  fireEvent.click(screen.getByRole("button", { name: "Go to next page" }));
  await screen.findByText("page-2.txt");
  expect(get).toHaveBeenCalledTimes(1);
  await waitFor(() =>
    expect(screen.getByText("101–200 of 201")).toBeInTheDocument(),
  );
  expect(
    screen.getByRole("button", { name: "Go to previous page" }),
  ).toBeEnabled();
  fireEvent.keyDown(document.body, { key: "F" });
  await waitFor(() =>
    expect(
      document.activeElement
        ?.closest(".MuiDataGrid-row")
        ?.getAttribute("data-id"),
    ).toBe("file-101"),
  );
});

it("sorts the complete directory and reorders folders as their sizes arrive", async () => {
  const folders = ["large", "pending", "empty"].map((id) =>
    createFolder({ id, name: id }),
  );
  const files = Array.from({ length: 102 }, (_, i) =>
    createFile({ id: `file-${i}`, name: `file-${i}`, sizeBytes: 102 - i }),
  );
  const get = vi
    .spyOn(nodesApi, "getChildren")
    .mockImplementation(async (_, options) => ({
      totalCount: 105,
      content: {
        ...createFolder(),
        nodes: folders,
        files: options?.pageSize === 25_000 ? files : files.slice(0, 97),
        stats: { folders: 3, files: 102, sizeBytes: 5253 },
      },
    }));
  const setSize = (id: string, sizeBytes: number) =>
    queryClient.setQueryData(queryKeys.nodeChildren.folderInfo(id, "user-1"), {
      sizeBytes,
      folders: 0,
      files: 1,
    });
  setSize("large", 300);
  setSize("empty", 0);
  render(
    <QueryClientProvider client={queryClient}>
      <FolderTable />
    </QueryClientProvider>,
  );
  await screen.findByText("large");
  const sizeHeader = () =>
    document.querySelector<HTMLElement>(
      '[role="columnheader"][data-field="sizeBytes"]',
    )!;
  const visibleIds = () =>
    Array.from(document.querySelectorAll<HTMLElement>(".MuiDataGrid-row")).map(
      (row) => row.dataset.id,
    );
  fireEvent.click(sizeHeader());
  await waitFor(() =>
    expect(visibleIds().slice(0, 4)).toEqual([
      "empty",
      "large",
      "pending",
      "file-101",
    ]),
  );
  await waitFor(() =>
    expect(screen.getByText("1–100 of 105")).toBeInTheDocument(),
  );
  fireEvent.click(screen.getByRole("button", { name: "Go to next page" }));
  await waitFor(() =>
    expect(screen.getByText("101–105 of 105")).toBeInTheDocument(),
  );
  expect(visibleIds()[0]).toBe("file-4");
  fireEvent.click(sizeHeader());
  await waitFor(() =>
    expect(visibleIds().slice(0, 4)).toEqual([
      "large",
      "empty",
      "pending",
      "file-0",
    ]),
  );
  const requests = get.mock.calls.length;
  act(() => setSize("pending", 500));
  await waitFor(() =>
    expect(visibleIds().slice(0, 3)).toEqual(["pending", "large", "empty"]),
  );
  expect(get).toHaveBeenCalledTimes(requests);
  document.activeElement?.dispatchEvent(new FocusEvent("blur"));
  fireEvent.keyDown(document.body, { key: "L" });
  await waitFor(() =>
    expect(
      document.activeElement
        ?.closest(".MuiDataGrid-row")
        ?.getAttribute("data-id"),
    ).toBe("large"),
  );
});
