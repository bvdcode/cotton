import React from "react";
import { Box } from "@mui/material";
import { Folder } from "@mui/icons-material";
import { InlineRenameField } from "../InlineRenameField";

interface NewFolderCardProps {
  newFolderName: string;
  onNewFolderNameChange: (name: string) => void;
  onConfirmNewFolder: () => Promise<void>;
  onCancelNewFolder: () => void;
  folderNamePlaceholder: string;
}

export const NewFolderCard: React.FC<NewFolderCardProps> = ({
  newFolderName,
  onNewFolderNameChange,
  onConfirmNewFolder,
  onCancelNewFolder,
  folderNamePlaceholder,
}) => (
  <Box
    sx={{
      border: "2px solid",
      borderColor: "primary.main",
      borderRadius: 1,
      aspectRatio: "1 / 1",
      display: "flex",
      flexDirection: "column",
      p: { xs: 1, sm: 1.25, md: 1 },
      bgcolor: "action.hover",
    }}
  >
    <Box
      sx={{
        width: "100%",
        flex: 1,
        minHeight: 0,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        borderRadius: 1.5,
        overflow: "hidden",
        "& > svg": { width: "70%", height: "70%" },
      }}
    >
      <Folder sx={{ color: "primary.main" }} />
    </Box>
    <Box display="flex" alignItems="center" gap={0.5}>
      <InlineRenameField
        value={newFolderName}
        onChange={onNewFolderNameChange}
        onConfirm={onConfirmNewFolder}
        onCancel={onCancelNewFolder}
        placeholder={folderNamePlaceholder}
      />
    </Box>
  </Box>
);
