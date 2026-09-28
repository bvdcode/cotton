import { z } from "zod";

export const baseDtoSchema = z.object({
  id: z.string(),
  createdAt: z.string(),
  updatedAt: z.string(),
});

const metadataSchema = z
  .record(z.string(), z.string())
  .nullable()
  .optional()
  .transform((metadata) => metadata ?? {});

export const nodeDtoSchema = baseDtoSchema.extend({
  layoutId: z.string(),
  parentId: z.string().nullable(),
  name: z.string(),
  metadata: metadataSchema,
});

export const nodeFileManifestSchema = baseDtoSchema.extend({
  nodeId: z.string(),
  fileManifestId: z.string().optional(),
  originalNodeFileId: z.string().optional(),
  ownerId: z.string(),
  name: z.string(),
  contentType: z.string(),
  sizeBytes: z.number(),
  contentHash: z.string().optional(),
  eTag: z.string().optional(),
  metadata: metadataSchema,
  requiresVideoTranscoding: z.boolean().optional().default(false),
  previewHashEncryptedHex: z.string().nullable().optional(),
});

export const folderStatsSchema = z.object({
  folders: z.number(),
  files: z.number(),
  encryptedFiles: z.number(),
  sizeBytes: z.number(),
});

export const nodeContentSchema = baseDtoSchema.extend({
  nodes: z.array(nodeDtoSchema),
  files: z.array(nodeFileManifestSchema),
  stats: folderStatsSchema.nullable().optional(),
});

export const siblingNameLookupSchema = z.object({
  nodes: z.array(nodeDtoSchema),
  files: z.array(baseDtoSchema.extend({ name: z.string() })),
  takenNameKeys: z.array(z.string()),
});

export const restoreStatusSchema = z.enum([
  "Restored",
  "ParentMissing",
  "Conflict",
  "NotRestorable",
]);

export const restoreConflictKindSchema = z.enum(["Folder", "File"]);

export const restoreOutcomeSchema = z.object({
  status: restoreStatusSchema,
  originalParentPath: z.string().nullable().optional(),
  missingPath: z.string().nullable().optional(),
  conflictKind: restoreConflictKindSchema.nullable().optional(),
  conflictName: z.string().nullable().optional(),
  restoredNode: nodeDtoSchema.nullable().optional(),
  restoredFile: nodeFileManifestSchema.nullable().optional(),
  reason: z.string().nullable().optional(),
});
