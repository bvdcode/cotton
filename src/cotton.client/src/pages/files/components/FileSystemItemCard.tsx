import { Box, IconButton, Menu, MenuItem, Typography } from "@mui/material";
import { MoreVert } from "@mui/icons-material";
import type { SxProps, Theme } from "@mui/material/styles";
import type { ReactNode, MouseEvent } from "react";
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";

export interface FileSystemItemCardAction {
  icon: ReactNode;
  onClick: () => void;
  tooltip?: string;
}

export interface FileSystemItemCardProps {
  icon: ReactNode;
  title: string;
  subtitle?: string;
  onClick?: () => void;
  actions?: FileSystemItemCardAction[];
  iconContainerSx?: SxProps<Theme>;
  sx?: SxProps<Theme>;
  variant?: "default" | "squareTile";
}

const HoverMarqueeText = ({
  text,
  sx,
  cardHovered = false,
}: {
  text: string;
  sx?: SxProps<Theme>;
  cardHovered?: boolean;
}) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const textRef = useRef<HTMLSpanElement | null>(null);
  const hoverTimerRef = useRef<number | null>(null);
  const overflowingRef = useRef(false);
  const animateRef = useRef(false);
  const [isOverflowing, setIsOverflowing] = useState(false);
  const [distancePx, setDistancePx] = useState(0);
  const [animate, setAnimate] = useState(false);

  const durationSeconds = useMemo(() => {
    // Keep it slow and readable; tune by distance.
    // ~40px/s with bounds.
    const seconds = distancePx > 0 ? distancePx / 40 : 0;
    return Math.max(4, Math.min(14, seconds));
  }, [distancePx]);

  useEffect(() => {
    animateRef.current = animate;
  }, [animate]);

  const measure = useCallback(() => {
    const container = containerRef.current;
    const inner = textRef.current;
    if (!container || !inner) return;

    const available = container.clientWidth;
    const needed = inner.scrollWidth;
    const distance = Math.max(0, needed - available);
    const overflow = distance > 1;
    overflowingRef.current = overflow;
    setIsOverflowing(overflow);
    setDistancePx(distance);

    if (!overflow && animateRef.current) {
      setAnimate(false);
    }
  }, []);

  useLayoutEffect(() => {
    measure();
  }, [text, measure]);

  useEffect(() => {
    if (!containerRef.current) return;
    const ro = new ResizeObserver(() => measure());
    ro.observe(containerRef.current);
    return () => ro.disconnect();
  }, [measure]);

  useEffect(() => {
    return () => {
      if (hoverTimerRef.current) {
        window.clearTimeout(hoverTimerRef.current);
        hoverTimerRef.current = null;
      }
    };
  }, []);

  // React to cardHovered prop changes
  useEffect(() => {
    if (hoverTimerRef.current) {
      window.clearTimeout(hoverTimerRef.current);
      hoverTimerRef.current = null;
    }

    if (cardHovered) {
      hoverTimerRef.current = window.setTimeout(() => {
        if (overflowingRef.current) {
          setAnimate(true);
        }
      }, 300);
    } else {
      // Use a microtask to avoid synchronous setState in effect
      Promise.resolve().then(() => {
        setAnimate(false);
      });
    }
  }, [cardHovered]);

  return (
    <Box
      ref={containerRef}
      sx={{
        display: "flex",
        alignItems: "center",
        lineHeight: "inherit",
        minWidth: 0,
        overflow: "hidden",
        whiteSpace: "nowrap",
        ...sx,
      }}
    >
      <Box
        component="span"
        ref={textRef}
        sx={{
          display: "block",
          lineHeight: "inherit",
          maxWidth: animate ? "none" : "100%",
          overflow: animate ? "visible" : "hidden",
          textOverflow: animate ? "clip" : "ellipsis",
          whiteSpace: "nowrap",
          willChange: animate ? "transform" : "auto",
          transform: "translate3d(0,0,0)",
          "--marquee-distance": `${distancePx}px`,
          "--marquee-duration": `${durationSeconds}s`,
          ...(animate &&
            isOverflowing && {
              animation:
                "fsCardMarquee var(--marquee-duration) linear infinite",
            }),
          "@keyframes fsCardMarquee": {
            "0%": { transform: "translate3d(0,0,0)" },
            "10%": { transform: "translate3d(0,0,0)" },
            "45%": {
              transform: "translate3d(calc(-1 * var(--marquee-distance)),0,0)",
            },
            "60%": {
              transform: "translate3d(calc(-1 * var(--marquee-distance)),0,0)",
            },
            "90%": { transform: "translate3d(0,0,0)" },
            "100%": { transform: "translate3d(0,0,0)" },
          },
        }}
      >
        {text}
      </Box>
    </Box>
  );
};

export const FileSystemItemCard = ({
  icon,
  title,
  subtitle,
  onClick,
  actions,
  iconContainerSx,
  sx,
  variant = "default",
}: FileSystemItemCardProps) => {
  const clickable = typeof onClick === "function";
  const [menuAnchorEl, setMenuAnchorEl] = useState<HTMLElement | null>(null);
  const [isHovered, setIsHovered] = useState(false);
  const hasActions = Boolean(actions && actions.length > 0);

  const actionsOpen = Boolean(menuAnchorEl);

  const handleToggleActions = (e: MouseEvent<HTMLButtonElement>) => {
    e.stopPropagation();
    setMenuAnchorEl(actionsOpen ? null : e.currentTarget);
  };

  const handleCloseMenu = () => {
    setMenuAnchorEl(null);
  };

  return (
    <Box
      role={clickable ? "button" : undefined}
      tabIndex={clickable ? 0 : undefined}
      onClick={onClick}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      onKeyDown={(e) => {
        if (!clickable) return;
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onClick?.();
        }
      }}
      sx={{
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
              boxShadow: (theme) => `0 0 0 2px ${theme.palette.primary.main}`,
            }
          : undefined,
        ...sx,
      }}
    >
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
      <Box
        sx={{
          px: 0.75,
          pb: 0.75,
        }}
      >
        <Box
          sx={{
            position: "relative",
            display: "flex",
            alignItems: "center",
            minHeight: 28,
            gap: 0.5,
          }}
        >
          <Typography
            component="div"
            variant="body2"
            fontWeight={500}
            sx={{
              flex: 1,
              minWidth: 0,
              fontSize: { xs: "0.8rem", md: "0.85rem" },
              lineHeight: 1.4,
            }}
          >
            <HoverMarqueeText text={title} cardHovered={isHovered} />
          </Typography>

          {hasActions && (
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
              onClick={(e) => e.stopPropagation()}
            >
              <Box sx={{ position: "relative", width: 28, height: 28 }}>
                <IconButton
                  size="small"
                  onClick={handleToggleActions}
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
          )}
        </Box>

        {subtitle && (
          <Typography
            variant="caption"
            color="text.secondary"
            display="block"
            noWrap
            title={subtitle}
            sx={{ fontSize: { xs: "0.7rem", md: "0.75rem" }, lineHeight: 1.4 }}
          >
            {subtitle}
          </Typography>
        )}
      </Box>

      {hasActions && (
        <Menu
          anchorEl={menuAnchorEl}
          open={actionsOpen}
          onClose={handleCloseMenu}
          onClick={(e) => e.stopPropagation()}
          anchorOrigin={{ vertical: "top", horizontal: "right" }}
          transformOrigin={{ vertical: "bottom", horizontal: "right" }}
          disablePortal={false}
          PaperProps={{
            variant: "outlined",
            sx: {
              bgcolor: "background.default",
              borderColor: "divider",
            },
          }}
        >
          {actions?.map((action, idx) => (
            <MenuItem
              key={idx}
              onClick={(e) => {
                e.stopPropagation();
                setMenuAnchorEl(null);
                action.onClick();
              }}
              title={action.tooltip}
              sx={{ gap: 1 }}
            >
              {action.icon}
              <Typography variant="body2">
                {action.tooltip ?? ""}
              </Typography>
            </MenuItem>
          ))}
        </Menu>
      )}
    </Box>
  );
};
