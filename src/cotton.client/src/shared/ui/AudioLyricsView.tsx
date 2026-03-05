import React from "react";
import { Box, Typography, useMediaQuery } from "@mui/material";
import { alpha, useTheme } from "@mui/material/styles";
import type { LrcLine } from "../utils/lrc";

const DESKTOP_LINE_HEIGHT_PX = 32;
const MOBILE_LINE_HEIGHT_PX = 44;
const VISIBLE_LINES = 3;

const clamp = (value: number, min: number, max: number): number => {
  return Math.min(Math.max(value, min), max);
};

interface AudioLyricsViewProps {
  lines: ReadonlyArray<LrcLine>;
  activeIndex: number;
}

export const AudioLyricsView: React.FC<AudioLyricsViewProps> = ({
  lines,
  activeIndex,
}) => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const lineHeightPx = isMobile ? MOBILE_LINE_HEIGHT_PX : DESKTOP_LINE_HEIGHT_PX;

  const offsetLines = clamp(activeIndex - 1, 0, Math.max(0, lines.length - 1));
  const translateY = offsetLines * lineHeightPx;

  return (
    <Box
      height={lineHeightPx * VISIBLE_LINES}
      overflow="hidden"
      position="relative"
      width="100%"
    >
      <Box
        position="absolute"
        top={0}
        left={0}
        right={0}
        height={lineHeightPx}
        zIndex={1}
        sx={{
          background: `linear-gradient(to bottom, ${alpha(
            theme.palette.background.paper,
            0.7,
          )} 0%, ${alpha(theme.palette.background.paper, 0)} 100%)`,
          pointerEvents: "none",
        }}
      />
      <Box
        position="absolute"
        bottom={0}
        left={0}
        right={0}
        height={lineHeightPx}
        zIndex={1}
        sx={{
          background: `linear-gradient(to top, ${alpha(
            theme.palette.background.paper,
            0.7,
          )} 0%, ${alpha(theme.palette.background.paper, 0)} 100%)`,
          pointerEvents: "none",
        }}
      />

      <Box
        sx={{
          transform: `translateY(-${translateY}px)`,
          transition: "transform 350ms ease",
          willChange: "transform",
        }}
      >
        {lines.map((line, idx) => {
          const isActive = idx === activeIndex;
          return (
            <Box
              key={`${line.timeSeconds}-${idx}`}
              height={lineHeightPx}
              display="flex"
              alignItems="center"
              justifyContent="center"
            >
              <Typography
                variant={isActive ? "subtitle1" : "body2"}
                fontWeight={isActive ? 800 : 400}
                color={isActive ? "text.primary" : "text.secondary"}
                width="100%"
                textAlign="center"
                sx={{
                  px: 1,
                  opacity: isActive ? 1 : 0.35,
                  lineHeight: 1.15,
                  whiteSpace: "normal",
                  overflow: "hidden",
                  display: "block",
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
