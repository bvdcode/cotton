import {
  ContentCut,
  Delete,
  Download,
  Edit,
  LockOpenOutlined,
  LockOutlined,
  Restore,
  Share,
} from "@mui/icons-material";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import {
  isFolderEncryptionPolicyEnabled,
  type FolderEncryptionPolicyState,
} from "../../../shared/crypto";
import { RenamableItemCard } from "./RenamableItemCard";
import { getFolderIcon } from "@shared/utils/icons";

interface FolderCardProps {
  folder: NodeDto;
  isRenaming: boolean;
  renamingName: string;
  onRenamingNameChange: (name: string) => void;
  onConfirmRename?: () => void;
  onCancelRename?: () => void;
  onStartRename?: () => void;
  onRestore?: () => void;
  onDelete?: () => void;
  onDownload?: () => void;
  onShare?: () => void;
  onCut?: () => void;
  onToggleEncryptionPolicy?: () => void;
  encryptionPolicy?: FolderEncryptionPolicyState;
  onClick: (event?: React.SyntheticEvent) => void;
  variant?: "default" | "squareTile";
  readOnly?: boolean;
}

type FolderAction = NonNullable<
  React.ComponentProps<typeof RenamableItemCard>["actions"]
>[number];

type FolderActionOptions = Pick<
  FolderCardProps,
  | "onCut"
  | "onDelete"
  | "onDownload"
  | "onRestore"
  | "onShare"
  | "onStartRename"
  | "onToggleEncryptionPolicy"
  | "readOnly"
> & {
  encryptionPolicyInherited: boolean;
  explicitEncryptionPolicyEnabled: boolean;
  t: ReturnType<typeof useTranslation>["t"];
};

const buildFolderActions = (options: FolderActionOptions): FolderAction[] => {
  const actions: FolderAction[] = [];
  addDownloadAction(actions, options);
  if (options.readOnly) {
    return actions;
  }

  addShareAction(actions, options);
  addRenameAction(actions, options);
  addCutAction(actions, options);
  addEncryptionPolicyAction(actions, options);
  addRestoreAction(actions, options);
  addDeleteAction(actions, options);
  return actions;
};

const addDownloadAction = (
  actions: FolderAction[],
  options: FolderActionOptions,
) => {
  if (options.onDownload) {
    actions.push({
      icon: <Download />,
      onClick: options.onDownload,
      tooltip: options.t("common:actions.download"),
    });
  }
};

const addShareAction = (
  actions: FolderAction[],
  options: FolderActionOptions,
) => {
  if (options.onShare) {
    actions.push({
      icon: <Share />,
      onClick: options.onShare,
      tooltip: options.t("common:actions.share"),
    });
  }
};

const addRenameAction = (
  actions: FolderAction[],
  options: FolderActionOptions,
) => {
  if (options.onStartRename) {
    actions.push({
      icon: <Edit />,
      onClick: options.onStartRename,
      tooltip: options.t("common:actions.rename"),
    });
  }
};

const addCutAction = (
  actions: FolderAction[],
  options: FolderActionOptions,
) => {
  if (options.onCut) {
    actions.push({
      icon: <ContentCut />,
      onClick: options.onCut,
      tooltip: options.t("files:move.cut"),
    });
  }
};

const addEncryptionPolicyAction = (
  actions: FolderAction[],
  options: FolderActionOptions,
) => {
  if (!options.onToggleEncryptionPolicy || options.encryptionPolicyInherited) {
    return;
  }

  actions.push({
    icon: options.explicitEncryptionPolicyEnabled ? (
      <LockOpenOutlined />
    ) : (
      <LockOutlined />
    ),
    onClick: options.onToggleEncryptionPolicy,
    tooltip: options.explicitEncryptionPolicyEnabled
      ? options.t("files:clientEncryption.disablePolicy")
      : options.t("files:clientEncryption.enablePolicy"),
  });
};

const addRestoreAction = (
  actions: FolderAction[],
  options: FolderActionOptions,
) => {
  if (options.onRestore) {
    actions.push({
      icon: <Restore />,
      onClick: options.onRestore,
      tooltip: options.t("common:actions.restore"),
    });
  }
};

const addDeleteAction = (
  actions: FolderAction[],
  options: FolderActionOptions,
) => {
  if (options.onDelete) {
    actions.push({
      icon: <Delete />,
      onClick: options.onDelete,
      tooltip: options.t("common:actions.delete"),
    });
  }
};

export const FolderCard = ({
  folder,
  isRenaming,
  renamingName,
  onRenamingNameChange,
  onConfirmRename,
  onCancelRename,
  onStartRename,
  onRestore,
  onDelete,
  onDownload,
  onShare,
  onCut,
  onToggleEncryptionPolicy,
  encryptionPolicy,
  onClick,
  variant = "default",
  readOnly = false,
}: FolderCardProps) => {
  const { t } = useTranslation(["files", "common"]);
  const explicitEncryptionPolicyEnabled =
    encryptionPolicy?.explicitEnabled ??
    isFolderEncryptionPolicyEnabled(folder.metadata);
  const effectiveEncryptionPolicyEnabled =
    encryptionPolicy?.effectiveEnabled ?? explicitEncryptionPolicyEnabled;
  const encryptionPolicyInherited = encryptionPolicy?.inheritedEnabled ?? false;

  const actions = useMemo(
    () =>
      buildFolderActions({
        encryptionPolicyInherited,
        explicitEncryptionPolicyEnabled,
        onCut,
        onDelete,
        onDownload,
        onRestore,
        onShare,
        onStartRename,
        onToggleEncryptionPolicy,
        readOnly,
        t,
      }),
    [
      encryptionPolicyInherited,
      explicitEncryptionPolicyEnabled,
      onCut,
      onDelete,
      onDownload,
      onRestore,
      onShare,
      onStartRename,
      onToggleEncryptionPolicy,
      readOnly,
      t,
    ],
  );

  return (
    <RenamableItemCard
      icon={getFolderIcon()}
      renamingIcon={getFolderIcon()}
      title={folder.name}
      cornerAdornment={
        effectiveEncryptionPolicyEnabled ? (
          <LockOutlined
            fontSize="small"
            titleAccess={t("common:clientEncryption.folderPolicyEnabledHint")}
          />
        ) : undefined
      }
      subtitle={new Date(folder.createdAt).toLocaleDateString()}
      onClick={onClick}
      variant={variant}
      actions={actions}
      isRenaming={isRenaming}
      renamingValue={renamingName}
      onRenamingValueChange={onRenamingNameChange}
      onConfirmRename={onConfirmRename ?? (() => {})}
      onCancelRename={onCancelRename ?? (() => {})}
      placeholder={t("actions.folderNamePlaceholder")}
    />
  );
};
