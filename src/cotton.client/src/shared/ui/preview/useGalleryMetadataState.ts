import { useState } from "react";
import {
  selectGalleryMetadataDetailsExpanded,
  selectGalleryMetadataOpen,
  useUserPreferencesStore,
} from "../../store/userPreferencesStore";

export const useGalleryMetadataState = (
  galleryOpen: boolean,
  isTouchDevice: boolean,
) => {
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
  const [mobileView, setMobileView] = useState({
    galleryOpen,
    isTouchDevice,
    panelOpen: false,
  });
  if (
    mobileView.galleryOpen !== galleryOpen ||
    mobileView.isTouchDevice !== isTouchDevice
  ) {
    setMobileView({ galleryOpen, isTouchDevice, panelOpen: false });
  }
  if (view.loaded !== preferencesLoaded) {
    setView({
      loaded: preferencesLoaded,
      panelOpen: savedPanelOpen,
      detailsExpanded: savedDetailsExpanded,
    });
  }
  const setPanelOpen = (value: boolean) => {
    if (isTouchDevice) {
      setMobileView((previous) => ({ ...previous, panelOpen: value }));
      return;
    }
    setView((previous) => ({ ...previous, panelOpen: value }));
    savePanelOpen(value);
  };
  const setDetailsExpanded = (value: boolean) => {
    setView((previous) => ({ ...previous, detailsExpanded: value }));
    saveDetailsExpanded(value);
  };
  return {
    panelOpen: isTouchDevice ? mobileView.panelOpen : view.panelOpen,
    detailsExpanded: view.detailsExpanded,
    setPanelOpen,
    setDetailsExpanded,
  };
};
