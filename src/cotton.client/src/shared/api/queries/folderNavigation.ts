import type { InfiniteData, QueryClient } from "@tanstack/react-query";
import type { NodeDto } from "../layoutsApi";
import type { NodeResponse } from "../nodesApi";
import { queryKeys } from "./queryKeys";

export function findCachedChildFolder(
  client: QueryClient,
  parentId: string,
  nodeId: string,
  userId: string | null,
): NodeDto | undefined {
  if (!userId) {
    return undefined;
  }
  const findNode = (page: NodeResponse) =>
    page.content.nodes.find(
      (node) => node.id === nodeId && node.parentId === parentId,
    );
  const pages = client.getQueriesData<NodeResponse>({
    queryKey: [...queryKeys.nodeChildren.all(parentId), userId],
    predicate: ({ queryKey }) =>
      queryKey[3] === "overview" ||
      queryKey[3] === "page" ||
      queryKey[3] === "complete",
  });
  for (const [, page] of pages) {
    const node = page && findNode(page);
    if (node) {
      return node;
    }
  }
  const tiles = client.getQueryData<InfiniteData<NodeResponse, number>>(
    queryKeys.nodeChildren.tiles(parentId, userId),
  );
  for (const page of tiles?.pages ?? []) {
    const node = findNode(page);
    if (node) {
      return node;
    }
  }
  return undefined;
}
