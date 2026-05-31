import { useCallback } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  invalidateTrashChildren,
  useTrashChildrenQuery,
} from "../../api/queries/trash";
import { useContentTiles } from "../useContentTiles";
import type { FileListSource } from "../../types/fileListSource";
import { InterfaceLayoutType } from "../../api/layoutsApi";
import type { NodeContentDto } from "../../api/nodesApi";

interface UseTrashFileListOptions {
  nodeId: string | null;
  isRoot: boolean;
  layoutType: InterfaceLayoutType;
  listContent?: NodeContentDto | null;
}

export const useTrashFileList = ({
  nodeId,
  isRoot,
  layoutType,
  listContent,
}: UseTrashFileListOptions): FileListSource => {
  const queryClient = useQueryClient();
  const childrenQuery = useTrashChildrenQuery({
    nodeId,
    isRoot,
    enabled: layoutType !== InterfaceLayoutType.List && !!nodeId,
  });
  const effectiveContent =
    layoutType === InterfaceLayoutType.List
      ? (listContent ?? childrenQuery.data?.content)
      : childrenQuery.data?.content;

  const { tiles } = useContentTiles(effectiveContent ?? undefined, {
    sortMode: "updatedAtDesc",
  });

  const refresh = useCallback(() => {
    if (!nodeId) return;
    return invalidateTrashChildren(queryClient, nodeId);
  }, [nodeId, queryClient]);

  const loading =
    layoutType === InterfaceLayoutType.List
      ? false
      : childrenQuery.isPending && !!nodeId;
  const error = childrenQuery.isError ? "Failed to load trash contents" : null;

  return {
    loading,
    error,
    tiles,
    refresh: nodeId ? refresh : undefined,
    isContentTransitioning: childrenQuery.isFetching && !!effectiveContent,
    hasContent: !!effectiveContent,
  };
};
