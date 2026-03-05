import React from "react";
import { Box, Typography } from "@mui/material";
import { alpha } from "@mui/material/styles";
import type { LrcLine } from "../utils/lrc";

const LINE_HEIGHT_PX = 32;
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
  const offsetLines = clamp(activeIndex - 1, 0, Math.max(0, lines.length - 1));
  const translateY = offsetLines * LINE_HEIGHT_PX;

  return (
    <Box
      height={LINE_HEIGHT_PX * VISIBLE_LINES}
      overflow="hidden"
      position="relative"
      width="100%"
    >
      <Box
        position="absolute"
        top={0}
        left={0}
        right={0}
        height={LINE_HEIGHT_PX}
        zIndex={1}
        sx={(theme) => ({
          background: `linear-gradient(to bottom, ${alpha(
            theme.palette.background.paper,
            1,
          )} 0%, ${alpha(theme.palette.background.paper, 0)} 100%)`,
          pointerEvents: "none",
        })}
      />
      <Box
        position="absolute"
        bottom={0}
        left={0}
        right={0}
        height={LINE_HEIGHT_PX}
        zIndex={1}
        sx={(theme) => ({
          background: `linear-gradient(to top, ${alpha(
            theme.palette.background.paper,
            1,
          )} 0%, ${alpha(theme.palette.background.paper, 0)} 100%)`,
          pointerEvents: "none",
        })}
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
              height={LINE_HEIGHT_PX}
              display="flex"
              alignItems="center"
              justifyContent="center"
            >
              <Typography
                variant={isActive ? "subtitle1" : "body2"}
                noWrap
                fontWeight={isActive ? 700 : 400}
                color={isActive ? "text.primary" : "text.secondary"}
                width="100%"
                textAlign="center"
                sx={(theme) => ({
                  opacity: isActive ? 1 : 0.45,
                  lineHeight: `${LINE_HEIGHT_PX}px`,
                  fontSize: isActive
                    ? theme.typography.subtitle1.fontSize
                    : theme.typography.body2.fontSize,
                })}
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
