import type { InfiniteData, QueryClient } from "@tanstack/react-query";
import type { NodeResponse } from "../nodesApi";
import { queryKeys } from "./queryKeys";

export function updateFolderFilePreview(
  queryClient: QueryClient,
  nodeId: string,
  fileId: string,
  previewHashEncryptedHex: string,
): void {
  const updatePage = (page: NodeResponse): NodeResponse => {
    const index = page.content.files.findIndex(
      (file) => file.id === fileId && file.nodeId === nodeId,
    );
    const file = page.content.files[index];
    if (!file || file.previewHashEncryptedHex === previewHashEncryptedHex) {
      return page;
    }

    const files = page.content.files.slice();
    files[index] = { ...file, previewHashEncryptedHex };
    return { ...page, content: { ...page.content, files } };
  };

  const queryKey = queryKeys.nodeChildren.all(nodeId);
  queryClient.setQueriesData<NodeResponse>(
    {
      queryKey,
      predicate: (query) =>
        query.queryKey[3] === "overview" || query.queryKey[3] === "page",
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
