import type { NodeContentDto } from "../../../shared/api/nodesApi";
import { nextAvailableName } from "./fileNameUtils";

/**
 * Resolve upload conflicts by checking which files have duplicate names
 * and prompting the user to rename them.
 * Returns a list of files ready for upload (original or renamed).
 */
export async function resolveUploadConflicts(
  files: File[],
  content: NodeContentDto,
  confirmRename: (newName: string) => Promise<{ confirmed: boolean }>,
): Promise<File[]> {
  const takenLower = new Set<string>([
    ...content.nodes.map((n) => n.name.toLowerCase()),
    ...content.files.map((f) => f.name.toLowerCase()),
  ]);

  const resolved: File[] = [];

  for (const file of files) {
    const desiredNameLower = file.name.toLowerCase();
    if (!takenLower.has(desiredNameLower)) {
      takenLower.add(desiredNameLower);
      resolved.push(file);
      continue;
    }

    const newName = nextAvailableName(file.name, takenLower);
    const result = await confirmRename(newName);
    if (!result.confirmed) {
      continue;
    }

    const renamed = new File([file], newName, {
      type: file.type,
      lastModified: file.lastModified,
    });
    takenLower.add(newName.toLowerCase());
    resolved.push(renamed);
  }

  return resolved;
}
