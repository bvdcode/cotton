import { nodesApi, type NodeContentDto } from "../../../shared/api/nodesApi";
import {
  FOLDER_ENCRYPTION_POLICY_KEY,
  isFolderEncryptionPolicyEnabled,
  useVault,
} from "../../../shared/crypto";
import { useNodesStore } from "../../../shared/store/nodesStore";
import type { DroppedFile } from "./scanDroppedFiles";

interface FolderBucket {
  label: string;
  files: File[];
}

export class DroppedFolderResolver {
  private readonly folderIdByKey = new Map<string, string>();
  private readonly childrenByNodeId = new Map<string, NodeContentDto>();
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

  private async getChildren(id: string): Promise<NodeContentDto> {
    const cached = this.childrenByNodeId.get(id);
    if (cached) return cached;
    const loaded = await nodesApi.getChildren(id);
    this.childrenByNodeId.set(id, loaded.content);
    return loaded.content;
  }

  private async findAvailableFolderName(
    parentId: string,
    baseName: string,
  ): Promise<string> {
    const content = await this.getChildren(parentId);
    const takenLower = new Set<string>([
      ...content.nodes.map((node) => node.name.toLowerCase()),
      ...content.files.map((file) => file.name.toLowerCase()),
    ]);

    const preferred = `${baseName} (folder)`;
    if (!takenLower.has(preferred.toLowerCase())) return preferred;

    for (let index = 2; index < 10_000; index += 1) {
      const candidate = `${baseName} (folder ${index})`;
      if (!takenLower.has(candidate.toLowerCase())) return candidate;
    }
    return `${baseName}-${Date.now()}`;
  }

  private async ensureFolder(
    parentId: string,
    desiredName: string,
  ): Promise<{ id: string; name: string }> {
    const key = `${parentId}::${desiredName}`;
    const cachedId = this.folderIdByKey.get(key);
    if (cachedId) return { id: cachedId, name: desiredName };

    const content = await this.getChildren(parentId);
    const existing = content.nodes.find((node) => node.name === desiredName);
    if (existing) {
      const parentPolicyEnabled =
        this.policyEnabledByNodeId.get(parentId) ??
        this.isPolicyEnabledForNode(parentId);
      this.folderIdByKey.set(key, existing.id);
      this.policyEnabledByNodeId.set(
        existing.id,
        parentPolicyEnabled ||
          isFolderEncryptionPolicyEnabled(existing.metadata),
      );
      return { id: existing.id, name: desiredName };
    }

    const hasFileConflict = content.files.some(
      (file) => file.name === desiredName,
    );
    const nameToCreate = hasFileConflict
      ? await this.findAvailableFolderName(parentId, desiredName)
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
    content.nodes.push(folder);
    useNodesStore.getState().addFolderToCache(parentId, folder);
    this.folderIdByKey.set(`${parentId}::${nameToCreate}`, folder.id);

    return { id: folder.id, name: nameToCreate };
  }

  private async ensureFolderPath(
    segments: string[],
  ): Promise<{ nodeId: string; labelSuffix: string }> {
    let currentId = this.rootNodeId;
    const effectiveSegments: string[] = [];

    for (const raw of segments) {
      const segment = raw.trim();
      if (segment.length === 0) continue;
      const next = await this.ensureFolder(currentId, segment);
      currentId = next.id;
      effectiveSegments.push(next.name);
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
