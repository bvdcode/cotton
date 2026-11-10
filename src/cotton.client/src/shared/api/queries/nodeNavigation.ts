import { queryOptions, type QueryClient } from "@tanstack/react-query";
import { nodesApi, type NodeResponse } from "../nodesApi";
import { layoutsApi, type NodeDto } from "../layoutsApi";
import { findCachedChildFolder } from "./folderNavigation";
import { queryClient } from "./queryClient";
import { queryKeys } from "./queryKeys";
import type { InfiniteData } from "@tanstack/react-query";

export const NODE_NAVIGATION_STALE_TIME = 30_000;
const ROOT_NODE_STALE_TIME = 600_000;

export interface NodeNavigation {
  node: NodeDto;
  ancestors: NodeDto[];
}

export function cachedFolderPages(
  client: QueryClient,
  userId: string,
): NodeResponse[] {
  const pages = client
    .getQueriesData<NodeResponse>({
      queryKey: ["nodeChildren"],
      predicate: ({ queryKey }) =>
        queryKey[2] === userId &&
        (queryKey[3] === "overview" ||
          queryKey[3] === "page" ||
          queryKey[3] === "complete"),
    })
    .flatMap(([, page]) => (page ? [page] : []));
  const tiles = client
    .getQueriesData<InfiniteData<NodeResponse, number>>({
      queryKey: ["nodeChildren"],
      predicate: ({ queryKey }) =>
        queryKey[2] === userId && queryKey[3] === "tiles",
    })
    .flatMap(([, data]) => data?.pages ?? []);
  return [...pages, ...tiles];
}

export function findCachedNode(
  client: QueryClient,
  userId: string,
  nodeId: string,
): NodeDto | undefined {
  const views = client.getQueriesData<NodeNavigation>({
    queryKey: queryKeys.nodeNavigation.all(),
    predicate: ({ queryKey }) => queryKey[2] === userId,
  });
  for (const [, view] of views) {
    const node =
      view?.node.id === nodeId
        ? view.node
        : view?.ancestors.find((item) => item.id === nodeId);
    if (node) {
      return node;
    }
  }
  for (const page of cachedFolderPages(client, userId)) {
    if (page.content.node.id === nodeId) {
      return page.content.node;
    }
    const node = page.content.nodes.find((item) => item.id === nodeId);
    if (node) {
      return node;
    }
  }
  return undefined;
}

export function nodeNavigationOptions(
  nodeId: string,
  userId: string,
  force = false,
) {
  return queryOptions({
    queryKey: queryKeys.nodeNavigation.view(nodeId, userId),
    queryFn: async ({ signal }): Promise<NodeNavigation> => {
      if (!force) {
        const views = queryClient.getQueriesData<NodeNavigation>({
          queryKey: queryKeys.nodeNavigation.all(),
          predicate: ({ queryKey }) => queryKey[2] === userId,
        });
        for (const [key, view] of views) {
          if (!view || queryClient.getQueryState(key)?.isInvalidated) {
            continue;
          }
          const index = view.ancestors.findIndex((node) => node.id === nodeId);
          if (index >= 0) {
            return {
              node: view.ancestors[index],
              ancestors: view.ancestors.slice(0, index),
            };
          }
          const child = findCachedChildFolder(
            queryClient,
            view.node.id,
            nodeId,
            userId,
          );
          if (child) {
            return { node: child, ancestors: [...view.ancestors, view.node] };
          }
        }
      }
      const [node, ancestors] = await Promise.all([
        nodesApi.getNode(nodeId, signal),
        nodesApi.getAncestors(nodeId, { signal }),
      ]);
      return { node, ancestors };
    },
    staleTime: NODE_NAVIGATION_STALE_TIME,
    retry: false,
  });
}

export const rootNodeOptions = (userId: string) =>
  queryOptions({
    queryKey: queryKeys.nodeNavigation.root(userId),
    queryFn: ({ signal }) => layoutsApi.resolve({ signal }),
    staleTime: ROOT_NODE_STALE_TIME,
    retry: false,
  });

export function clearNodeQueries(client: QueryClient): void {
  for (const key of [["nodeChildren"], ["nodeNavigation"], ["nodeRoot"]]) {
    void client.cancelQueries({ queryKey: key });
    client.removeQueries({ queryKey: key });
  }
}
