import { useMemo } from "react";
import { useTrashStore } from "../../store/trashStore";
import { useContentTiles } from "../useContentTiles";
import type { FileListSource } from "../../types/fileListSource";
import { InterfaceLayoutType } from "../../api/layoutsApi";
import type { NodeContentDto } from "../../api/nodesApi";

interface UseTrashFileListOptions {
  nodeId: string | null;
  layoutType: InterfaceLayoutType;
  listContent?: NodeContentDto | null;
}

export const useTrashFileList = ({
  nodeId,
  layoutType,
  listContent,
}: UseTrashFileListOptions): FileListSource => {
  const { contentByNodeId, loading, error, refreshNodeContent } = useTrashStore();

  const content = nodeId ? contentByNodeId[nodeId] : undefined;
  const effectiveContent =
    layoutType === InterfaceLayoutType.List ? (listContent ?? content) : content;

  const { tiles } = useContentTiles(effectiveContent ?? undefined);

  const refresh = useMemo(() => {
    if (!nodeId) return undefined;
    return () => refreshNodeContent(nodeId);
  }, [nodeId, refreshNodeContent]);

  return {
    loading,
    error,
    tiles,
    refresh,
  };
};
