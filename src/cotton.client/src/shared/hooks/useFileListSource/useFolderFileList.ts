import { useDeferredValue, useMemo } from "react";
import { useNodesStore } from "../../store/nodesStore";
import { useAuthStore } from "../../store/authStore";
import { useContentTiles } from "../useContentTiles";
import type { FileListSource } from "../../types/fileListSource";
import { InterfaceLayoutType } from "../../api/layoutsApi";
import type { NodeContentDto } from "../../api/nodesApi";

interface UseFolderFileListOptions {
  nodeId: string | null;
  layoutType: InterfaceLayoutType;
  listContent?: NodeContentDto | null;
  deferContent?: boolean;
}

export const useFolderFileList = ({
  nodeId,
  layoutType,
  listContent,
  deferContent = false,
}: UseFolderFileListOptions): FileListSource => {
  const currentUserId = useAuthStore((s) => s.user?.id ?? null);
  const cacheOwnerUserId = useNodesStore((s) => s.cacheOwnerUserId);
  const rawContent = useNodesStore((s) =>
    nodeId ? s.contentByNodeId[nodeId] : undefined,
  );
  const loading = useNodesStore((s) => s.loading);
  const error = useNodesStore((s) => s.error);
  const refreshNodeContent = useNodesStore((s) => s.refreshNodeContent);

  const content =
    cacheOwnerUserId === currentUserId ? rawContent : undefined;
  const effectiveContent =
    layoutType === InterfaceLayoutType.List ? (listContent ?? content) : content;

  const deferredContent = useDeferredValue(effectiveContent);
  const visibleContent = deferContent ? deferredContent : effectiveContent;
  const isContentTransitioning =
    deferContent && !!effectiveContent && deferredContent !== effectiveContent;

  const { tiles } = useContentTiles(visibleContent ?? undefined);

  const refresh = useMemo(() => {
    if (!nodeId) return undefined;
    return () => refreshNodeContent(nodeId);
  }, [nodeId, refreshNodeContent]);

  return {
    loading,
    error,
    tiles,
    refresh,
    isContentTransitioning,
  };
};
