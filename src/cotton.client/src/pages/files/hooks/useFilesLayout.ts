import { useCallback, useMemo } from "react";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import {
  selectFilesLayoutType,
  selectFilesTilesSize,
  useLocalPreferencesStore,
} from "../../../shared/store/localPreferencesStore";
import type { TilesSize } from "../types/FileListViewTypes";

export type FileBrowserViewMode =
  | "table"
  | "tiles-small"
  | "tiles-medium"
  | "tiles-large";

export const useFilesLayout = () => {
  const storedLayoutType = useLocalPreferencesStore(selectFilesLayoutType);
  const layoutType = storedLayoutType ?? InterfaceLayoutType.Tiles;
  const tilesSize = useLocalPreferencesStore(selectFilesTilesSize) as TilesSize;

  const setLayoutType = useLocalPreferencesStore((s) => s.setFilesLayoutType);
  const setTilesSize = useLocalPreferencesStore((s) => s.setFilesTilesSize);

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
