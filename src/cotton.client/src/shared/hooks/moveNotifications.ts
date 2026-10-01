import { useTranslation } from "react-i18next";
import { toast } from "@shared/ui/notifications";
import type { MoveClipboardItem } from "../store/moveClipboardStore";
import type { MoveTranslation } from "./useMoveOperations";
import { showActionToast } from "../ui/ActionToast";
import { useVault } from "../crypto";
import { fetchServerSettings } from "../api/queries/serverSettings";
import { queryClient } from "../api/queries/queryClient";
import { decryptExistingFileWithTask } from "../tasks";
import { refreshNodeContent } from "../store/nodesActions";

export const offerDecryptForMovedFiles = (options: {
  files: ReadonlyArray<MoveClipboardItem>;
  targetNodeName: string;
  targetParentId: string;
  t: MoveTranslation;
}): void => {
  if (options.files.length === 0) return;

  showActionToast({
    toastId:
      "files-cse-decrypt-moved-" + options.targetParentId + "-" + Date.now(),
    message: options.t("clientEncryption.movedEncrypted.toast", {
      ns: "files",
      count: options.files.length,
    }),
    action: options.t("clientEncryption.movedEncrypted.action", {
      ns: "files",
    }),
    onAction: () => {
      void decryptMovedEncryptedFiles({
        files: options.files,
        targetParentId: options.targetParentId,
        targetNodeName: options.targetNodeName,
        t: options.t,
      });
    },
  });
};

export const showMoveOutcomeToasts = (options: {
  encryptionFailedCount: number;
  encryptionScanIncomplete: boolean;
  failed: ReadonlyArray<MoveClipboardItem>;
  lastErrorMessage: string | null;
  succeeded: ReadonlyArray<MoveClipboardItem>;
  targetParentId: string;
  t: MoveTranslation;
}): void => {
  if (options.succeeded.length > 0) {
    toast.success(
      options.t("move.toasts.moved", {
        ns: "files",
        count: options.succeeded.length,
      }),
      {
        toastId: "move-success-" + options.targetParentId + "-" + Date.now(),
      },
    );
  }

  if (options.failed.length > 0) {
    toast.error(
      options.lastErrorMessage ??
        options.t("move.toasts.failed", {
          ns: "files",
          count: options.failed.length,
        }),
      {
        toastId: "move-error-" + options.targetParentId + "-" + Date.now(),
      },
    );
  }

  if (options.encryptionFailedCount > 0) {
    toast.error(
      options.t("clientEncryption.toasts.encryptExistingFailed", {
        ns: "files",
        count: options.encryptionFailedCount,
      }),
      {
        toastId:
          "move-encrypt-error-" + options.targetParentId + "-" + Date.now(),
      },
    );
  }

  if (options.encryptionScanIncomplete) {
    toast.error(
      options.t("clientEncryption.toasts.encryptExistingScanIncomplete", {
        ns: "files",
      }),
      {
        toastId:
          "move-encrypt-scan-incomplete-" +
          options.targetParentId +
          "-" +
          Date.now(),
      },
    );
  }
};

async function decryptMovedEncryptedFiles(options: {
  files: ReadonlyArray<MoveClipboardItem>;
  targetParentId: string;
  targetNodeName: string;
  t: ReturnType<typeof useTranslation<["files", "common", "tasks"]>>["t"];
}): Promise<void> {
  const { files, targetParentId, targetNodeName, t } = options;

  if (!useVault.getState().isUnlocked) {
    toast.error(t("clientEncryption.toasts.unlockRequired", { ns: "files" }));
    return;
  }

  let settings: Awaited<ReturnType<typeof fetchServerSettings>>;
  try {
    settings = await fetchServerSettings(queryClient);
  } catch {
    toast.error(t("errors.serverSettingsNotLoaded", { ns: "tasks" }));
    return;
  }

  let decryptedCount = 0;
  let failedCount = 0;

  for (const item of files) {
    if (!item.file) continue;

    try {
      await decryptExistingFileWithTask({
        file: {
          id: item.id,
          name: item.file.name,
          contentType: item.file.contentType,
          sizeBytes: item.file.sizeBytes,
          metadata: item.file.metadata,
        },
        targetNodeId: targetParentId,
        scopeLabel: targetNodeName,
        server: {
          maxChunkSizeBytes: settings.maxChunkSizeBytes,
          supportedHashAlgorithm: settings.supportedHashAlgorithm,
        },
      });
      decryptedCount += 1;
    } catch {
      failedCount += 1;
    }
  }

  void refreshNodeContent(targetParentId);

  if (decryptedCount > 0) {
    toast.success(
      t("clientEncryption.toasts.decryptExistingComplete", {
        ns: "files",
        count: decryptedCount,
      }),
    );
  }

  if (failedCount > 0) {
    toast.error(
      t("clientEncryption.toasts.decryptExistingFailed", {
        ns: "files",
        count: failedCount,
      }),
    );
  }
}
