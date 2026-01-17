import { Box, IconButton, Typography } from "@mui/material";
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
}

const HoverMarqueeText = ({
  text,
  sx,
}: {
  text: string;
  sx?: SxProps<Theme>;
}) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const textRef = useRef<HTMLSpanElement | null>(null);
  const hoverTimerRef = useRef<number | null>(null);
  const hoveredRef = useRef(false);
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

  return (
    <Box
      ref={containerRef}
      onMouseEnter={() => {
        hoveredRef.current = true;
        if (hoverTimerRef.current) {
          window.clearTimeout(hoverTimerRef.current);
          hoverTimerRef.current = null;
        }
        hoverTimerRef.current = window.setTimeout(() => {
          if (hoveredRef.current && overflowingRef.current) {
            setAnimate(true);
          }
        }, 1000);
      }}
      onMouseLeave={() => {
        hoveredRef.current = false;
        if (hoverTimerRef.current) {
          window.clearTimeout(hoverTimerRef.current);
          hoverTimerRef.current = null;
        }
        setAnimate(false);
      }}
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
}: FileSystemItemCardProps) => {
  const clickable = typeof onClick === "function";
  const [actionsOpen, setActionsOpen] = useState(false);
  const hasActions = Boolean(actions && actions.length > 0);

  const handleToggleActions = (e: MouseEvent<HTMLButtonElement>) => {
    e.stopPropagation();
    setActionsOpen(!actionsOpen);
  };

  const handleActionClick =
    (action: FileSystemItemCardAction) =>
    (e: MouseEvent<HTMLButtonElement>) => {
      e.stopPropagation();
      setActionsOpen(false);
      action.onClick();
    };

  return (
    <Box
      role={clickable ? "button" : undefined}
      tabIndex={clickable ? 0 : undefined}
      onClick={onClick}
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
        borderRadius: 2,
        p: {
          xs: 1,
          sm: 1.25,
          md: 1,
        },
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
          {icon}
        </Box>
      </Box>

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
            lineHeight: 1.2,
          }}
        >
          <HoverMarqueeText text={title} />
        </Typography>

        {hasActions && (
          <Box
            className="card-menu-slot"
            sx={{
              width: actionsOpen ? 28 : 0,
              height: 28,
              overflow: "hidden",
              opacity: actionsOpen ? 1 : 0,
              pointerEvents: actionsOpen ? "auto" : "none",
              transition: "width 0.2s, opacity 0.2s",
              flex: "0 0 auto",
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

              {actionsOpen && (
                <Box
                  sx={{
                    position: "absolute",
                    right: 0,
                    bottom: 32,
                    display: "flex",
                    flexDirection: "column",
                    gap: 0.75,
                    animation: "slideUp 0.2s ease-out",
                    "@keyframes slideUp": {
                      from: {
                        opacity: 0,
                        transform: "translateY(10px)",
                      },
                      to: {
                        opacity: 1,
                        transform: "translateY(0)",
                      },
                    },
                  }}
                >
                  {actions!.map((action, idx) => (
                    <IconButton
                      key={idx}
                      size="small"
                      onClick={handleActionClick(action)}
                      title={action.tooltip}
                      sx={{
                        p: 0.5,
                        width: 28,
                        height: 28,
                        "& svg": {
                          fontSize: "1rem",
                        },
                      }}
                    >
                      {action.icon}
                    </IconButton>
                  ))}
                </Box>
              )}
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
          sx={{ fontSize: { xs: "0.7rem", md: "0.75rem" } }}
        >
          {subtitle}
        </Typography>
      )}
    </Box>
  );
};
