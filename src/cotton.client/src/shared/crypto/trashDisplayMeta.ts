import { z } from "zod";
import type { NodeContentDto } from "../api/nodesApi";
import { applyDisplayMetaToNodes } from "./folderDisplayMeta";
import { projectFolderContent } from "./useDecryptedFolderContent";

const parentsSchema = z.array(
  z.object({
    name: z.string(),
    metadata: z.record(z.string(), z.string()).nullable().optional(),
  }),
);

async function projectRestorePath<
  T extends { metadata?: Record<string, string> | null },
>(item: T): Promise<T> {
  const snapshot = item.metadata?.originalParentsV1;
  if (!snapshot) {
    return item;
  }
  const parents = parentsSchema.parse(JSON.parse(snapshot));
  const display = await applyDisplayMetaToNodes(parents);
  return {
    ...item,
    metadata: {
      ...item.metadata,
      originalParentPath: display
        .slice(1)
        .map((parent) => parent.name)
        .join("/"),
    },
  };
}

export async function projectTrashContent(
  source: NodeContentDto | undefined,
): Promise<NodeContentDto | undefined> {
  const content = await projectFolderContent(source);
  if (!content) {
    return content;
  }
  const [nodes, files] = await Promise.all([
    Promise.all(content.nodes.map(projectRestorePath)),
    Promise.all(content.files.map(projectRestorePath)),
  ]);
  return { ...content, nodes, files };
}
