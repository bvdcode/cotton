import type { NodeDto } from "../../../shared/api/layoutsApi";

/**
 * Build breadcrumbs from ancestors and current node
 */
export const buildBreadcrumbs = (
  ancestors: NodeDto[],
  currentNode: NodeDto | null,
): Array<{ id: string; name: string }> => {
  if (!currentNode) return [];
  const chain = [...ancestors, currentNode];
  return chain.map((n) => ({ id: n.id, name: n.name }));
};

/**
 * Calculate folder statistics
 */
export const calculateFolderStats = (
  nodes: Array<{ id: string }> | undefined,
  files: Array<{ sizeBytes?: number }> | undefined,
): { folders: number; files: number; sizeBytes: number } => {
  const folders = nodes?.length ?? 0;
  const filesCount = files?.length ?? 0;
  const sizeBytes = (files ?? []).reduce(
    (sum, file) => sum + (file.sizeBytes ?? 0),
    0,
  );
  return { folders, files: filesCount, sizeBytes };
};
