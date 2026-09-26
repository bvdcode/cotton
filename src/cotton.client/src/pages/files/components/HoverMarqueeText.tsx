import { Box } from "@mui/material";
import type { SxProps, Theme } from "@mui/material/styles";
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";

export const HoverMarqueeText = ({
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
