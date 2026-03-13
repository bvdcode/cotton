import { useCallback, useMemo } from "react";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import {
  selectFilesLayoutType,
  selectFilesTilesSize,
  useLocalPreferencesStore,
} from "../../../shared/store/localPreferencesStore";
import type { TilesSize } from "../types/FileListViewTypes";
import {
  cycleFileBrowserViewMode,
  getFileBrowserViewMode,
  type FileBrowserViewMode,
} from "../utils/viewMode";

export const useFilesLayout = () => {
  const storedLayoutType = useLocalPreferencesStore(selectFilesLayoutType);
  const layoutType = storedLayoutType ?? InterfaceLayoutType.Tiles;
  const tilesSize = useLocalPreferencesStore(selectFilesTilesSize) as TilesSize;

  const setLayoutType = useLocalPreferencesStore((s) => s.setFilesLayoutType);
  const setTilesSize = useLocalPreferencesStore((s) => s.setFilesTilesSize);

  const viewMode: FileBrowserViewMode = useMemo(() => {
    return getFileBrowserViewMode(layoutType, tilesSize);
  }, [layoutType, tilesSize]);

  const cycleViewMode = useCallback(() => {
    cycleFileBrowserViewMode(viewMode, setLayoutType, setTilesSize);
  }, [setLayoutType, setTilesSize, viewMode]);

  return {
    layoutType,
    setLayoutType,
    tilesSize,
    setTilesSize,
    viewMode,
    cycleViewMode,
  };
};
