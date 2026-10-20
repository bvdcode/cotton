import { Box, Menu, MenuItem, Typography } from "@mui/material";
import type { SxProps, Theme } from "@mui/material/styles";
import type { ReactNode, MouseEvent } from "react";
import { memo, useState } from "react";

export interface FileSystemItemCardAction {
  icon: ReactNode;
  onClick: () => void;
  tooltip?: string;
}

export interface FileSystemItemCardProps {
  icon: ReactNode;
  title: string;
  titleContent?: ReactNode;
  titleAdornment?: ReactNode;
  cornerAdornment?: ReactNode;
  subtitle?: string;
  subtitleEnd?: string;
  onClick?: (event?: React.SyntheticEvent) => void;
  actions?: FileSystemItemCardAction[];
  iconContainerSx?: SxProps<Theme>;
  sx?: SxProps<Theme>;
  variant?: "default" | "squareTile";
}

import { CardText } from "./CardText";

const useActionMenu = () => {
  const [menuAnchorEl, setMenuAnchorEl] = useState<HTMLElement | null>(null);
  const actionsOpen = Boolean(menuAnchorEl);

  const handleToggleActions = (event: MouseEvent<HTMLButtonElement>) => {
    event.stopPropagation();
    setMenuAnchorEl(actionsOpen ? null : event.currentTarget);
  };
  const handleCloseMenu = () => {
    setMenuAnchorEl(null);
  };
  const runAction = (action: FileSystemItemCardAction) => {
    setMenuAnchorEl(null);
    action.onClick();
  };

  return {
    actionsOpen,
    handleCloseMenu,
    handleToggleActions,
    menuAnchorEl,
    runAction,
  };
};

const CardArtwork = ({
  cornerAdornment,
  icon,
  iconContainerSx,
  variant,
}: Pick<
  FileSystemItemCardProps,
  "cornerAdornment" | "icon" | "iconContainerSx" | "variant"
>): React.ReactElement => (
  <Box
    sx={{
      width: "100%",
      ...(variant === "squareTile"
        ? { flex: 1, minHeight: 0 }
        : { aspectRatio: "1 / 1" }),
      position: "relative",
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      overflow: "hidden",
    }}
  >
    {cornerAdornment && (
      <Box
        sx={{
          position: "absolute",
          top: 6,
          right: 6,
          zIndex: 2,
          display: "inline-flex",
          alignItems: "center",
          justifyContent: "center",
          minWidth: 22,
          gap: 0.5,
          px: 0.25,
          height: 22,
          color: "primary.main",
          "& > svg": {
            fontSize: 16,
          },
        }}
      >
        {cornerAdornment}
      </Box>
    )}
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
      {icon}
    </Box>
  </Box>
);

const CardActionsMenu = ({
  actions,
  anchorEl,
  onClose,
  onRunAction,
  open,
}: {
  actions: ReadonlyArray<FileSystemItemCardAction>;
  anchorEl: HTMLElement | null;
  onClose: () => void;
  onRunAction: (action: FileSystemItemCardAction) => void;
  open: boolean;
}): React.ReactElement => (
  <Menu
    anchorEl={anchorEl}
    open={open}
    onClose={onClose}
    onClick={(event) => event.stopPropagation()}
    anchorOrigin={{ vertical: "top", horizontal: "right" }}
    transformOrigin={{ vertical: "bottom", horizontal: "right" }}
    disablePortal={false}
    slotProps={{
      paper: {
        variant: "outlined",
        sx: {
          bgcolor: "background.default",
          borderColor: "divider",
        },
      },
    }}
  >
    {actions.map((action, index) => (
      <MenuItem
        key={index}
        onClick={(event) => {
          event.stopPropagation();
          onRunAction(action);
        }}
        title={action.tooltip}
        sx={{ gap: 1 }}
      >
        {action.icon}
        <Typography variant="body2">{action.tooltip ?? ""}</Typography>
      </MenuItem>
    ))}
  </Menu>
);

const FileSystemItemCardImpl = ({
  icon,
  title,
  titleContent,
  titleAdornment,
  cornerAdornment,
  subtitle,
  subtitleEnd,
  onClick,
  actions = [],
  iconContainerSx,
  sx,
  variant = "default",
}: FileSystemItemCardProps) => {
  const clickable = typeof onClick === "function";
  const [isHovered, setIsHovered] = useState(false);
  const hasActions = actions.length > 0;
  const {
    actionsOpen,
    handleCloseMenu,
    handleToggleActions,
    menuAnchorEl,
    runAction,
  } = useActionMenu();

  return (
    <Box
      role={clickable ? "button" : undefined}
      tabIndex={clickable ? 0 : undefined}
      onClick={onClick}
      onContextMenu={(event) => {
        event.preventDefault();
      }}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      onKeyDown={(event) => {
        if (!clickable) return;
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          onClick?.(event);
        }
      }}
      sx={[
        {
          position: "relative",
          overflow: "hidden",
          border: "1px solid",
          borderColor: "divider",
          borderRadius: 1,
          ...(variant === "squareTile" && {
            aspectRatio: "1 / 1",
            display: "flex",
            flexDirection: "column",
          }),
          cursor: clickable ? "pointer" : "default",
          userSelect: "none",
          WebkitTapHighlightColor: "transparent",
          outline: "none",
          "&:hover": clickable ? { bgcolor: "action.hover" } : undefined,
          ...(hasActions
            ? {
                "&:hover .card-menu-slot, &:focus-within .card-menu-slot": {
                  width: 28,
                  opacity: 1,
                  pointerEvents: "auto",
                },
              }
            : undefined),
          "&:focus-visible": clickable
            ? {
                borderColor: "primary.main",
                boxShadow: "none",
                bgcolor: "action.hover",
              }
            : undefined,
        },
        ...(Array.isArray(sx) ? sx : [sx ?? {}]),
      ]}
    >
      <CardArtwork
        cornerAdornment={cornerAdornment}
        icon={icon}
        iconContainerSx={iconContainerSx}
        variant={variant}
      />
      <CardText
        actionsOpen={actionsOpen}
        cardHovered={isHovered}
        hasActions={hasActions}
        onToggleActions={handleToggleActions}
        subtitle={subtitle}
        subtitleEnd={subtitleEnd}
        title={title}
        titleContent={titleContent}
        titleAdornment={titleAdornment}
        variant={variant}
      />
      {hasActions && (
        <CardActionsMenu
          actions={actions}
          anchorEl={menuAnchorEl}
          onClose={handleCloseMenu}
          onRunAction={runAction}
          open={actionsOpen}
        />
      )}
    </Box>
  );
};

export const FileSystemItemCard = memo(FileSystemItemCardImpl);
