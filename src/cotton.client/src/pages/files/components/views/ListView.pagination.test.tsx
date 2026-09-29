import { QueryClientProvider } from "@tanstack/react-query";
import {
  cleanup,
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
  vi.spyOn(nodesApi, "getChildren").mockImplementation(async (_, options) => ({
    totalCount: 201,
    content: {
      ...createFolder(),
      nodes: [],
      files: [createFile({ name: `page-${options?.page}.txt` })],
      stats: { folders: 0, files: 201, encryptedFiles: 0, sizeBytes: 201 },
    },
  }));
  render(
    <QueryClientProvider client={queryClient}>
      <FolderTable />
    </QueryClientProvider>,
  );
  await screen.findByText("page-1.txt");
  fireEvent.click(screen.getByRole("button", { name: "Go to next page" }));
  await screen.findByText("page-2.txt");
  await waitFor(() =>
    expect(screen.getByText("101–200 of 201")).toBeInTheDocument(),
  );
  expect(
    screen.getByRole("button", { name: "Go to previous page" }),
  ).toBeEnabled();
});
