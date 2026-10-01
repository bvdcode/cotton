import type { Guid } from "./layoutsApi";
import type { SiblingNameLookupDto } from "./nodesApi";
import type { JsonValue } from "../types/json";
import { getFileNameKey } from "../utils/fileNameUtils";
import { httpClient, parseValidated } from "./httpClient";
import { siblingNameLookupSchema } from "./schemas/node";

export const SIBLING_NAME_BATCH_SIZE = 1000;

function* nameBatches(names: string[]): Generator<string[]> {
  const groups = new Map<string, string[]>();
  for (const name of names) {
    const key = getFileNameKey(name);
    const group = groups.get(key);
    if (!group) {
      groups.set(key, [name]);
    } else if (group.length === 1) {
      group.push(name);
    }
  }
  let batch: string[] = [];
  for (const group of groups.values()) {
    if (batch.length + group.length > SIBLING_NAME_BATCH_SIZE) {
      yield batch;
      batch = [];
    }
    batch.push(...group);
  }
  if (batch.length > 0) {
    yield batch;
  }
}

export async function lookupSiblingNames(
  nodeId: Guid,
  names: string[],
  includeTakenNamesOnConflict = false,
): Promise<SiblingNameLookupDto> {
  const result: SiblingNameLookupDto = {
    nodes: [],
    files: [],
    takenNameKeys: [],
  };
  const url = `/layouts/nodes/${nodeId}/sibling-names`;
  for (const batch of nameBatches(names)) {
    const response = await httpClient.post<JsonValue>(url, {
      names: batch,
      includeTakenNamesOnConflict:
        includeTakenNamesOnConflict && result.takenNameKeys.length === 0,
    });
    const matches = parseValidated(url, response.data, siblingNameLookupSchema);
    result.nodes.push(...matches.nodes);
    result.files.push(...matches.files);
    if (matches.takenNameKeys.length > 0) {
      result.takenNameKeys = matches.takenNameKeys;
    }
  }
  return result;
}
