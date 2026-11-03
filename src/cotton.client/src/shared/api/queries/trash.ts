import {
  useDecryptedNode,
  useDecryptedNodes,
} from "../../crypto/useDecryptedNodes";
import { useDisplayProjection } from "../../crypto/useDisplayProjection";
import { projectTrashContent } from "../../crypto/trashDisplayMeta";
import { useQuery, type QueryClient } from "@tanstack/react-query";
import { layoutsApi, type NodeDto } from "../layoutsApi";
import { nodesApi, type NodeResponse } from "../nodesApi";
import { fetchAllRawNodeChildren } from "../nodeChildren";
import { queryKeys } from "./queryKeys";
import { FOLDER_PAGE_SIZE } from "./folderPageSize";

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

  const query = useQuery<TrashNodeMeta>({
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
  const node = useDecryptedNode(query.data?.node ?? null);
  const ancestors = useDecryptedNodes(query.data?.ancestors ?? EMPTY_NODES);
  return {
    ...query,
    data: query.data && node ? { node, ancestors } : undefined,
  };
};

const EMPTY_NODES: NodeDto[] = [];

export const useTrashChildrenQuery = (options: {
  nodeId: string | null | undefined;
  isRoot: boolean;
  page?: number;
  pageSize?: number;
  enabled?: boolean;
}) => {
  const { nodeId, isRoot, page = 1, pageSize, enabled = true } = options;
  const depth = isRoot ? 1 : 0;

  const query = useQuery<NodeResponse>({
    queryKey:
      pageSize === undefined
        ? queryKeys.trash.children.complete(nodeId ?? "", depth)
        : queryKeys.trash.children.page(nodeId ?? "", {
            page,
            pageSize,
            depth,
          }),
    queryFn: ({ signal }) => {
      if (!nodeId) {
        throw new Error("Trash children query requires a node id");
      }
      if (pageSize === undefined) {
        return fetchAllRawNodeChildren(nodeId, {
          nodeType: "trash",
          depth,
          signal,
          pageSize: FOLDER_PAGE_SIZE,
        });
      }
      return nodesApi.getChildren(nodeId, {
        nodeType: "trash",
        page,
        pageSize,
        depth,
        signal,
      });
    },
    enabled: enabled && !!nodeId,
  });
  const content = useDisplayProjection(
    query.data?.content,
    projectTrashContent,
  );
  return {
    ...query,
    data: query.data && content ? { ...query.data, content } : undefined,
  };
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
