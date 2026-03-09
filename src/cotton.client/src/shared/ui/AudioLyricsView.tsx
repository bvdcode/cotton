import React from "react";
import { Box, Typography, useMediaQuery } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import type { LrcLine } from "../utils/lrc";

const DESKTOP_LINE_HEIGHT_PX = 28;
const MOBILE_LINE_HEIGHT_PX = 36;
const VISIBLE_LINES = 3;
const HIGHLIGHT_SWAP_DELAY_MS = 180;

const clamp = (value: number, min: number, max: number): number => {
  return Math.min(Math.max(value, min), max);
};

interface AudioLyricsViewProps {
  lines: ReadonlyArray<LrcLine>;
  activeIndex: number;
  isActivePlaying?: boolean;
}

export const AudioLyricsView: React.FC<AudioLyricsViewProps> = ({
  lines,
  activeIndex,
  isActivePlaying = true,
}) => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const lineHeightPx = isMobile ? MOBILE_LINE_HEIGHT_PX : DESKTOP_LINE_HEIGHT_PX;

  const [highlightIndex, setHighlightIndex] = React.useState(activeIndex);
  const highlightTimerRef = React.useRef<number | null>(null);

  React.useEffect(() => {
    if (!isActivePlaying) {
      setHighlightIndex(activeIndex);
      return;
    }

    if (highlightIndex === activeIndex) {
      return;
    }

    if (highlightTimerRef.current !== null) {
      window.clearTimeout(highlightTimerRef.current);
      highlightTimerRef.current = null;
    }

    highlightTimerRef.current = window.setTimeout(() => {
      setHighlightIndex(activeIndex);
      highlightTimerRef.current = null;
    }, HIGHLIGHT_SWAP_DELAY_MS);
  }, [activeIndex, highlightIndex, isActivePlaying]);

  React.useEffect(() => {
    return () => {
      if (highlightTimerRef.current !== null) {
        window.clearTimeout(highlightTimerRef.current);
      }
    };
  }, []);

  const maxOffset = Math.max(0, lines.length - VISIBLE_LINES);
  const offsetLines = clamp(activeIndex - 1, 0, maxOffset);
  const translateY = offsetLines * lineHeightPx;

  return (
    <Box
      height={lineHeightPx * VISIBLE_LINES}
      overflow="hidden"
      position="relative"
      width="100%"
    >
      <Box
        sx={{
          transform: `translateY(-${translateY}px)`,
          transition: "transform 350ms ease",
          willChange: "transform",
        }}
      >
        {lines.map((line, idx) => {
          const isActive = idx === highlightIndex;
          const activeAndPlaying = isActive && isActivePlaying;
          const distance = Math.abs(idx - activeIndex);
          const baseOpacity = distance === 0 ? 1 : distance === 1 ? 0.45 : 0.1;
          return (
            <Box
              key={`${line.timeSeconds}-${idx}`}
              height={lineHeightPx}
              display="flex"
              alignItems="center"
              justifyContent="center"
            >
              <Typography
                variant={activeAndPlaying ? "h6" : "body2"}
                fontWeight={activeAndPlaying ? 800 : isActive ? 500 : 400}
                color={activeAndPlaying ? "text.primary" : "text.secondary"}
                width="100%"
                textAlign="center"
                sx={{
                  px: 1,
                  opacity: baseOpacity,
                  lineHeight: 1.05,
                  whiteSpace: "normal",
                  overflow: "hidden",
                  display: "block",
                  ...(activeAndPlaying
                    ? {
                        fontSize: { xs: "1.25rem", sm: "1.35rem" },
                      }
                    : {
                        fontSize: { xs: "0.95rem", sm: "1rem" },
                      }),
                }}
              >
                {line.text || "\u00A0"}
              </Typography>
            </Box>
          );
        })}
      </Box>
    </Box>
  );
};
