import { useMemo } from "react";
import type { NodeContentDto } from "@shared/api/nodesApi";
import { useDecryptedFolderContent } from "@shared/crypto/useDecryptedFolderContent";
import { compareNames } from "@shared/utils/compareNames";
import { LIST_PAGE_SIZE } from "./useFolderListPage";

interface FolderContentOrderOptions {
  naturalOrder: boolean;
  descending: boolean;
  pageOffset?: number;
}

export function useFolderContentOrder(
  source: NodeContentDto | undefined,
  { naturalOrder, descending, pageOffset }: FolderContentOrderOptions,
) {
  const visibleSource = useMemo(
    () =>
      source && !naturalOrder && pageOffset !== undefined
        ? sliceListPage(source, pageOffset)
        : source,
    [source, naturalOrder, pageOffset],
  );
  const display = useDecryptedFolderContent(visibleSource);
  const ordered = useMemo(() => {
    if (!display || !naturalOrder) {
      return display;
    }
    const direction = descending ? -1 : 1;
    const compare = (left: { name: string }, right: { name: string }) =>
      direction * compareNames(left.name, right.name);
    const sorted: NodeContentDto = {
      ...display,
      nodes: display.nodes.slice().sort(compare),
      files: display.files.slice().sort(compare),
    };
    return sorted;
  }, [display, naturalOrder, descending]);
  return useMemo(
    () =>
      ordered && naturalOrder && pageOffset !== undefined
        ? sliceListPage(ordered, pageOffset)
        : ordered,
    [ordered, naturalOrder, pageOffset],
  );
}

function sliceListPage(
  content: NodeContentDto,
  offset: number,
): NodeContentDto {
  const end = offset + LIST_PAGE_SIZE;
  return {
    ...content,
    nodes: content.nodes.slice(offset, end),
    files: content.files.slice(
      Math.max(0, offset - content.nodes.length),
      Math.max(0, end - content.nodes.length),
    ),
  };
}
