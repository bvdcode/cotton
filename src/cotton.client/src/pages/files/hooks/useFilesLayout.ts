import { useEffect, useState } from "react";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import { usePreferencesStore } from "../../../shared/store/preferencesStore";

export const useFilesLayout = () => {
  const { layoutPreferences, setFilesLayoutType } = usePreferencesStore();
  
  const initialLayoutType =
    layoutPreferences.filesLayoutType ?? InterfaceLayoutType.Tiles;

  const [layoutType, setLayoutType] = useState<InterfaceLayoutType>(initialLayoutType);

  useEffect(() => {
    setFilesLayoutType(layoutType);
  }, [layoutType, setFilesLayoutType]);

  return {
    layoutType,
    setLayoutType,
  };
};
