import { fetchAllNodeChildren } from "../api/nodeChildren";
import { nodesApi, type SiblingNameLookupDto } from "../api/nodesApi";
import { requireMasterKey, useVault } from "./vault";
import { applyDisplayMetaToNodes } from "./folderDisplayMeta";
import { getFileNameKey, normalizeFileName } from "../utils/fileNameUtils";

export async function lookupDisplayNames(
  nodeId: string,
  names: string[],
  encrypted: boolean,
): Promise<SiblingNameLookupDto> {
  if (!encrypted && !useVault.getState().isUnlocked) {
    return nodesApi.lookupSiblingNames(
      nodeId,
      names.map(normalizeFileName),
      true,
    );
  }
  const key = requireMasterKey();
  const { content } = await fetchAllNodeChildren(nodeId);
  const nodes = await applyDisplayMetaToNodes(content.nodes);
  if (requireMasterKey() !== key) {
    throw new Error("The encryption key changed during the name lookup.");
  }
  return {
    nodes,
    files: content.files,
    takenNameKeys: [...nodes, ...content.files].map((item) =>
      getFileNameKey(item.name),
    ),
  };
}
