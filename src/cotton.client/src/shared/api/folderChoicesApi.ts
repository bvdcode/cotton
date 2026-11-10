import { z } from "zod";
import { getValidated } from "./httpClient";
import { nodeDtoSchema } from "./schemas/node";

const folderChoicesSchema = z.object({
  current: nodeDtoSchema,
  folders: z.array(nodeDtoSchema),
  hasMore: z.boolean(),
});
export const getFolderChoices = (
  nodeId: string,
  page: number,
  signal: AbortSignal,
) =>
  getValidated(`/layouts/nodes/${nodeId}/folders`, folderChoicesSchema, {
    params: { page },
    signal,
  });
