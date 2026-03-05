import React from "react";
import { Box, Typography } from "@mui/material";
import type { LrcLine } from "../utils/lrc";

const LINE_HEIGHT_PX = 28;
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
              px={0.5}
            >
              <Typography
                variant="body2"
                noWrap
                fontWeight={isActive ? 700 : 400}
                color={isActive ? "text.primary" : "text.secondary"}
                width="100%"
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
