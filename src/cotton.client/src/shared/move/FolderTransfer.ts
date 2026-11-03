import { nodesApi } from "../api/nodesApi";
import { fetchAllRawNodeChildren } from "../api/nodeChildren";
import { useVault } from "../crypto/vault";
import {
  getMoveTarget,
  needsEncryptionAfterMove,
  needsDecryptionAfterMove,
} from "../hooks/moveState";
import type { MoveExecutionResult } from "../hooks/moveTypes";
import type {
  FileTransferOperation,
  MoveClipboardItem,
} from "../store/moveClipboardStore";
import { createConflictResolver } from "../types/createConflictResolver";
import { ConflictAction } from "../types/nameConflict";
import { FolderTransferNames } from "./FolderTransferNames";
import {
  moveItemWithConflictResolution,
  type MoveConflictResolver,
} from "./moveConflictResolution";
import {
  applyTransferToCache,
  removeMergedFolderFromCache,
} from "./transferCache";

type TransferOutcome =
  | { kind: "completed"; item: MoveClipboardItem }
  | { kind: "failed"; error: Error }
  | { kind: "skipped" }
  | { kind: "cancelled" };

export class FolderTransfer {
  readonly result: Pick<
    MoveExecutionResult,
    | "sourceParents"
    | "movedFilesToEncrypt"
    | "movedFilesToOfferDecrypt"
    | "movedFoldersToEncrypt"
  > = {
    sourceParents: new Set<string>(),
    movedFilesToEncrypt: [],
    movedFilesToOfferDecrypt: [],
    movedFoldersToEncrypt: [],
  };
  private readonly namesByParent = new Map<string, FolderTransferNames>();
  private readonly confirmConflict: MoveConflictResolver;
  private readonly operation: FileTransferOperation;
  private readonly prepareEncryptedTarget: (() => Promise<void>) | undefined;
  private skipAllConflicts = false;

  constructor(
    operation: FileTransferOperation,
    confirmConflict: MoveConflictResolver,
    prepareEncryptedTarget?: () => Promise<void>,
  ) {
    this.operation = operation;
    this.prepareEncryptedTarget = prepareEncryptedTarget;
    this.confirmConflict = createConflictResolver(confirmConflict);
  }

  async execute(
    item: MoveClipboardItem,
    targetParentId: string,
    encryptsNewFiles: boolean,
    hasMoreItems: boolean,
    mergeFolders = false,
  ): Promise<TransferOutcome> {
    let names = this.namesByParent.get(targetParentId);
    if (!names) {
      names = new FolderTransferNames(targetParentId);
      this.namesByParent.set(targetParentId, names);
    }
    try {
      const outcome = await moveItemWithConflictResolution({
        operation: this.operation,
        item,
        targetParentId,
        encryptFolders: encryptsNewFiles,
        hasMoreItems,
        folderNames: names,
        skipAllConflicts: this.skipAllConflicts,
        confirmConflict: (prompt) =>
          mergeFolders && prompt.canMerge
            ? Promise.resolve(ConflictAction.Merge)
            : this.confirmConflict(prompt),
      });
      switch (outcome.kind) {
        case "merge":
          return await this.merge(item, outcome.targetId, hasMoreItems);
        case "moved": {
          const transferred = applyTransferToCache(
            item,
            outcome.moved,
            targetParentId,
            this.operation,
          );
          this.recordTransfer(transferred, targetParentId, encryptsNewFiles);
          if (this.operation === "move") {
            this.result.sourceParents.add(item.sourceParentId);
          }
          return { kind: "completed", item: transferred };
        }
        case "skipped":
          this.skipAllConflicts ||= outcome.skipAll;
          return { kind: "skipped" };
        case "failed":
        case "cancelled":
          return outcome;
      }
    } catch (error) {
      return {
        kind: "failed",
        error:
          error instanceof Error ? error : new Error("Folder transfer failed."),
      };
    }
  }

  private recordTransfer(
    item: MoveClipboardItem,
    targetParentId: string,
    encryptsNewFiles: boolean,
  ): void {
    const transferred = { ...item, sourceParentId: targetParentId };
    if (item.kind === "folder" && encryptsNewFiles) {
      this.result.movedFoldersToEncrypt.push(transferred);
    }
    if (encryptsNewFiles && needsEncryptionAfterMove(item)) {
      this.result.movedFilesToEncrypt.push(transferred);
    }
    if (!encryptsNewFiles && needsDecryptionAfterMove(item)) {
      this.result.movedFilesToOfferDecrypt.push(transferred);
    }
  }

  private async merge(
    item: MoveClipboardItem,
    targetId: string,
    hasMoreItems: boolean,
  ): Promise<TransferOutcome> {
    const target = await getMoveTarget(targetId);
    if (
      targetId === item.id ||
      target.ancestors.some((node) => node.id === item.id)
    ) {
      throw new Error("Cannot merge a folder into itself or its descendant.");
    }
    if (target.encryptsNewFiles && !useVault.getState().isUnlocked) {
      throw new Error("Unlock encryption before merging into this folder.");
    }
    if (target.encryptsNewFiles) {
      await this.prepareEncryptedTarget?.();
    }
    const { content } = await fetchAllRawNodeChildren(item.id);
    this.result.sourceParents.add(targetId);
    const children: MoveClipboardItem[] = [
      ...content.nodes.map((node): MoveClipboardItem => ({
        id: node.id,
        kind: "folder",
        sourceParentId: item.id,
      })),
      ...content.files.map((file): MoveClipboardItem => ({
        id: file.id,
        kind: "file",
        sourceParentId: item.id,
        file,
      })),
    ];
    let incomplete: TransferOutcome | null = null;
    for (const [index, child] of children.entries()) {
      const outcome = await this.execute(
        child,
        targetId,
        target.encryptsNewFiles,
        hasMoreItems || index < children.length - 1,
        true,
      );
      switch (outcome.kind) {
        case "cancelled":
          return outcome;
        case "failed":
          incomplete = outcome;
          break;
        case "skipped":
          incomplete ??= outcome;
          break;
        case "completed":
          break;
      }
    }
    if (incomplete) {
      return incomplete;
    }
    if (this.operation === "move") {
      await nodesApi.deleteNode(item.id, false, true);
      removeMergedFolderFromCache(item);
      this.result.sourceParents.delete(item.id);
      this.result.sourceParents.add(item.sourceParentId);
    }
    return { kind: "completed", item };
  }
}
