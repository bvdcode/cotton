import { AxiosError, AxiosHeaders } from "axios";
import { vi } from "vitest";
import type { NodeDto } from "../api/layoutsApi";
import type { MoveFileRequest } from "../api/filesApi";
import type {
  MoveNodeRequest,
  NodeFileManifestDto,
  RestoreConflictKind,
} from "../api/nodesApi";
import { createFile, createFolder } from "../../test/fileFixtures";

export function conflict(kind: RestoreConflictKind): AxiosError {
  return new AxiosError(
    "Name conflict",
    "ERR_BAD_REQUEST",
    undefined,
    undefined,
    {
      status: 409,
      statusText: "Conflict",
      data: { conflictKind: kind },
      headers: {},
      config: { headers: new AxiosHeaders() },
    },
  );
}

export class TransferFixture {
  readonly folders = new Map<string, NodeDto>();
  readonly files = new Map<string, NodeFileManifestDto>();

  folder(id: string, parentId: string | null, name = id): NodeDto {
    const folder = createFolder({ id, parentId, name });
    this.folders.set(id, folder);
    return folder;
  }

  file(id: string, nodeId: string, name = id): NodeFileManifestDto {
    const file = createFile({ id, nodeId, name });
    this.files.set(id, file);
    return file;
  }

  readonly getNode = vi.fn(async (id: string) => this.requireFolder(id));

  readonly getChildren = vi.fn(async (id: string) => {
    const node = this.requireFolder(id);
    const nodes = [...this.folders.values()].filter(
      (folder) => folder.parentId === id,
    );
    const files = [...this.files.values()].filter((file) => file.nodeId === id);
    return {
      content: { ...node, node, nodes, files },
      totalCount: nodes.length + files.length,
    };
  });

  readonly getAncestors = vi.fn(async (id: string) => {
    const ancestors: NodeDto[] = [];
    let node = this.requireFolder(id);
    while (node.parentId) {
      node = this.requireFolder(node.parentId);
      ancestors.push(node);
    }
    return ancestors;
  });

  readonly moveNode = vi.fn(async (id: string, request: MoveNodeRequest) => {
    const source = this.requireFolder(id);
    this.checkName(request.parentId, request.name ?? source.name, id);
    const node = {
      ...source,
      parentId: request.parentId,
      name: request.name ?? source.name,
      metadata: applyMetadata(source.metadata, request.metadata),
    };
    this.folders.set(id, node);
    return node;
  });

  readonly copyNode = vi.fn(async (id: string, request: MoveNodeRequest) => {
    const source = this.requireFolder(id);
    this.checkName(request.parentId, request.name ?? source.name);
    const node = {
      ...source,
      id: `copy-${id}`,
      parentId: request.parentId,
      name: request.name ?? source.name,
      metadata: applyMetadata(source.metadata, request.metadata),
    };
    this.folders.set(node.id, node);
    return node;
  });

  readonly moveFile = vi.fn(async (id: string, request: MoveFileRequest) =>
    this.transferFile(id, request, false),
  );
  readonly copyFile = vi.fn(async (id: string, request: MoveFileRequest) =>
    this.transferFile(id, request, true),
  );

  readonly deleteNode = vi.fn(
    async (id: string, skipTrash: boolean, onlyIfEmpty: boolean) => {
      if (
        skipTrash ||
        !onlyIfEmpty ||
        [...this.folders.values()].some((node) => node.parentId === id) ||
        [...this.files.values()].some((file) => file.nodeId === id)
      ) {
        throw conflict("Folder");
      }
      this.folders.delete(id);
    },
  );

  private requireFolder(id: string): NodeDto {
    const node = this.folders.get(id);
    if (!node) {
      throw new Error(`Missing folder: ${id}`);
    }
    return node;
  }

  private checkName(parentId: string, name: string, excludedId?: string): void {
    if (
      [...this.folders.values()].some(
        (node) =>
          node.parentId === parentId &&
          node.name === name &&
          node.id !== excludedId,
      )
    ) {
      throw conflict("Folder");
    }
    if (
      [...this.files.values()].some(
        (file) =>
          file.nodeId === parentId &&
          file.name === name &&
          file.id !== excludedId,
      )
    ) {
      throw conflict("File");
    }
  }

  private transferFile(
    id: string,
    request: MoveFileRequest,
    copy: boolean,
  ): NodeFileManifestDto {
    const source = this.files.get(id);
    if (!source) {
      throw new Error(`Missing file: ${id}`);
    }
    const name = request.name ?? source.name;
    if (request.overwrite) {
      for (const file of this.files.values()) {
        if (
          file.nodeId === request.parentId &&
          file.name === name &&
          file.id !== id
        ) {
          this.files.delete(file.id);
        }
      }
    }
    this.checkName(request.parentId, name, copy ? undefined : id);
    const file = {
      ...source,
      id: copy ? `copy-${id}` : id,
      nodeId: request.parentId,
      name,
      metadata: { ...source.metadata, ...request.metadata },
    };
    this.files.set(file.id, file);
    return file;
  }
}

function applyMetadata(
  metadata: Record<string, string>,
  patch?: Record<string, string | null>,
): Record<string, string> {
  const result = { ...metadata };
  for (const [key, value] of Object.entries(patch ?? {})) {
    if (value === null) {
      delete result[key];
    } else {
      result[key] = value;
    }
  }
  return result;
}
