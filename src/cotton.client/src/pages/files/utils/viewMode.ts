import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import type { TilesSize } from "../types/FileListViewTypes";

export type FileBrowserViewMode =
  | "table"
  | "tiles-small"
  | "tiles-medium"
  | "tiles-large";

const NEXT_VIEW_MODE: Record<FileBrowserViewMode, FileBrowserViewMode> = {
  table: "tiles-small",
  "tiles-small": "tiles-medium",
  "tiles-medium": "tiles-large",
  "tiles-large": "table",
};

const NEXT_VIEW_MODE_TITLE_KEY: Record<FileBrowserViewMode, string> = {
  table: "actions.switchToSmallTilesView",
  "tiles-small": "actions.switchToMediumTilesView",
  "tiles-medium": "actions.switchToLargeTilesView",
  "tiles-large": "actions.switchToTableView",
};

export const getFileBrowserViewMode = (
  layoutType: InterfaceLayoutType,
  tilesSize: TilesSize,
): FileBrowserViewMode => {
  if (layoutType === InterfaceLayoutType.List) return "table";
  if (tilesSize === "small") return "tiles-small";
  if (tilesSize === "large") return "tiles-large";
  return "tiles-medium";
};

export const getNextFileBrowserViewMode = (
  viewMode: FileBrowserViewMode,
): FileBrowserViewMode => NEXT_VIEW_MODE[viewMode];

export const getNextFileBrowserViewTitleKey = (
  viewMode: FileBrowserViewMode,
): string => NEXT_VIEW_MODE_TITLE_KEY[viewMode];

export const setFileBrowserViewMode = (
  viewMode: FileBrowserViewMode,
  setLayoutType: (layoutType: InterfaceLayoutType) => void,
  setTilesSize: (tilesSize: TilesSize) => void,
): void => {
  switch (viewMode) {
    case "table":
      setLayoutType(InterfaceLayoutType.List);
      return;
    case "tiles-small":
      setLayoutType(InterfaceLayoutType.Tiles);
      setTilesSize("small");
      return;
    case "tiles-medium":
      setLayoutType(InterfaceLayoutType.Tiles);
      setTilesSize("medium");
      return;
    case "tiles-large":
      setLayoutType(InterfaceLayoutType.Tiles);
      setTilesSize("large");
  }
};

export const cycleFileBrowserViewMode = (
  currentViewMode: FileBrowserViewMode,
  setLayoutType: (layoutType: InterfaceLayoutType) => void,
  setTilesSize: (tilesSize: TilesSize) => void,
): void => {
  const nextViewMode = getNextFileBrowserViewMode(currentViewMode);
  setFileBrowserViewMode(nextViewMode, setLayoutType, setTilesSize);
};

export const getTilesIconScale = (viewMode: FileBrowserViewMode): number => {
  if (viewMode === "tiles-small") return 0.9;
  if (viewMode === "tiles-large") return 1.1;
  return 1;
};
