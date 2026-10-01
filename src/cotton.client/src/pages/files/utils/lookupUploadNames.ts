import { fetchAllNodeChildren } from "../../../shared/api/nodeChildren";
import {
  nodesApi,
  type SiblingNameLookupDto,
} from "../../../shared/api/nodesApi";
import { requireMasterKey } from "../../../shared/crypto/vault";
import {
  getFileNameKey,
  normalizeFileName,
} from "../../../shared/utils/fileNameUtils";

export async function lookupUploadNames(
  nodeId: string,
  names: string[],
  encrypted: boolean,
): Promise<SiblingNameLookupDto> {
  if (!encrypted) {
    return nodesApi.lookupSiblingNames(
      nodeId,
      names.map(normalizeFileName),
      true,
    );
  }
  const key = requireMasterKey();
  const { content } = await fetchAllNodeChildren(nodeId);
  if (requireMasterKey() !== key) {
    throw new Error("The encryption key changed during the name lookup.");
  }
  return {
    nodes: content.nodes,
    files: content.files,
    takenNameKeys: [...content.nodes, ...content.files].map((item) =>
      getFileNameKey(item.name),
    ),
  };
}
