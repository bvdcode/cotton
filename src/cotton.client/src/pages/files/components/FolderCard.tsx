import {
  ContentCut,
  Delete,
  Edit,
  LockOpenOutlined,
  LockOutlined,
  Restore,
  Share,
} from "@mui/icons-material";
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
  onShare?: () => void;
  onCut?: () => void;
  onToggleEncryptionPolicy?: () => void;
  encryptionPolicy?: FolderEncryptionPolicyState;
  onClick: (event?: React.SyntheticEvent) => void;
  variant?: "default" | "squareTile";
  readOnly?: boolean;
}

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
      actions={[
        ...(!readOnly && onShare
          ? [
              {
                icon: <Share />,
                onClick: onShare,
                tooltip: t("common:actions.share"),
              },
            ]
          : []),
        ...(!readOnly && onStartRename
          ? [
              {
                icon: <Edit />,
                onClick: onStartRename,
                tooltip: t("common:actions.rename"),
              },
            ]
          : []),
        ...(!readOnly && onCut
          ? [
              {
                icon: <ContentCut />,
                onClick: onCut,
                tooltip: t("files:move.cut"),
              },
            ]
          : []),
        ...(!readOnly && onToggleEncryptionPolicy && !encryptionPolicyInherited
          ? [
              {
                icon: explicitEncryptionPolicyEnabled ? (
                  <LockOpenOutlined />
                ) : (
                  <LockOutlined />
                ),
                onClick: onToggleEncryptionPolicy,
                tooltip: explicitEncryptionPolicyEnabled
                  ? t("files:clientEncryption.disablePolicy")
                  : t("files:clientEncryption.enablePolicy"),
              },
            ]
          : []),
        ...(!readOnly && onRestore
          ? [
              {
                icon: <Restore />,
                onClick: onRestore,
                tooltip: t("common:actions.restore"),
              },
            ]
          : []),
        ...(!readOnly && onDelete
          ? [
              {
                icon: <Delete />,
                onClick: onDelete,
                tooltip: t("common:actions.delete"),
              },
            ]
          : []),
      ]}
      isRenaming={isRenaming}
      renamingValue={renamingName}
      onRenamingValueChange={onRenamingNameChange}
      onConfirmRename={onConfirmRename ?? (() => {})}
      onCancelRename={onCancelRename ?? (() => {})}
      placeholder={t("actions.folderNamePlaceholder")}
    />
  );
};
