import { Box, IconButton, Typography } from "@mui/material";
import { MoreVert } from "@mui/icons-material";
import type { MouseEvent, ReactNode } from "react";
import type { FileSystemItemCardProps } from "./FileSystemItemCard";
import { HoverMarqueeText } from "./HoverMarqueeText";

const CardActionButton = ({
  actionsOpen,
  onToggleActions,
}: {
  actionsOpen: boolean;
  onToggleActions: (event: MouseEvent<HTMLButtonElement>) => void;
}): React.ReactElement => (
  <Box
    className="card-menu-slot"
    sx={{
      width: 0,
      height: 28,
      overflow: "visible",
      opacity: 0,
      pointerEvents: "auto",
      transition: "width 0.2s, opacity 0.2s",
      flex: "0 0 auto",
      ...(actionsOpen && {
        width: 28,
        opacity: 1,
      }),
    }}
    onClick={(event) => event.stopPropagation()}
  >
    <Box sx={{ position: "relative", width: 28, height: 28 }}>
      <IconButton
        size="small"
        onClick={onToggleActions}
        aria-haspopup="menu"
        aria-expanded={actionsOpen ? true : undefined}
        className="card-menu-button"
        sx={{
          p: 0.5,
          width: 28,
          height: 28,
          transition: "transform 0.3s",
          transform: actionsOpen ? "rotate(90deg)" : "rotate(0deg)",
        }}
      >
        <MoreVert />
      </IconButton>
    </Box>
  </Box>
);

export const CardText = ({
  actionsOpen,
  cardHovered,
  hasActions,
  onToggleActions,
  subtitle,
  subtitleEnd,
  title,
  titleContent,
  titleAdornment,
  variant,
}: {
  actionsOpen: boolean;
  cardHovered: boolean;
  hasActions: boolean;
  onToggleActions: (event: MouseEvent<HTMLButtonElement>) => void;
  subtitle?: string;
  subtitleEnd?: string;
  title: string;
  titleContent?: ReactNode;
  titleAdornment?: ReactNode;
  variant: FileSystemItemCardProps["variant"];
}): React.ReactElement => (
  <Box sx={{ px: 0.75, pb: 0.75 }}>
    <Box
      sx={{
        position: "relative",
        display: "flex",
        alignItems: "center",
        minHeight: 28,
        gap: 0.5,
      }}
    >
      {titleAdornment && (
        <Box
          sx={{
            display: "inline-flex",
            alignItems: "center",
            flexShrink: 0,
            color: "text.secondary",
          }}
        >
          {titleAdornment}
        </Box>
      )}
      <Typography
        component="div"
        variant="body2"
        sx={{
          fontWeight: 500,
          flex: 1,
          minWidth: 0,
          fontSize: { xs: "0.8rem", md: "0.85rem" },
          lineHeight: 1.4,
        }}
      >
        {titleContent ?? (
          <HoverMarqueeText text={title} cardHovered={cardHovered} />
        )}
      </Typography>
      {hasActions && (
        <CardActionButton
          actionsOpen={actionsOpen}
          onToggleActions={onToggleActions}
        />
      )}
    </Box>
    {(subtitle || subtitleEnd || variant === "squareTile") && (
      <Typography
        variant="caption"
        component="div"
        sx={{
          color: "text.secondary",
          display: "flex",
          justifyContent: "space-between",
          columnGap: 0.5,
          fontSize: { xs: "0.7rem", md: "0.75rem" },
          lineHeight: 1.4,
          minHeight: "1.4em",
        }}
      >
        <Box
          component="span"
          title={subtitle}
          sx={{
            flex: 1,
            minWidth: 0,
            overflow: "hidden",
            whiteSpace: "nowrap",
            textOverflow: "ellipsis",
          }}
        >
          {subtitle}
        </Box>
        {subtitleEnd && (
          <Box
            component="span"
            title={subtitleEnd}
            sx={{
              flexShrink: 0,
              ml: "auto",
            }}
          >
            {subtitleEnd}
          </Box>
        )}
      </Typography>
    )}
  </Box>
);
