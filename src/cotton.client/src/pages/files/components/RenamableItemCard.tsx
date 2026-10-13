import type { SxProps, Theme } from "@mui/material/styles";
import type { ReactNode } from "react";
import { FileSystemItemCard } from "./FileSystemItemCard";
import type { FileSystemItemCardAction } from "./FileSystemItemCard";
import { InlineRenameField } from "./InlineRenameField";

interface RenamableItemCardProps {
  icon: ReactNode;
  renamingIcon?: ReactNode;
  title: string;
  titleAdornment?: ReactNode;
  cornerAdornment?: ReactNode;
  subtitle?: string;
  onClick?: (event?: React.SyntheticEvent) => void;
  actions?: FileSystemItemCardAction[];
  iconContainerSx?: SxProps<Theme>;
  sx?: SxProps<Theme>;
  variant?: "default" | "squareTile";

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
  titleAdornment,
  cornerAdornment,
  subtitle,
  onClick,
  actions,
  iconContainerSx,
  sx,
  variant = "default",
  isRenaming,
  renamingValue,
  onRenamingValueChange,
  onConfirmRename,
  onCancelRename,
  placeholder,
}: RenamableItemCardProps) => {
  return (
    <FileSystemItemCard
      icon={isRenaming ? (renamingIcon ?? icon) : icon}
      title={title}
      titleContent={
        isRenaming ? (
          <InlineRenameField
            value={renamingValue}
            onChange={onRenamingValueChange}
            onConfirm={onConfirmRename}
            onCancel={onCancelRename}
            placeholder={placeholder}
          />
        ) : undefined
      }
      titleAdornment={titleAdornment}
      cornerAdornment={cornerAdornment}
      subtitle={subtitle}
      onClick={isRenaming ? undefined : onClick}
      actions={isRenaming ? undefined : actions}
      iconContainerSx={iconContainerSx}
      sx={[
        {
          ...(isRenaming && {
            borderColor: "primary.main",
            bgcolor: "action.hover",
          }),
        },
        ...(Array.isArray(sx) ? sx : [sx ?? {}]),
      ]}
      variant={variant}
    />
  );
};
