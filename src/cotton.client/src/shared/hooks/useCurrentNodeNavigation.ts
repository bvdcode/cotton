import { useQuery } from "@tanstack/react-query";
import type { NodeDto } from "../api/layoutsApi";
import { folderOverviewOptions } from "../api/queries/folderOverview";
import { nodeNavigationOptions } from "../api/queries/nodeNavigation";
import { queryClient } from "../api/queries/queryClient";
import { useNodesStore } from "../store/nodesStore";

const emptyAncestors: NodeDto[] = [];

export function useCurrentNodeNavigation() {
  const nodeId = useNodesStore((state) => state.currentNodeId);
  const userId = useNodesStore((state) => state.cacheOwnerUserId);
  const navigation = useQuery(
    {
      ...nodeNavigationOptions(nodeId ?? "", userId ?? ""),
      enabled: Boolean(nodeId && userId),
    },
    queryClient,
  ).data;
  const overview = useQuery(
    {
      ...folderOverviewOptions(nodeId ?? "", userId ?? ""),
      enabled: false,
      select: (response) => response.content.node,
    },
    queryClient,
  ).data;
  let node = navigation?.node ?? null;
  if (
    overview &&
    (!node || Date.parse(overview.updatedAt) >= Date.parse(node.updatedAt))
  ) {
    node = overview;
  }
  return {
    currentNode: node,
    ancestors: navigation?.ancestors ?? emptyAncestors,
  };
}
