import { nodesApi, type NodeResponse } from "../api/nodesApi";

export const FOLDER_SCAN_PAGE_SIZE = 250;

export interface FolderScanPage {
  response: NodeResponse;
  pageSize: number;
}

export async function* readFolderPages(
  nodeId: string,
  signal?: AbortSignal,
  firstPage?: FolderScanPage,
  readPage: typeof nodesApi.getChildren = nodesApi.getChildren,
): AsyncGenerator<NodeResponse> {
  const pageSize = firstPage?.pageSize ?? FOLDER_SCAN_PAGE_SIZE;
  let seen = 0;
  for (let page = 1; ; page += 1) {
    signal?.throwIfAborted();
    const response =
      page === 1 && firstPage
        ? firstPage.response
        : await readPage(nodeId, {
            page,
            pageSize,
            ...(signal ? { signal } : {}),
          });
    signal?.throwIfAborted();
    yield response;
    const count = response.content.nodes.length + response.content.files.length;
    seen += count;
    if (count === 0 || seen >= response.totalCount) {
      return;
    }
  }
}
