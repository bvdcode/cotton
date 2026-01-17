import { Box, TextField, Typography } from "@mui/material";
import type { SxProps, Theme } from "@mui/material/styles";
import type { ReactNode } from "react";
import { FileSystemItemCard } from "./FileSystemItemCard";
import type { FileSystemItemCardAction } from "./FileSystemItemCard";

interface RenamableItemCardProps {
  icon: ReactNode;
  renamingIcon?: ReactNode;
  title: string;
  subtitle?: string;
  onClick?: () => void;
  actions?: FileSystemItemCardAction[];
  iconContainerSx?: SxProps<Theme>;
  sx?: SxProps<Theme>;

  isRenaming: boolean;
  renamingValue: string;
  onRenamingValueChange: (value: string) => void;
  onConfirmRename: () => void;
  onCancelRename: () => void;
  placeholder?: string;
}

export const RenamableItemCard = ({
  icon,
  renamingIcon,
  title,
  subtitle,
  onClick,
  actions,
  iconContainerSx,
  sx,
  isRenaming,
  renamingValue,
  onRenamingValueChange,
  onConfirmRename,
  onCancelRename,
  placeholder,
}: RenamableItemCardProps) => {
  if (!isRenaming) {
    return (
      <FileSystemItemCard
        icon={icon}
        title={title}
        subtitle={subtitle}
        onClick={onClick}
        actions={actions}
        iconContainerSx={iconContainerSx}
        sx={sx}
      />
    );
  }

  return (
    <Box
      sx={{
        border: "1px solid",
        borderColor: "primary.main",
        borderRadius: 2,
        p: {
          xs: 1,
          sm: 1.25,
          md: 1,
        },
        bgcolor: "action.hover",
        overflow: "hidden",
        ...sx,
      }}
    >
      <Box
        sx={{
          width: "100%",
          aspectRatio: "1 / 1",
          position: "relative",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          borderRadius: 1.5,
          overflow: "hidden",
          mb: 0.75,
        }}
      >
        <Box
          sx={{
            width: "100%",
            height: "100%",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            "& > svg": {
              width: "70%",
              height: "70%",
            },
            ...iconContainerSx,
          }}
        >
          {renamingIcon ?? icon}
        </Box>
      </Box>

      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
        <TextField
          autoFocus
          fullWidth
          size="small"
          value={renamingValue}
          onChange={(e) => onRenamingValueChange(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              onConfirmRename();
            } else if (e.key === "Escape") {
              onCancelRename();
            }
          }}
          onBlur={onConfirmRename}
          placeholder={placeholder}
          slotProps={{
            input: {
              sx: {
                fontSize: { xs: "0.8rem", md: "0.85rem" },
              },
            },
          }}
        />
      </Box>

      {subtitle && (
        <Typography
          variant="caption"
          color="text.secondary"
          display="block"
          noWrap
          title={subtitle}
          sx={{ mt: 0.5, fontSize: { xs: "0.7rem", md: "0.75rem" }, lineHeight: 1.4 }}
        >
          {subtitle}
        </Typography>
      )}
    </Box>
  );
};
