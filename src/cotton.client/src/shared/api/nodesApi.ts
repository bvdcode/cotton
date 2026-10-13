import { getValidated, httpClient, parseValidated } from "./httpClient";
import type { JsonValue } from "../types/json";
import type { BaseDto, Guid, NodeDto } from "./layoutsApi";
import { readRequiredIntHeader } from "./utils/headerUtils";
import {
  nodeContentSchema,
  nodeDtoSchema,
  folderStatsSchema,
  restoreOutcomeSchema,
} from "./schemas/node";
import { lookupSiblingNames } from "./siblingNames";
import type { DirectoryListingOptions } from "./types/DirectoryListingOptions";
import { z } from "zod";

export interface NodeFileManifestDto extends BaseDto {
  /**
   * Container node id (folder) where this file is located.
   */
  nodeId: Guid;
  fileManifestId?: Guid;
  originalNodeFileId?: Guid;
  ownerId: Guid;
  name: string;
  contentType: string;
  sizeBytes: number;
  contentHash?: string;
  eTag?: string;
  metadata: Record<string, string>;
  requiresVideoTranscoding?: boolean;
  previewHashEncryptedHex?: string | null;
}
export interface NodeResponse {
  content: NodeContentDto;
  totalCount: number;
}

export interface NodeContentDto extends BaseDto {
  nodes: NodeDto[];
  files: NodeFileManifestDto[];
  stats?: FolderStatsDto | null;
}

export interface FileNameMatchDto extends BaseDto {
  name: string;
}

export interface SiblingNameLookupDto {
  nodes: NodeDto[];
  files: FileNameMatchDto[];
  takenNameKeys: string[];
}

export interface FolderStatsDto {
  folders: number;
  files: number;
  encryptedFiles: number;
  sizeBytes: number;
}

export interface CreateNodeRequest {
  parentId: Guid;
  name: string;
}

export interface RenameNodeRequest {
  name: string;
}

export interface MoveNodeRequest {
  parentId: Guid;
  name?: string;
}

export type RestoreStatus =
  "Restored" | "ParentMissing" | "Conflict" | "NotRestorable";

export type RestoreConflictKind = "Folder" | "File";

export interface RestoreOutcomeDto {
  status: RestoreStatus;
  originalParentPath?: string | null;
  missingPath?: string | null;
  conflictKind?: RestoreConflictKind | null;
  conflictName?: string | null;
  restoredNode?: NodeDto | null;
  restoredFile?: NodeFileManifestDto | null;
  reason?: string | null;
}

export interface RestoreOptions {
  createMissingParents?: boolean;
  overwrite?: boolean;
}

const nodeListSchema = z.array(nodeDtoSchema);

export const nodesApi = {
  copyNode: async (
    nodeId: Guid,
    request: MoveNodeRequest,
  ): Promise<NodeDto> => {
    const url = `/layouts/nodes/${nodeId}/copy`;
    const response = await httpClient.post<JsonValue>(url, request);
    return parseValidated(url, response.data, nodeDtoSchema);
  },

  getNode: (nodeId: Guid): Promise<NodeDto> =>
    getValidated(`/layouts/nodes/${nodeId}`, nodeDtoSchema),

  getFolderStats: (nodeId: Guid, recursive = false): Promise<FolderStatsDto> =>
    getValidated(`/layouts/nodes/${nodeId}/stats`, folderStatsSchema, {
      params: { recursive },
    }),

  lookupSiblingNames,

  getAncestors: async (
    nodeId: Guid,
    options?: { nodeType?: string },
  ): Promise<NodeDto[]> =>
    getValidated(`/layouts/nodes/${nodeId}/ancestors`, nodeListSchema, {
      params: options?.nodeType ? { nodeType: options.nodeType } : undefined,
    }),

  getChildren: async (
    nodeId: Guid,
    options?: {
      nodeType?: string;
      page?: number;
      pageSize?: number;
      depth?: number;
      includeStats?: boolean;
      listing?: DirectoryListingOptions;
    },
  ): Promise<NodeResponse> => {
    const requestedPage = options?.page ?? 1;
    const requestedPageSize = options?.pageSize ?? 1000;
    const url = `/layouts/nodes/${nodeId}/children`;
    const response = await httpClient.get<JsonValue>(url, {
      paramsSerializer: { indexes: null },
      params: {
        page: requestedPage,
        pageSize: requestedPageSize,
        nodeType: options?.nodeType,
        depth: options?.depth,
        includeStats: options?.includeStats,
        ...options?.listing,
      },
    });
    const content = parseValidated(url, response.data, nodeContentSchema);
    const totalCount = readRequiredIntHeader(response.headers, "x-total-count");
    return {
      content,
      totalCount,
    };
  },

  createNode: async (request: CreateNodeRequest): Promise<NodeDto> => {
    const url = "layouts/nodes";
    const response = await httpClient.put<JsonValue>(url, request);
    return parseValidated(url, response.data, nodeDtoSchema);
  },

  deleteNode: async (nodeId: Guid, skipTrash = false): Promise<void> => {
    await httpClient.delete(`/layouts/nodes/${nodeId}`, {
      params: skipTrash ? { skipTrash: true } : undefined,
    });
  },

  renameNode: async (
    nodeId: Guid,
    request: RenameNodeRequest,
  ): Promise<NodeDto> => {
    const url = `/layouts/nodes/${nodeId}/rename`;
    const response = await httpClient.patch<JsonValue>(url, request);
    return parseValidated(url, response.data, nodeDtoSchema);
  },

  moveNode: async (
    nodeId: Guid,
    request: MoveNodeRequest,
  ): Promise<NodeDto> => {
    const url = `/layouts/nodes/${nodeId}/move`;
    const response = await httpClient.patch<JsonValue>(url, request);
    return parseValidated(url, response.data, nodeDtoSchema);
  },

  updateNodeMetadata: async (
    nodeId: Guid,
    patch: Record<string, string>,
  ): Promise<NodeDto> => {
    const url = `/layouts/nodes/${nodeId}/metadata`;
    const response = await httpClient.patch<JsonValue>(url, patch);
    return parseValidated(url, response.data, nodeDtoSchema);
  },

  restoreNode: async (
    nodeId: Guid,
    options: RestoreOptions = {},
  ): Promise<RestoreOutcomeDto> => {
    const url = `/layouts/nodes/${nodeId}/restore`;
    const response = await httpClient.post<JsonValue>(url, {
      createMissingParents: options.createMissingParents ?? false,
      overwrite: options.overwrite ?? false,
    });
    return parseValidated(url, response.data, restoreOutcomeSchema);
  },
};
