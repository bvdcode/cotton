import type { NodeContentDto } from "../api/nodesApi";
import { applyDisplayMetaToFiles } from "./displayMeta";
import { applyDisplayMetaToNodes } from "./folderDisplayMeta";
import { useDisplayProjection } from "./useDisplayProjection";

export async function projectFolderContent(
  source: NodeContentDto | undefined,
): Promise<NodeContentDto | undefined> {
  if (!source) {
    return source;
  }
  const [files, nodes] = await Promise.all([
    applyDisplayMetaToFiles(source.files),
    applyDisplayMetaToNodes(source.nodes),
  ]);
  return { ...source, files, nodes };
}

export function useDecryptedFolderContent(source: NodeContentDto | undefined) {
  return useDisplayProjection(source, projectFolderContent);
}
