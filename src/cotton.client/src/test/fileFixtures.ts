import type { NodeDto } from "../shared/api/layoutsApi";
import type { NodeFileManifestDto } from "../shared/api/nodesApi";

const createdAt = "2026-05-17T00:00:00Z";

export const createFolder = (overrides: Partial<NodeDto> = {}): NodeDto => ({
  id: "folder-1",
  name: "Folder",
  layoutId: "layout-1",
  parentId: null,
  createdAt,
  updatedAt: createdAt,
  metadata: {},
  ...overrides,
});

export const createFile = (
  overrides: Partial<NodeFileManifestDto> = {},
): NodeFileManifestDto => ({
  id: "file-1",
  name: "file.txt",
  nodeId: "folder-1",
  ownerId: "user-1",
  createdAt,
  updatedAt: createdAt,
  contentType: "text/plain",
  sizeBytes: 1,
  metadata: {},
  ...overrides,
});
