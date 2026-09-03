import type { NodeDto } from "../../../shared/api/layoutsApi";
import type {
  FileBreadcrumb,
  FileSizeEntry,
  FileListStats,
} from "../../../shared/types/FileListViewTypes";

export const buildBreadcrumbs = (
  ancestors: NodeDto[],
  currentNode: NodeDto | null,
): FileBreadcrumb[] => {
  if (!currentNode) return [];
  const chain = [...ancestors, currentNode];
  return chain.map((n) => ({ id: n.id, name: n.name }));
};

export const calculateFolderStats = (
  nodes: ReadonlyArray<Pick<NodeDto, "id">> | undefined,
  files: ReadonlyArray<FileSizeEntry> | undefined,
): FileListStats => {
  const folders = nodes?.length ?? 0;
  const filesCount = files?.length ?? 0;
  const sizeBytes = (files ?? []).reduce(
    (sum, file) => sum + (file.sizeBytes ?? 0),
    0,
  );
  return { folders, files: filesCount, sizeBytes };
};
