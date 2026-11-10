import type { NodeDto } from "../shared/api/layoutsApi";
import type { NodeContentDto, NodeResponse } from "../shared/api/nodesApi";
import { queryClient } from "../shared/api/queries/queryClient";
import { queryKeys } from "../shared/api/queries/queryKeys";
import type { NodeNavigation } from "../shared/api/queries/nodeNavigation";
import { useNodesStore } from "../shared/store/nodesStore";
import { createFolder } from "./fileFixtures";

const owner = () => useNodesStore.getState().cacheOwnerUserId ?? "user-1";

export function seedFolderListing(
  nodeId: string,
  content: NodeContentDto,
  userId = owner(),
): void {
  useNodesStore.setState({ cacheOwnerUserId: userId });
  queryClient.setQueryData<NodeResponse>(
    queryKeys.nodeChildren.complete(nodeId, userId),
    {
      content: { ...content, node: { ...content.node, id: nodeId } },
      totalCount: content.nodes.length + content.files.length,
    },
  );
}

export function seedNodeNavigation(
  node: NodeDto | null,
  ancestors: NodeDto[] = [],
  userId = owner(),
): void {
  useNodesStore.setState({ currentNodeId: node?.id ?? null });
  if (node) {
    useNodesStore.setState({ cacheOwnerUserId: userId });
    queryClient.setQueryData<NodeNavigation>(
      queryKeys.nodeNavigation.view(node.id, userId),
      { node, ancestors },
    );
  }
}

export function seedNodePath(
  nodeId: string,
  ancestors: NodeDto[],
  userId = owner(),
): void {
  queryClient.setQueryData<NodeNavigation>(
    queryKeys.nodeNavigation.view(nodeId, userId),
    {
      node: createFolder({ id: nodeId }),
      ancestors,
    },
  );
}

export function getFolderListing(
  nodeId: string,
  userId = owner(),
): NodeContentDto | undefined {
  return queryClient.getQueryData<NodeResponse>(
    queryKeys.nodeChildren.complete(nodeId, userId),
  )?.content;
}

export function getCurrentNode(): NodeDto | null {
  const state = useNodesStore.getState();
  return state.currentNodeId
    ? (queryClient.getQueryData<NodeNavigation>(
        queryKeys.nodeNavigation.view(state.currentNodeId, owner()),
      )?.node ?? null)
    : null;
}

export function getCurrentAncestors(): NodeDto[] {
  const state = useNodesStore.getState();
  return state.currentNodeId
    ? (queryClient.getQueryData<NodeNavigation>(
        queryKeys.nodeNavigation.view(state.currentNodeId, owner()),
      )?.ancestors ?? [])
    : [];
}
