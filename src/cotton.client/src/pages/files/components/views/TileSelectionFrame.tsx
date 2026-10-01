import React from "react";
import { Box, Checkbox } from "@mui/material";
import { setClippedDragImage } from "./dragPreview";

import type { TileLongPressHandlers } from "./tileSelection";

export const SelectionCheckbox = ({
  onToggle,
  selected,
  selectionMode,
}: {
  onToggle?: (shiftKey: boolean) => void;
  selected: boolean;
  selectionMode: boolean;
}): React.ReactElement => (
  <Checkbox
    checked={selected}
    onChange={(event) => {
      const shiftKey =
        event.nativeEvent instanceof MouseEvent
          ? event.nativeEvent.shiftKey
          : false;
      onToggle?.(shiftKey);
    }}
    sx={{
      position: "absolute",
      top: 4,
      left: 4,
      zIndex: 5,
      display: selectionMode ? "inline-flex" : "none",
    }}
    size="small"
  />
);

export const TileFrame = ({
  children,
  dimmed,
  draggable,
  dropActive,
  isRenaming,
  longPressHandlers,
  onMoveDragLeave,
  onMoveDragOver,
  onMoveDragStart,
  onMoveDrop,
  selectionCheckbox,
}: {
  children: React.ReactNode;
  dimmed: boolean;
  draggable: boolean;
  dropActive?: boolean;
  isRenaming: boolean;
  longPressHandlers: TileLongPressHandlers;
  onMoveDragLeave?: (event: React.DragEvent<HTMLDivElement>) => void;
  onMoveDragOver?: (event: React.DragEvent<HTMLDivElement>) => void;
  onMoveDragStart?: (event: React.DragEvent<HTMLDivElement>) => void;
  onMoveDrop?: (event: React.DragEvent<HTMLDivElement>) => void;
  selectionCheckbox: React.ReactNode;
}): React.ReactElement => (
  <Box
    position="relative"
    draggable={draggable && !isRenaming}
    onDragStart={(event) => {
      onMoveDragStart?.(event);
      if (!event.defaultPrevented) {
        setClippedDragImage(event, event.currentTarget);
      }
    }}
    onDragOver={onMoveDragOver}
    onDragLeave={onMoveDragLeave}
    onDrop={onMoveDrop}
    onContextMenu={(event) => {
      event.preventDefault();
    }}
    {...longPressHandlers}
    sx={{
      opacity: dimmed ? 0.45 : 1,
      transition: "opacity 120ms ease-out, box-shadow 120ms ease-out",
      ...(dropActive && {
        outline: "2px solid",
        outlineColor: "primary.main",
        outlineOffset: 1,
        borderRadius: 1,
      }),
    }}
  >
    {selectionCheckbox}
    {children}
  </Box>
);
