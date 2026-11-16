import { createNamedFolder } from "../../../shared/crypto/folderNameOperations";
import { applyDisplayMetaToNode } from "../../../shared/crypto/folderDisplayMeta";
import { lookupDisplayNames } from "../../../shared/crypto/lookupDisplayNames";
import { type SiblingNameLookupDto } from "../../../shared/api/nodesApi";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import {
  isFolderEncryptionPolicyEnabled,
  useVault,
} from "../../../shared/crypto";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { getFileNameKey } from "../../../shared/utils/fileNameUtils";
import { uploadConfig } from "../../../shared/upload/config";
import type { DroppedFile } from "./scanDroppedFiles";

interface FolderBucket {
  label: string;
  files: File[];
}

interface FolderIdentity {
  id: string;
  name: string;
}

interface FolderTarget {
  nodeId: string;
  labelSuffix: string;
}

interface FolderLookup extends SiblingNameLookupDto {
  foldersByNameKey: Map<string, NodeDto>;
  fileNameKeys: Set<string>;
}

export class DroppedFolderResolver {
  private readonly folderByKey = new Map<string, Promise<FolderIdentity>>();
  private readonly targetByPath = new Map<string, Promise<FolderTarget>>();
  private readonly lookupByNodeId = new Map<string, Promise<FolderLookup>>();
  private readonly requestedChildrenByPath = new Map<string, Set<string>>();
  private readonly policyEnabledByNodeId: Map<string, boolean>;
  private readonly rootNodeId: string;
  private readonly baseLabel: string;
  private readonly isPolicyEnabledForNode: (nodeId: string) => boolean;

  constructor(
    rootNodeId: string,
    baseLabel: string,
    rootPolicyEnabled: boolean,
    isPolicyEnabledForNode: (nodeId: string) => boolean,
  ) {
    this.rootNodeId = rootNodeId;
    this.baseLabel = baseLabel;
    this.isPolicyEnabledForNode = isPolicyEnabledForNode;
    this.policyEnabledByNodeId = new Map([[rootNodeId, rootPolicyEnabled]]);
  }

  decideEncryption(targetNodeId: string): {
    encrypt: boolean;
    vaultLocked: boolean;
  } {
    const policyEnabled =
      this.policyEnabledByNodeId.get(targetNodeId) ??
      this.isPolicyEnabledForNode(targetNodeId);
    if (!policyEnabled) {
      return { encrypt: false, vaultLocked: false };
    }

    const vaultUnlocked = useVault.getState().isUnlocked;
    return { encrypt: vaultUnlocked, vaultLocked: !vaultUnlocked };
  }

  private getLookup(
    id: string,
    pathKey: string,
    desiredName: string,
  ): Promise<FolderLookup> {
    const cached = this.lookupByNodeId.get(id);
    if (cached) {
      return cached;
    }
    const names = this.requestedChildrenByPath.get(pathKey);
    const lookup = lookupDisplayNames(
      id,
      names ? [...names] : [desiredName],
      this.policyEnabledByNodeId.get(id) ?? this.isPolicyEnabledForNode(id),
    ).then((result) => ({
      ...result,
      foldersByNameKey: new Map(
        result.nodes.map((node) => [getFileNameKey(node.name), node]),
      ),
      fileNameKeys: new Set(
        result.files.map((file) => getFileNameKey(file.name)),
      ),
    }));
    this.lookupByNodeId.set(id, lookup);
    return lookup;
  }

  private async findAvailableFolderName(
    parentId: string,
    baseName: string,
    lookup: SiblingNameLookupDto,
  ): Promise<string> {
    if (lookup.takenNameKeys.length === 0) {
      const expanded = await lookupDisplayNames(
        parentId,
        [baseName],
        this.policyEnabledByNodeId.get(parentId) ??
          this.isPolicyEnabledForNode(parentId),
      );
      lookup.takenNameKeys = expanded.takenNameKeys;
    }
    const takenNames = new Set<string>([
      ...lookup.takenNameKeys,
      ...lookup.nodes.map((node) => getFileNameKey(node.name)),
      ...lookup.files.map((file) => getFileNameKey(file.name)),
    ]);

    const preferred = `${baseName} (folder)`;
    if (!takenNames.has(getFileNameKey(preferred))) {
      return preferred;
    }

    for (let index = 2; index < 10_000; index += 1) {
      const candidate = `${baseName} (folder ${index})`;
      if (!takenNames.has(getFileNameKey(candidate))) {
        return candidate;
      }
    }
    return `${baseName}-${Date.now()}`;
  }

  private ensureFolder(
    parentId: string,
    desiredName: string,
    parentPathKey: string,
  ): Promise<FolderIdentity> {
    const desiredNameKey = getFileNameKey(desiredName);
    const key = `${parentId}::${desiredNameKey}`;
    const cachedFolder = this.folderByKey.get(key);
    if (cachedFolder) {
      return cachedFolder;
    }

    const folder = this.resolveFolder(parentId, desiredName, parentPathKey);
    this.folderByKey.set(key, folder);
    return folder;
  }

  private async resolveFolder(
    parentId: string,
    desiredName: string,
    parentPathKey: string,
  ): Promise<FolderIdentity> {
    const desiredNameKey = getFileNameKey(desiredName);
    const lookup = await this.getLookup(parentId, parentPathKey, desiredName);
    const existing = lookup.foldersByNameKey.get(desiredNameKey);
    if (existing) {
      const parentPolicyEnabled =
        this.policyEnabledByNodeId.get(parentId) ??
        this.isPolicyEnabledForNode(parentId);
      const folder = { id: existing.id, name: existing.name };
      this.policyEnabledByNodeId.set(
        existing.id,
        parentPolicyEnabled ||
          isFolderEncryptionPolicyEnabled(existing.metadata),
      );
      return folder;
    }

    const hasFileConflict = lookup.fileNameKeys.has(desiredNameKey);
    const nameToCreate = hasFileConflict
      ? await this.findAvailableFolderName(parentId, desiredName, lookup)
      : desiredName;

    const aliasKey = `${parentId}::${getFileNameKey(nameToCreate)}`;
    if (getFileNameKey(nameToCreate) !== desiredNameKey) {
      const alias = this.folderByKey.get(aliasKey);
      if (alias) {
        return alias;
      }
    }

    const folder = this.createFolder(parentId, nameToCreate, lookup);
    this.folderByKey.set(aliasKey, folder);
    return folder;
  }

  private async createFolder(
    parentId: string,
    name: string,
    lookup: FolderLookup,
  ): Promise<FolderIdentity> {
    const parentPolicyEnabled =
      this.policyEnabledByNodeId.get(parentId) ??
      this.isPolicyEnabledForNode(parentId);
    const folder = await createNamedFolder(parentId, name, parentPolicyEnabled);
    this.policyEnabledByNodeId.set(folder.id, parentPolicyEnabled);
    this.lookupByNodeId.set(
      folder.id,
      Promise.resolve({
        nodes: [],
        files: [],
        takenNameKeys: [],
        foldersByNameKey: new Map<string, NodeDto>(),
        fileNameKeys: new Set<string>(),
      }),
    );
    const displayFolder = await applyDisplayMetaToNode(folder);
    lookup.nodes.push(displayFolder);
    lookup.foldersByNameKey.set(
      getFileNameKey(displayFolder.name),
      displayFolder,
    );
    useNodesStore.getState().addFolderToCache(parentId, folder);
    return { id: folder.id, name };
  }

  private ensureFolderPath(segments: string[]): Promise<FolderTarget> {
    const key = segments.map(getFileNameKey).join("/");
    const cached = this.targetByPath.get(key);
    if (cached) {
      return cached;
    }
    const target = this.resolveFolderPath(segments);
    this.targetByPath.set(key, target);
    return target;
  }

  private async resolveFolderPath(segments: string[]): Promise<FolderTarget> {
    let currentId = this.rootNodeId;
    let parentPathKey = "";
    const effectiveSegments: string[] = [];

    for (const raw of segments) {
      const segment = raw.trim();
      if (segment.length === 0) {
        continue;
      }
      const next = await this.ensureFolder(currentId, segment, parentPathKey);
      currentId = next.id;
      effectiveSegments.push(next.name);
      parentPathKey += `/${getFileNameKey(segment)}`;
    }

    return {
      nodeId: currentId,
      labelSuffix: effectiveSegments.join(" / "),
    };
  }

  async groupFiles(
    dropped: DroppedFile[],
    onProgress: (processed: number) => void,
  ): Promise<Map<string, FolderBucket>> {
    const filesByTarget = new Map<string, FolderBucket>();
    const paths: string[][] = [];
    this.requestedChildrenByPath.clear();
    for (const item of dropped) {
      const parts = item.relativePath
        .replace(/^\\+|^\/+/, "")
        .split(/[\\/]+/)
        .filter((part) => part.length > 0);
      parts.pop();
      const segments = parts.map((part) => part.trim()).filter(Boolean);
      paths.push(segments);
      let parentPathKey = "";
      for (const segment of segments) {
        const names =
          this.requestedChildrenByPath.get(parentPathKey) ?? new Set<string>();
        names.add(segment);
        this.requestedChildrenByPath.set(parentPathKey, names);
        parentPathKey += `/${getFileNameKey(segment)}`;
      }
    }
    onProgress(0);

    const targets: FolderTarget[] = [];
    let nextIndex = 0;
    let processed = 0;
    let stopped = false;
    const resolveNext = async () => {
      while (!stopped && nextIndex < dropped.length) {
        const index = nextIndex;
        nextIndex += 1;
        targets[index] = await this.ensureFolderPath(paths[index]);
        processed += 1;
        onProgress(processed);
      }
    };
    const workers = Array.from(
      {
        length: Math.min(
          uploadConfig.maxConcurrentFolderCreations,
          dropped.length,
        ),
      },
      resolveNext,
    );
    try {
      await Promise.all(workers);
    } catch (error) {
      stopped = true;
      await Promise.allSettled(workers);
      throw error;
    }

    for (let index = 0; index < dropped.length; index += 1) {
      const item = dropped[index];
      const { nodeId, labelSuffix } = targets[index];
      const label =
        labelSuffix.length > 0
          ? `${this.baseLabel} / ${labelSuffix}`
          : this.baseLabel;

      const bucket = filesByTarget.get(nodeId) ?? { label, files: [] };
      bucket.files.push(item.file);
      filesByTarget.set(nodeId, bucket);
    }

    return filesByTarget;
  }
}
