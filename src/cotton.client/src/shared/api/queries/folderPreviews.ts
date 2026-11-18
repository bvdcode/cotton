import type { InfiniteData, QueryClient } from "@tanstack/react-query";
import type { NodeResponse } from "../nodesApi";
import { queryKeys } from "./queryKeys";

export function updateFolderFilePreviews(
  queryClient: QueryClient,
  nodeId: string,
  previews: ReadonlyMap<string, string>,
): void {
  const updatePage = (page: NodeResponse): NodeResponse => {
    let changed = false;
    const files = page.content.files.map((file) => {
      const previewHashEncryptedHex = previews.get(file.id);
      if (
        file.nodeId !== nodeId ||
        previewHashEncryptedHex === undefined ||
        file.previewHashEncryptedHex === previewHashEncryptedHex
      ) {
        return file;
      }
      changed = true;
      return { ...file, previewHashEncryptedHex };
    });
    return changed ? { ...page, content: { ...page.content, files } } : page;
  };

  const queryKey = queryKeys.nodeChildren.all(nodeId);
  queryClient.setQueriesData<NodeResponse>(
    {
      queryKey,
      predicate: (query) =>
        query.queryKey[3] === "overview" ||
        query.queryKey[3] === "page" ||
        query.queryKey[3] === "complete",
    },
    (page) => (page ? updatePage(page) : page),
  );
  queryClient.setQueriesData<InfiniteData<NodeResponse, number>>(
    { queryKey, predicate: (query) => query.queryKey[3] === "tiles" },
    (data) => {
      if (!data) {
        return data;
      }
      const pages = data.pages.map(updatePage);
      return pages.every((page, index) => page === data.pages[index])
        ? data
        : { ...data, pages };
    },
  );
}
