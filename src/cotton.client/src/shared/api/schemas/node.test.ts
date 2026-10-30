import { describe, expect, it } from "vitest";
import {
  nodeContentSchema,
  nodeDtoSchema,
  nodeFileManifestSchema,
  restoreOutcomeSchema,
} from "./node";

const baseDto = {
  id: "id",
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
};

describe("node api schemas", () => {
  it("normalizes missing node metadata to an empty object", () => {
    const parsed = nodeDtoSchema.parse({
      ...baseDto,
      layoutId: "layout-id",
      parentId: null,
      name: "Folder",
      metadata: null,
    });

    expect(parsed.metadata).toEqual({});
  });

  it("normalizes missing file metadata to an empty object", () => {
    const parsed = nodeFileManifestSchema.parse({
      ...baseDto,
      nodeId: "node-id",
      ownerId: "owner-id",
      name: "track.mp3",
      contentType: "audio/mpeg",
      sizeBytes: 42,
    });

    expect(parsed.metadata).toEqual({});
  });

  it("preserves sync metadata fields from file manifests", () => {
    const parsed = nodeFileManifestSchema.parse({
      ...baseDto,
      nodeId: "node-id",
      fileManifestId: "manifest-id",
      originalNodeFileId: "original-file-id",
      ownerId: "owner-id",
      name: "track.mp3",
      contentType: "audio/mpeg",
      sizeBytes: 42,
      contentHash: "abc123",
      eTag: "sha256-abc123",
    });

    expect(parsed.fileManifestId).toBe("manifest-id");
    expect(parsed.originalNodeFileId).toBe("original-file-id");
    expect(parsed.contentHash).toBe("abc123");
    expect(parsed.eTag).toBe("sha256-abc123");
  });

  it("validates node content envelopes", () => {
    const parsed = nodeContentSchema.parse({
      ...baseDto,
      nodes: [
        {
          ...baseDto,
          id: "node-id",
          layoutId: "layout-id",
          parentId: null,
          name: "Folder",
          metadata: { color: "blue" },
        },
      ],
      files: [],
    });

    expect(parsed.nodes[0].metadata).toEqual({ color: "blue" });
  });

  it("validates restore outcomes with restored files", () => {
    const parsed = restoreOutcomeSchema.parse({
      status: "Restored",
      restoredFile: {
        ...baseDto,
        nodeId: "node-id",
        ownerId: "owner-id",
        name: "file.txt",
        contentType: "text/plain",
        sizeBytes: 12,
        metadata: {},
      },
    });

    expect(parsed.status).toBe("Restored");
    expect(parsed.restoredFile?.name).toBe("file.txt");
  });
});
