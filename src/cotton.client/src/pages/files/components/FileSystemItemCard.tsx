import {
  Box,
  IconButton,
  Menu,
  MenuItem,
  Tooltip,
  Typography,
} from "@mui/material";
import { MoreHoriz } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { ShareLinkDialog } from "@shared/ui/ShareLinkDialog";
import type { ShareLinkTarget } from "@shared/utils/shareLinkOptions";
import type { SxProps, Theme } from "@mui/material/styles";
import type { ReactNode, MouseEvent } from "react";
import { memo, useState } from "react";

export interface FileSystemItemCardAction {
  icon: ReactNode;
  onClick: () => void;
  tooltip?: string;
  shareTarget?: ShareLinkTarget;
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
  onShareOptions,
  open,
}: {
  actions: ReadonlyArray<FileSystemItemCardAction>;
  anchorEl: HTMLElement | null;
  onClose: () => void;
  onRunAction: (action: FileSystemItemCardAction) => void;
  onShareOptions: (target: ShareLinkTarget) => void;
  open: boolean;
}): React.ReactElement => {
  const { t } = useTranslation("files");
  return (
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
          aria-haspopup={action.shareTarget ? "dialog" : undefined}
          aria-keyshortcuts={action.shareTarget ? "ArrowRight" : undefined}
          onKeyDown={(event) => {
            if (event.key === "ArrowRight" && action.shareTarget) {
              event.preventDefault();
              event.stopPropagation();
              onShareOptions(action.shareTarget);
            }
          }}
          sx={{ gap: 1 }}
        >
          {action.icon}
          <Typography variant="body2" sx={{ flex: 1 }}>
            {action.tooltip ?? ""}
          </Typography>
          {action.shareTarget && (
            <Tooltip title={t("share.options.title")}>
              <IconButton
                size="small"
                edge="end"
                aria-label={t("share.options.title")}
                tabIndex={-1}
                onKeyDown={(event) => event.stopPropagation()}
                onClick={(event) => {
                  event.stopPropagation();
                  if (action.shareTarget) {
                    onShareOptions(action.shareTarget);
                  }
                }}
              >
                <MoreHoriz fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
        </MenuItem>
      ))}
    </Menu>
  );
};

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
  const [shareTarget, setShareTarget] = useState<ShareLinkTarget | null>(null);
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
          onShareOptions={(target) => {
            handleCloseMenu();
            setShareTarget(target);
          }}
          open={actionsOpen}
        />
      )}
      {shareTarget && (
        <ShareLinkDialog
          target={shareTarget}
          onClose={() => setShareTarget(null)}
        />
      )}
    </Box>
  );
};

export const FileSystemItemCard = memo(FileSystemItemCardImpl);
