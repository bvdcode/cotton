import { z } from "zod";
import { httpClient, parseValidated } from "./httpClient";
import { restoreOutcomeSchema } from "./schemas/node";
import type { JsonValue } from "../types/json";

export type BatchItem = {
  id: string;
  kind: "Folder" | "File";
  createMissingParents?: boolean;
  overwrite?: boolean;
};

export const toBatchItem = (id: string, kind: "folder" | "file"): BatchItem => {
  switch (kind) {
    case "folder":
      return { id, kind: "Folder" };
    case "file":
      return { id, kind: "File" };
  }
};

const batchResultSchema = z.object({
  id: z.string(),
  kind: z.enum(["Folder", "File"]),
  restoreOutcome: restoreOutcomeSchema.nullable().optional(),
  deleted: z.boolean(),
  failed: z.boolean(),
});

export type BatchItemResult = z.infer<typeof batchResultSchema>;

const batchResultsSchema = z.array(batchResultSchema);

export const RESTORE_BATCH_SIZE = 25;

export const batchItemsApi = {
  restore: async (items: BatchItem[]): Promise<BatchItemResult[]> => {
    if (items.length === 0 || items.length > RESTORE_BATCH_SIZE) {
      throw new RangeError("Invalid restore batch size.");
    }
    const url = "/items/restore";
    const response = await httpClient.post<JsonValue>(
      url,
      { items },
      { timeout: 0 },
    );
    return parseValidated(url, response.data, batchResultsSchema);
  },
  delete: async (
    items: BatchItem[],
    skipTrash: boolean,
  ): Promise<BatchItemResult[]> => {
    const url = "/items/delete";
    const response = await httpClient.post<JsonValue>(
      url,
      { items, skipTrash },
      { timeout: 0 },
    );
    return parseValidated(url, response.data, batchResultsSchema);
  },
};
