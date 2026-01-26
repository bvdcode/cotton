import { useMemo } from "react";
import { useNodesStore } from "../../store/nodesStore";
import { useContentTiles } from "../useContentTiles";
import type { FileListSource } from "../../types/fileListSource";
import { InterfaceLayoutType } from "../../api/layoutsApi";
import type { NodeContentDto } from "../../api/nodesApi";

interface UseFolderFileListOptions {
  nodeId: string | null;
  layoutType: InterfaceLayoutType;
  listContent?: NodeContentDto | null;
}

export const useFolderFileList = ({
  nodeId,
  layoutType,
  listContent,
}: UseFolderFileListOptions): FileListSource => {
  const { contentByNodeId, loading, error, refreshNodeContent } = useNodesStore();

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
