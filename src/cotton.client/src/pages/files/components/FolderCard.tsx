import { Box, TextField } from "@mui/material";
import { Delete, Edit, Folder } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { NodeDto } from "../../../shared/api/layoutsApi";
import { FileSystemItemCard } from "./FileSystemItemCard";

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

  if (isRenaming) {
    return (
      <Box
        sx={{
          border: "2px solid",
          borderColor: "primary.main",
          borderRadius: 2,
          p: {
            xs: 1,
            sm: 1.25,
            md: 1,
          },
          bgcolor: "action.hover",
        }}
      >
        <Box
          sx={{
            width: "100%",
            aspectRatio: "1 / 1",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            borderRadius: 1.5,
            overflow: "hidden",
            mb: 0.75,
            "& > svg": {
              width: "70%",
              height: "70%",
            },
          }}
        >
          <Folder sx={{ color: "primary.main" }} />
        </Box>
        <TextField
          autoFocus
          fullWidth
          size="small"
          value={renamingName}
          onChange={(e) => onRenamingNameChange(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              onConfirmRename();
            } else if (e.key === "Escape") {
              onCancelRename();
            }
          }}
          onBlur={onConfirmRename}
          placeholder={t("actions.folderNamePlaceholder")}
          slotProps={{
            input: {
              sx: {
                fontSize: { xs: "0.8rem", md: "0.85rem" },
              },
            },
          }}
        />
      </Box>
    );
  }

  return (
    <FileSystemItemCard
      icon={<Folder fontSize="large" />}
      title={folder.name}
      onClick={onClick}
      subtitle={new Date(folder.createdAt).toLocaleDateString()}
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
    />
  );
};
