import { useState } from "react";
import {
  selectGalleryMetadataDetailsExpanded,
  selectGalleryMetadataOpen,
  useUserPreferencesStore,
} from "../../store/userPreferencesStore";

export const useGalleryMetadataState = () => {
  const savedPanelOpen = useUserPreferencesStore(selectGalleryMetadataOpen);
  const savedDetailsExpanded = useUserPreferencesStore(
    selectGalleryMetadataDetailsExpanded,
  );
  const savePanelOpen = useUserPreferencesStore(
    (s) => s.setGalleryMetadataOpen,
  );
  const saveDetailsExpanded = useUserPreferencesStore(
    (s) => s.setGalleryMetadataDetailsExpanded,
  );
  const preferencesLoaded = useUserPreferencesStore((s) => s.loaded);
  const [view, setView] = useState({
    loaded: preferencesLoaded,
    panelOpen: savedPanelOpen,
    detailsExpanded: savedDetailsExpanded,
  });
  if (view.loaded !== preferencesLoaded) {
    setView({
      loaded: preferencesLoaded,
      panelOpen: savedPanelOpen,
      detailsExpanded: savedDetailsExpanded,
    });
  }
  const setPanelOpen = (value: boolean) => {
    setView((previous) => ({ ...previous, panelOpen: value }));
    savePanelOpen(value);
  };
  const setDetailsExpanded = (value: boolean) => {
    setView((previous) => ({ ...previous, detailsExpanded: value }));
    saveDetailsExpanded(value);
  };
  return {
    panelOpen: view.panelOpen,
    detailsExpanded: view.detailsExpanded,
    setPanelOpen,
    setDetailsExpanded,
  };
};
