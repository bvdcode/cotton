import { Delete, Edit, Folder } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import { RenamableItemCard } from "./RenamableItemCard";

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
}: FolderCardProps) => {
  const { t } = useTranslation(["files", "common"]);

  return (
    <RenamableItemCard
      icon={<Folder fontSize="large" />}
      renamingIcon={<Folder sx={{ color: "primary.main" }} />}
      title={folder.name}
      subtitle={new Date(folder.createdAt).toLocaleDateString()}
      onClick={onClick}
      actions={[
        {
          icon: <Edit fontSize="small" />,
          onClick: onStartRename,
          tooltip: t("common:actions.rename"),
        },
        {
          icon: <Delete fontSize="small" />,
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
