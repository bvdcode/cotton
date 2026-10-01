import { nodesApi, type SiblingNameLookupDto } from "../../../shared/api/nodesApi";
import {
  FOLDER_ENCRYPTION_POLICY_KEY,
  isFolderEncryptionPolicyEnabled,
  useVault,
} from "../../../shared/crypto";
import { useNodesStore } from "../../../shared/store/nodesStore";
import { getFileNameKey } from "../../../shared/utils/fileNameUtils";
import type { DroppedFile } from "./scanDroppedFiles";

interface FolderBucket {
  label: string;
  files: File[];
}

export class DroppedFolderResolver {
  private readonly folderByKey = new Map<string, { id: string; name: string }>();
  private readonly lookupByNodeId = new Map<string, SiblingNameLookupDto>();
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

  private async getLookup(
    id: string,
    pathKey: string,
    desiredName: string,
  ): Promise<SiblingNameLookupDto> {
    const cached = this.lookupByNodeId.get(id);
    if (cached) {
      return cached;
    }
    const names = this.requestedChildrenByPath.get(pathKey);
    const lookup = await nodesApi.lookupSiblingNames(
      id,
      names ? [...names] : [desiredName],
    );
    this.lookupByNodeId.set(id, lookup);
    return lookup;
  }

  private async findAvailableFolderName(
    parentId: string,
    baseName: string,
    lookup: SiblingNameLookupDto,
  ): Promise<string> {
    if (lookup.takenNameKeys.length === 0) {
      const expanded = await nodesApi.lookupSiblingNames(
        parentId,
        [baseName],
        true,
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

  private async ensureFolder(
    parentId: string,
    desiredName: string,
    parentPathKey: string,
  ): Promise<{ id: string; name: string }> {
    const desiredNameKey = getFileNameKey(desiredName);
    const key = `${parentId}::${desiredNameKey}`;
    const cachedFolder = this.folderByKey.get(key);
    if (cachedFolder) {
      return cachedFolder;
    }

    const lookup = await this.getLookup(parentId, parentPathKey, desiredName);
    const existing = lookup.nodes.find(
      (node) => getFileNameKey(node.name) === desiredNameKey,
    );
    if (existing) {
      const parentPolicyEnabled =
        this.policyEnabledByNodeId.get(parentId) ??
        this.isPolicyEnabledForNode(parentId);
      const folder = { id: existing.id, name: existing.name };
      this.folderByKey.set(key, folder);
      this.policyEnabledByNodeId.set(
        existing.id,
        parentPolicyEnabled ||
          isFolderEncryptionPolicyEnabled(existing.metadata),
      );
      return folder;
    }

    const hasFileConflict = lookup.files.some(
      (file) => getFileNameKey(file.name) === desiredNameKey,
    );
    const nameToCreate = hasFileConflict
      ? await this.findAvailableFolderName(parentId, desiredName, lookup)
      : desiredName;

    const created = await nodesApi.createNode({
      parentId,
      name: nameToCreate,
    });
    const parentPolicyEnabled =
      this.policyEnabledByNodeId.get(parentId) ??
      this.isPolicyEnabledForNode(parentId);
    const folder = parentPolicyEnabled
      ? await nodesApi.updateNodeMetadata(created.id, {
          [FOLDER_ENCRYPTION_POLICY_KEY]: "true",
        })
      : created;
    this.policyEnabledByNodeId.set(folder.id, parentPolicyEnabled);
    lookup.nodes.push(folder);
    useNodesStore.getState().addFolderToCache(parentId, folder);
    this.folderByKey.set(key, { id: folder.id, name: folder.name });
    this.folderByKey.set(`${parentId}::${getFileNameKey(nameToCreate)}`, {
      id: folder.id,
      name: folder.name,
    });

    return { id: folder.id, name: nameToCreate };
  }

  private async ensureFolderPath(
    segments: string[],
  ): Promise<{ nodeId: string; labelSuffix: string }> {
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
    this.requestedChildrenByPath.clear();
    for (const item of dropped) {
      const parts = item.relativePath.replace(/^\\+|^\/+/, "")
        .split(/[\\/]+/)
        .filter((part) => part.length > 0);
      parts.pop();
      let parentPathKey = "";
      for (const raw of parts) {
        const segment = raw.trim();
        if (segment.length === 0) {
          continue;
        }
        const names = this.requestedChildrenByPath.get(parentPathKey)
          ?? new Set<string>();
        names.add(segment);
        this.requestedChildrenByPath.set(parentPathKey, names);
        parentPathKey += `/${getFileNameKey(segment)}`;
      }
    }
    onProgress(0);

    for (let index = 0; index < dropped.length; index += 1) {
      onProgress(index);

      const item = dropped[index];
      const normalized = item.relativePath.replace(/^\\+|^\/+/, "");
      const parts = normalized
        .split(/[\\/]+/)
        .filter((part) => part.length > 0);

      if (parts.length === 0) {
        const bucket = filesByTarget.get(this.rootNodeId) ?? {
          label: this.baseLabel,
          files: [],
        };
        bucket.files.push(item.file);
        filesByTarget.set(this.rootNodeId, bucket);
        continue;
      }

      parts.pop();
      const { nodeId, labelSuffix } = await this.ensureFolderPath(parts);
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
