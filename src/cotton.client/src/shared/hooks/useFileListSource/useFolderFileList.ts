import { useDeferredValue } from "react";
import { useContentTiles } from "../useContentTiles";
import type { FileListSource } from "../../types/fileListSource";
import type { NodeContentDto } from "../../api/nodesApi";

interface UseFolderFileListOptions {
  content: NodeContentDto | undefined;
  loading: boolean;
  error: string | null;
  refresh: () => void;
  deferContent?: boolean;
}

export const useFolderFileList = ({
  content,
  loading,
  error,
  refresh,
  deferContent = false,
}: UseFolderFileListOptions): FileListSource => {
  const deferredContent = useDeferredValue(content);
  const visibleContent = deferContent ? deferredContent : content;
  const isContentTransitioning =
    deferContent && !!content && deferredContent !== content;

  const { tiles } = useContentTiles(visibleContent ?? undefined, { sortMode: "server" });

  return {
    loading,
    error,
    tiles,
    refresh,
    isContentTransitioning,
    hasContent: !!visibleContent,
  };
};
