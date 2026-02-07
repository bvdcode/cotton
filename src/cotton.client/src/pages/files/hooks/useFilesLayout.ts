import { useCallback, useEffect, useMemo, useState } from "react";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import { usePreferencesStore } from "../../../shared/store/preferencesStore";
import type { TilesSize } from "../types/FileListViewTypes";

export type FileBrowserViewMode =
  | "table"
  | "tiles-small"
  | "tiles-medium"
  | "tiles-large";

export const useFilesLayout = () => {
  const {
    layoutPreferences,
    setFilesLayoutType,
    setFilesTilesSize,
  } = usePreferencesStore();
  
  const initialLayoutType =
    layoutPreferences.filesLayoutType ?? InterfaceLayoutType.Tiles;

  const initialTilesSize: TilesSize =
    layoutPreferences.filesTilesSize ?? "medium";

  const [layoutType, setLayoutType] = useState<InterfaceLayoutType>(initialLayoutType);
  const [tilesSize, setTilesSize] = useState<TilesSize>(initialTilesSize);

  useEffect(() => {
    setFilesLayoutType(layoutType);
  }, [layoutType, setFilesLayoutType]);

  useEffect(() => {
    setFilesTilesSize(tilesSize);
  }, [tilesSize, setFilesTilesSize]);

  const viewMode: FileBrowserViewMode = useMemo(() => {
    if (layoutType === InterfaceLayoutType.List) return "table";
    if (tilesSize === "small") return "tiles-small";
    if (tilesSize === "large") return "tiles-large";
    return "tiles-medium";
  }, [layoutType, tilesSize]);

  const cycleViewMode = useCallback(() => {
    switch (viewMode) {
      case "table":
        setLayoutType(InterfaceLayoutType.Tiles);
        setTilesSize("small");
        return;
      case "tiles-small":
        setTilesSize("medium");
        return;
      case "tiles-medium":
        setTilesSize("large");
        return;
      case "tiles-large":
        setLayoutType(InterfaceLayoutType.List);
        return;
      default:
        setLayoutType(InterfaceLayoutType.List);
    }
  }, [viewMode]);

  return {
    layoutType,
    setLayoutType,
    tilesSize,
    setTilesSize,
    viewMode,
    cycleViewMode,
  };
};
