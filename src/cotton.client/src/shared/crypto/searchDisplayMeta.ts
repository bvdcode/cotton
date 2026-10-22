import type { LayoutSearchResultDto } from "../api/layoutsApi";
import { applyDisplayMetaToFiles } from "./displayMeta";
import { applyDisplayMetaToNodes } from "./folderDisplayMeta";

export async function projectSearchResult(
  source: LayoutSearchResultDto | null,
): Promise<LayoutSearchResultDto | null> {
  if (!source) {
    return source;
  }
  const [nodes, files, ancestors] = await Promise.all([
    applyDisplayMetaToNodes(source.nodes),
    applyDisplayMetaToFiles(source.files),
    applyDisplayMetaToNodes(source.ancestors ?? []),
  ]);
  const byId = new Map([...ancestors, ...nodes].map((node) => [node.id, node]));
  const buildPath = (id: string): string | null => {
    const parts: string[] = [];
    const visited = new Set<string>();
    let node = byId.get(id);
    while (node) {
      if (visited.has(node.id)) {
        return null;
      }
      visited.add(node.id);
      parts.push(node.name);
      if (!node.parentId) {
        return `/${parts.reverse().join("/")}`;
      }
      node = byId.get(node.parentId);
    }
    return null;
  };
  const nodePaths = { ...source.nodePaths };
  const filePaths = { ...source.filePaths };
  for (const node of nodes) {
    const path = buildPath(node.id);
    if (path) {
      nodePaths[node.id] = path;
    }
  }
  for (const file of files) {
    const parentPath = buildPath(file.nodeId);
    if (parentPath) {
      filePaths[file.id] = `${parentPath}/${file.name}`;
    }
  }
  return { ...source, nodes, files, ancestors, nodePaths, filePaths };
}
