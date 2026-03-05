import { Delete, Edit, Share } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import { RenamableItemCard } from "./RenamableItemCard";
import { getFolderIcon } from "../utils/icons";

interface FolderCardProps {
  folder: NodeDto;
  isRenaming: boolean;
  renamingName: string;
  onRenamingNameChange: (name: string) => void;
  onConfirmRename?: () => void;
  onCancelRename?: () => void;
  onStartRename?: () => void;
  onDelete?: () => void;
  onShare?: () => void;
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
  onDelete,
  onShare,
  onClick,
  variant = "default",
  readOnly = false,
}: FolderCardProps) => {
  const { t } = useTranslation(["files", "common"]);

  return (
    <RenamableItemCard
      icon={getFolderIcon()}
      renamingIcon={getFolderIcon()}
      title={folder.name}
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
