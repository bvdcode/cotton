import React from "react";
import { Box, Typography, useMediaQuery } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import type { LrcLine } from "../utils/lrc";

const DESKTOP_LINE_HEIGHT_PX = 32;
const MOBILE_LINE_HEIGHT_PX = 44;
const VISIBLE_LINES = 2;

const clamp = (value: number, min: number, max: number): number => {
  return Math.min(Math.max(value, min), max);
};

interface AudioLyricsViewProps {
  lines: ReadonlyArray<LrcLine>;
  activeIndex: number;
  isActivePlaying?: boolean;
  translateExtraPx?: number;
  activeOpacity?: number;
}

export const AudioLyricsView: React.FC<AudioLyricsViewProps> = ({
  lines,
  activeIndex,
  isActivePlaying = true,
  translateExtraPx = 0,
  activeOpacity = 1,
}) => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const lineHeightPx = isMobile ? MOBILE_LINE_HEIGHT_PX : DESKTOP_LINE_HEIGHT_PX;

  const maxOffset = Math.max(0, lines.length - VISIBLE_LINES);
  const offsetLines = clamp(activeIndex, 0, maxOffset);
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
          transform: `translateY(${translateExtraPx}px) translateY(-${translateY}px)`,
          transition: "transform 350ms ease",
          willChange: "transform",
        }}
      >
        {lines.map((line, idx) => {
          const isActive = idx === activeIndex;
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
                variant={isActive ? "subtitle1" : "body2"}
                fontWeight={isActive ? 800 : 400}
                color={isActive ? "text.primary" : "text.secondary"}
                width="100%"
                textAlign="center"
                sx={{
                  px: 1,
                  opacity:
                    isActive ? baseOpacity * activeOpacity : baseOpacity,
                  ...(isActive && !isActivePlaying
                    ? {
                        color: theme.palette.text.secondary,
                      }
                    : null),
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
