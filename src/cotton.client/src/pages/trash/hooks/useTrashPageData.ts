import { useMemo } from "react";
import { InterfaceLayoutType } from "@shared/api/layoutsApi";
import {
  useTrashChildrenQuery,
  useTrashNodeMetaQuery,
  useTrashRootQuery,
} from "@shared/api/queries/trash";

interface UseTrashPageDataParams {
  routeNodeId?: string;
  layoutType: InterfaceLayoutType;
  loadErrorText: string;
}

const useTrashLocation = (routeNodeId?: string) => {
  const isTrashRoot = !routeNodeId;
  const rootQuery = useTrashRootQuery(isTrashRoot);
  const nodeId = routeNodeId ?? rootQuery.data?.id ?? null;
  const nodeMetaQuery = useTrashNodeMetaQuery(nodeId, {
    isRoot: isTrashRoot,
    enabled: Boolean(nodeId),
  });
  const currentNode =
    nodeMetaQuery.data?.node ?? (isTrashRoot ? (rootQuery.data ?? null) : null);
  const ancestors = useMemo(
    () => (isTrashRoot ? [] : (nodeMetaQuery.data?.ancestors ?? [])),
    [isTrashRoot, nodeMetaQuery.data?.ancestors],
  );

  return {
    nodeId,
    currentNode,
    ancestors,
    rootQuery,
    nodeMetaQuery,
    isTrashRoot,
  };
};

export const useTrashPageData = ({
  routeNodeId,
  layoutType,
  loadErrorText,
}: UseTrashPageDataParams) => {
  const {
    nodeId,
    currentNode,
    ancestors,
    rootQuery,
    nodeMetaQuery,
    isTrashRoot,
  } = useTrashLocation(routeNodeId);
  const childrenQuery = useTrashChildrenQuery({
    nodeId,
    isRoot: isTrashRoot,
    enabled: layoutType !== InterfaceLayoutType.List && Boolean(nodeId),
  });
  const content = childrenQuery.data?.content;
  const loading =
    (isTrashRoot && rootQuery.isPending) ||
    (Boolean(nodeId) && nodeMetaQuery.isPending) ||
    (layoutType !== InterfaceLayoutType.List &&
      Boolean(nodeId) &&
      childrenQuery.isPending);
  const error =
    rootQuery.isError || nodeMetaQuery.isError || childrenQuery.isError
      ? loadErrorText
      : null;

  return { nodeId, currentNode, ancestors, content, loading, error };
};
