import { Delete, Edit } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import { RenamableItemCard } from "./RenamableItemCard";
import { getFolderIcon } from "../utils/icons";

interface FolderCardProps {
  folder: NodeDto;
  isRenaming: boolean;
  renamingName: string;
  onRenamingNameChange: (name: string) => void;
  onConfirmRename: () => void;
  onCancelRename: () => void;
  onStartRename: () => void;
  onDelete: () => void;
  onClick: () => void;
  variant?: "default" | "squareTile";
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
  onClick,
  variant = "default",
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
        {
          icon: <Edit />,
          onClick: onStartRename,
          tooltip: t("common:actions.rename"),
        },
        {
          icon: <Delete />,
          onClick: onDelete,
          tooltip: t("common:actions.delete"),
        },
      ]}
      isRenaming={isRenaming}
      renamingValue={renamingName}
      onRenamingValueChange={onRenamingNameChange}
      onConfirmRename={onConfirmRename}
      onCancelRename={onCancelRename}
      placeholder={t("actions.folderNamePlaceholder")}
    />
  );
};
