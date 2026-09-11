import { useQuery, type QueryClient } from "@tanstack/react-query";
import { layoutsApi, type NodeDto } from "../layoutsApi";
import { nodesApi, type NodeContentDto } from "../nodesApi";
import { queryKeys } from "./queryKeys";

const TILES_PAGE = 1;
const TILES_PAGE_SIZE = 1_000_000;

interface TrashNodeMeta {
  node: NodeDto;
  ancestors: NodeDto[];
}

export const useTrashRootQuery = (enabled = true) =>
  useQuery<NodeDto>({
    queryKey: queryKeys.trash.root(),
    queryFn: () => layoutsApi.resolve({ nodeType: "trash" }),
    enabled,
  });

export const useTrashNodeMetaQuery = (
  nodeId: string | null | undefined,
  options: { isRoot?: boolean; enabled?: boolean } = {},
) => {
  const { isRoot = false, enabled = true } = options;

  return useQuery<TrashNodeMeta>({
    queryKey: queryKeys.trash.meta(nodeId ?? ""),
    queryFn: async () => {
      if (!nodeId) {
        throw new Error("Trash node metadata requires a node id");
      }
      const [node, ancestors] = await Promise.all([
        nodesApi.getNode(nodeId),
        isRoot
          ? Promise.resolve<NodeDto[]>([])
          : nodesApi.getAncestors(nodeId, { nodeType: "trash" }),
      ]);

      return { node, ancestors };
    },
    enabled: enabled && !!nodeId,
  });
};

export const useTrashChildrenQuery = (options: {
  nodeId: string | null | undefined;
  isRoot: boolean;
  page?: number;
  pageSize?: number;
  enabled?: boolean;
}) => {
  const {
    nodeId,
    isRoot,
    page = TILES_PAGE,
    pageSize = TILES_PAGE_SIZE,
    enabled = true,
  } = options;
  const depth = isRoot ? 1 : 0;

  return useQuery<{ content: NodeContentDto; totalCount: number }>({
    queryKey: queryKeys.trash.children.page(nodeId ?? "", {
      page,
      pageSize,
      depth,
    }),
    queryFn: () => {
      if (!nodeId) {
        throw new Error("Trash children query requires a node id");
      }
      return nodesApi.getChildren(nodeId, {
        nodeType: "trash",
        page,
        pageSize,
        depth,
      });
    },
    enabled: enabled && !!nodeId,
  });
};

export const invalidateTrashChildren = (
  queryClient: QueryClient,
  nodeId: string,
): Promise<void> =>
  queryClient.invalidateQueries({
    queryKey: queryKeys.trash.children.all(nodeId),
  });

export const clearTrashCaches = (queryClient: QueryClient): void => {
  queryClient.removeQueries({ queryKey: queryKeys.trash.all() });
};
