import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { NodeDto } from "@shared/api/layoutsApi";
import type { NodeContentDto } from "@shared/api/nodesApi";
import {
  getFolderEncryptionPolicyState,
  readEnvelopeFromPreferences,
  type FolderEncryptionPolicyState,
  useVault,
} from "@shared/crypto";
import { useUserPreferencesStore } from "@shared/store/userPreferencesStore";
import {
  buildFolderEncryptionPrompt,
  isFilesUnlockDialogOpen,
  shouldPromptForCurrentFolderUnlock,
  type ClientEncryptionFolderAction,
  type ClientEncryptionUnlockPrompt,
  type FolderEncryptionPromptModel,
} from "../filesPageModel";
import { useFolderClientEncryptionActions } from "./useFolderClientEncryptionActions";
import { useFolderEncryptionPolicy } from "./useFolderEncryptionPolicy";

interface UseFilesEncryptionControllerOptions {
  activeCurrentNode: NodeDto | null;
  ancestors: NodeDto[];
  content: NodeContentDto | undefined;
  nodeId: string | null;
  showToast: (message: string, variant?: "info" | "error") => void;
}

interface FilesEncryptionController {
  activeUnlockPrompt: ClientEncryptionUnlockPrompt | null;
  clientEncryptionEnvelope: ReturnType<typeof readEnvelopeFromPreferences>;
  currentFolderEncryptionPolicy: FolderEncryptionPolicyState;
  ensureCurrentFolderUnlocked: () => boolean;
  folderEncryptionPrompt: FolderEncryptionPromptModel | null;
  getChildFolderEncryptionPolicyState: (
    folder: NodeDto,
  ) => FolderEncryptionPolicyState;
  goHome: () => void;
  goToFolder: (folderId: string) => void;
  handleToggleFolderEncryption: (
    folderId: string,
    currentlyEnabled: boolean,
  ) => Promise<void>;
  handleUnlockCancel: () => void;
  handleUnlockSuccess: () => void;
  unlockDialogOpen: boolean;
}

export const useFilesEncryptionController = ({
  activeCurrentNode,
  ancestors,
  content,
  nodeId,
  showToast,
}: UseFilesEncryptionControllerOptions): FilesEncryptionController => {
  const navigate = useNavigate();
  const { t } = useTranslation("files");
  const preferences = useUserPreferencesStore((state) => state.preferences);
  const isVaultUnlocked = useVault((state) => state.isUnlocked);
  const clientEncryptionEnvelope = useMemo(
    () => readEnvelopeFromPreferences(preferences),
    [preferences],
  );
  const [unlockPrompt, setUnlockPrompt] =
    useState<ClientEncryptionUnlockPrompt | null>(null);

  const activeAncestors = useMemo(
    () => (activeCurrentNode ? ancestors : []),
    [activeCurrentNode, ancestors],
  );
  const currentFolderEncryptionPolicy = useMemo(
    () => getFolderEncryptionPolicyState(activeCurrentNode, activeAncestors),
    [activeAncestors, activeCurrentNode],
  );
  const childFolderEncryptionAncestors = useMemo(
    () =>
      activeCurrentNode
        ? [...activeAncestors, activeCurrentNode]
        : activeAncestors,
    [activeAncestors, activeCurrentNode],
  );
  const getChildFolderEncryptionPolicyState = useCallback(
    (folder: NodeDto) =>
      getFolderEncryptionPolicyState(
        folder,
        childFolderEncryptionAncestors,
      ),
    [childFolderEncryptionAncestors],
  );

  const {
    decryptEncryptedFiles,
    encryptPlainFiles,
    encryptedFiles,
    folderPolicyEnabled,
    isDecryptingEncryptedFiles,
    isEncryptingPlainFiles,
    plainFiles,
  } = useFolderClientEncryptionActions({
    nodeId,
    currentNode: activeCurrentNode,
    content,
    folderPolicyEnabled: currentFolderEncryptionPolicy.effectiveEnabled,
    onToast: showToast,
  });
  const { toggleFolderEncryptionPolicy: handleToggleFolderEncryption } =
    useFolderEncryptionPolicy({ onToast: showToast });

  const currentFolderRequiresUnlock = shouldPromptForCurrentFolderUnlock({
    clientEncryptionEnabled: currentFolderEncryptionPolicy.effectiveEnabled,
    currentNodeId: activeCurrentNode?.id,
    isVaultUnlocked,
    nodeId,
  });
  const activeUnlockPrompt = useMemo<ClientEncryptionUnlockPrompt | null>(() => {
    if (currentFolderRequiresUnlock && clientEncryptionEnvelope) {
      return { kind: "current" };
    }

    return unlockPrompt;
  }, [clientEncryptionEnvelope, currentFolderRequiresUnlock, unlockPrompt]);

  const goHome = useCallback(() => navigate("/files"), [navigate]);

  useEffect(() => {
    if (!currentFolderRequiresUnlock || clientEncryptionEnvelope) {
      return;
    }

    showToast(t("clientEncryption.toasts.setupRequired"), "error");
    goHome();
  }, [
    clientEncryptionEnvelope,
    currentFolderRequiresUnlock,
    goHome,
    showToast,
    t,
  ]);

  const ensureCurrentFolderUnlocked = useCallback((): boolean => {
    if (
      !currentFolderEncryptionPolicy.effectiveEnabled ||
      isVaultUnlocked
    ) {
      return true;
    }

    if (!clientEncryptionEnvelope) {
      showToast(t("clientEncryption.toasts.setupRequired"), "error");
      return false;
    }

    setUnlockPrompt({ kind: "current" });
    return false;
  }, [
    clientEncryptionEnvelope,
    currentFolderEncryptionPolicy.effectiveEnabled,
    isVaultUnlocked,
    showToast,
    t,
  ]);

  const runFolderClientEncryptionAction = useCallback(
    (action: ClientEncryptionFolderAction) => {
      if (isVaultUnlocked) {
        switch (action) {
          case "encrypt-existing":
            void encryptPlainFiles();
            return;
          case "decrypt-existing":
            void decryptEncryptedFiles();
            return;
        }
      }

      if (!clientEncryptionEnvelope) {
        showToast(t("clientEncryption.toasts.setupRequired"), "error");
        return;
      }

      setUnlockPrompt({ kind: "action", action });
    },
    [
      clientEncryptionEnvelope,
      decryptEncryptedFiles,
      encryptPlainFiles,
      isVaultUnlocked,
      showToast,
      t,
    ],
  );

  const folderEncryptionPrompt = useMemo(
    () =>
      buildFolderEncryptionPrompt({
        decryptEncryptedFiles: () =>
          runFolderClientEncryptionAction("decrypt-existing"),
        encryptedFilesCount: encryptedFiles.length,
        encryptedFilesMessage: t("clientEncryption.encryptedFilesRemain.toast", {
          count: encryptedFiles.length,
        }),
        encryptedFilesAction: t("clientEncryption.encryptedFilesRemain.action"),
        encryptPlainFiles: () =>
          runFolderClientEncryptionAction("encrypt-existing"),
        folderPolicyEnabled,
        isDecryptingEncryptedFiles,
        isEncryptingPlainFiles,
        plainFilesCount: plainFiles.length,
        plainFilesMessage: t("clientEncryption.mixedPlain.toast", {
          count: plainFiles.length,
        }),
        plainFilesAction: t("clientEncryption.mixedPlain.action"),
      }),
    [
      encryptedFiles.length,
      folderPolicyEnabled,
      isDecryptingEncryptedFiles,
      isEncryptingPlainFiles,
      plainFiles.length,
      runFolderClientEncryptionAction,
      t,
    ],
  );

  const goToFolder = useCallback(
    (folderId: string) => {
      const targetFolder = content?.nodes.find(
        (folder) => folder.id === folderId,
      );
      const requiresUnlock =
        targetFolder &&
        getChildFolderEncryptionPolicyState(targetFolder).effectiveEnabled &&
        !isVaultUnlocked;

      if (!requiresUnlock) {
        navigate(`/files/${folderId}`);
        return;
      }

      if (!clientEncryptionEnvelope) {
        showToast(t("clientEncryption.toasts.setupRequired"), "error");
        return;
      }

      setUnlockPrompt({ kind: "open", folderId });
    },
    [
      clientEncryptionEnvelope,
      content?.nodes,
      getChildFolderEncryptionPolicyState,
      isVaultUnlocked,
      navigate,
      showToast,
      t,
    ],
  );

  const handleUnlockCancel = useCallback(() => {
    const prompt = activeUnlockPrompt;
    setUnlockPrompt(null);

    if (prompt?.kind === "current") {
      goHome();
    }
  }, [activeUnlockPrompt, goHome]);

  const handleUnlockSuccess = useCallback(() => {
    const prompt = activeUnlockPrompt;
    setUnlockPrompt(null);

    switch (prompt?.kind) {
      case "open":
        navigate(`/files/${prompt.folderId}`);
        return;
      case "action":
        switch (prompt.action) {
          case "encrypt-existing":
            void encryptPlainFiles();
            return;
          case "decrypt-existing":
            void decryptEncryptedFiles();
            return;
        }
        return;
      case "current":
      case undefined:
        return;
    }
  }, [
    activeUnlockPrompt,
    decryptEncryptedFiles,
    encryptPlainFiles,
    navigate,
  ]);

  return {
    activeUnlockPrompt,
    clientEncryptionEnvelope,
    currentFolderEncryptionPolicy,
    ensureCurrentFolderUnlocked,
    folderEncryptionPrompt,
    getChildFolderEncryptionPolicyState,
    goHome,
    goToFolder,
    handleToggleFolderEncryption,
    handleUnlockCancel,
    handleUnlockSuccess,
    unlockDialogOpen: isFilesUnlockDialogOpen(
      activeUnlockPrompt,
      clientEncryptionEnvelope,
    ),
  };
};
