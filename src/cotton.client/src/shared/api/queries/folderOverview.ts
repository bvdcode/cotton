import { queryOptions, type QueryClient } from "@tanstack/react-query";
import { nodesApi, type NodeResponse } from "../nodesApi";
import type { NodeDto } from "../layoutsApi";
import { FOLDER_PAGE_SIZE } from "./folderPageSize";
import { queryKeys } from "./queryKeys";

export async function fetchFolderOverview(
  nodeId: string,
  pageSize: number,
): Promise<NodeResponse> {
  const response = await nodesApi.getChildren(nodeId, {
    page: 1,
    pageSize,
    includeStats: true,
  });
  if (!response.content.stats) {
    throw new Error("Folder statistics are missing");
  }
  return response;
}

export function folderOverviewOptions(nodeId: string, userId: string) {
  return queryOptions({
    queryKey: queryKeys.nodeChildren.overview(nodeId, userId, FOLDER_PAGE_SIZE),
    queryFn: () => fetchFolderOverview(nodeId, FOLDER_PAGE_SIZE),
  });
}

export function ensureFolderOverview(
  client: QueryClient,
  nodeId: string,
  userId: string,
): Promise<NodeResponse> {
  const options = folderOverviewOptions(nodeId, userId);
  const query = client.getQueryState(options.queryKey);
  if (query?.fetchStatus === "idle") {
    if (query.status === "error") {
      return Promise.reject(query.error);
    }
    if (query.data && !query.isInvalidated) {
      return Promise.resolve(query.data);
    }
  }
  return client.fetchQuery({ ...options, staleTime: 0 });
}

export function updateFolderOverviewNodes(
  client: QueryClient,
  nodes: readonly NodeDto[],
  userId: string,
): void {
  const updated = new Map(nodes.map((node) => [node.id, node]));
  client.setQueriesData<NodeResponse>(
    {
      queryKey: ["nodeChildren"],
      predicate: ({ queryKey }) =>
        queryKey[2] === userId &&
        queryKey[3] === "overview" &&
        typeof queryKey[1] === "string" &&
        updated.has(queryKey[1]),
    },
    (response) => {
      if (!response) {
        return response;
      }
      const node = updated.get(response.content.node.id);
      if (
        !node ||
        Date.parse(node.updatedAt) < Date.parse(response.content.node.updatedAt)
      ) {
        return response;
      }
      return { ...response, content: { ...response.content, node } };
    },
  );
}
