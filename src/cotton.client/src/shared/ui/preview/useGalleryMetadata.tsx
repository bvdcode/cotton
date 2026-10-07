import React from "react";
import {
  Box,
  Dialog,
  Drawer,
  IconButton,
  Portal,
  Stack,
  ThemeProvider,
  Typography,
} from "@mui/material";
import { Close, InfoOutlined } from "@mui/icons-material";
import { IconButton as LightboxIconButton } from "yet-another-react-lightbox";
import { useTranslation } from "react-i18next";
import type { MediaItem } from "../../types/mediaLightbox";
import type { GalleryMetadataPosition } from "../../types/galleryMetadataPosition";
import {
  selectGalleryMetadataPosition,
  useUserPreferencesStore,
} from "../../store/userPreferencesStore";
import { darkTheme } from "../../theme";
import { formatBytes } from "../../utils/formatBytes";
import { getGalleryMetadataEntries } from "./galleryMetadata";

const getMetadataAnchor = (position: GalleryMetadataPosition) => {
  switch (position) {
    case "left":
      return "left";
    case "right":
      return "right";
    case "hidden":
      return null;
  }
};

export const useGalleryMetadata = (
  item: MediaItem | undefined,
  open: boolean,
  isTouchDevice: boolean,
) => {
  const { t, i18n } = useTranslation(["files", "common"]);
  const position = useUserPreferencesStore(selectGalleryMetadataPosition);
  const anchor = getMetadataAnchor(position);
  const panelId = React.useId();
  const buttonRef = React.useRef<HTMLButtonElement>(null);
  const containerRef = React.useRef<HTMLDivElement>(null);
  const [interaction, setInteraction] = React.useState({
    open,
    position,
    pinned: false,
    hovered: false,
  });
  if (interaction.open !== open || interaction.position !== position) {
    setInteraction({ open, position, pinned: false, hovered: false });
  }
  const visible =
    open && anchor !== null && (interaction.pinned || interaction.hovered);
  const entries = React.useMemo(
    () =>
      visible
        ? getGalleryMetadataEntries(item?.metadata, t, i18n.language)
        : [],
    [visible, item?.metadata, t, i18n.language],
  );
  const closePanel = () => {
    setInteraction({ open, position, pinned: false, hovered: false });
    buttonRef.current?.focus();
  };

  if (!open || !item || anchor === null) {
    return { button: null, controls: null, visible: false };
  }

  const button = (
    <LightboxIconButton
      key="metadata"
      ref={buttonRef}
      className="media-lightbox__info-button"
      label="Metadata"
      aria-label={t("preview.metadata.label")}
      icon={InfoOutlined}
      aria-expanded={visible}
      aria-controls={panelId}
      onClick={() =>
        setInteraction((previous) => ({
          ...previous,
          pinned: !previous.pinned,
          hovered: false,
        }))
      }
    />
  );
  const panelProps = {
    id: panelId,
    "aria-label": t("preview.metadata.label"),
    className: "media-lightbox__metadata-panel",
    onPointerDown: (event: React.PointerEvent<HTMLDivElement>) =>
      event.stopPropagation(),
    onPointerUp: (event: React.PointerEvent<HTMLDivElement>) =>
      event.stopPropagation(),
    onClick: (event: React.MouseEvent<HTMLDivElement>) =>
      event.stopPropagation(),
    onWheel: (event: React.WheelEvent<HTMLDivElement>) =>
      event.stopPropagation(),
    onKeyDown: (event: React.KeyboardEvent<HTMLDivElement>) => {
      event.stopPropagation();
      if (event.key === "Escape") {
        closePanel();
      }
    },
  };
  const content = (
    <Stack spacing={2} padding={2}>
      <Stack direction="row" alignItems="center" justifyContent="space-between">
        <Typography variant="subtitle1" component="h2" id={`${panelId}-title`}>
          {t("preview.metadata.label")}
        </Typography>
        <IconButton
          aria-label={t("actions.close", { ns: "common" })}
          onClick={closePanel}
        >
          <Close />
        </IconButton>
      </Stack>
      <Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>
        {item.name}
      </Typography>
      {item.sizeBytes !== undefined && (
        <Typography variant="body2" color="text.secondary">
          {formatBytes(item.sizeBytes)}
        </Typography>
      )}
      <Box component="dl" margin={0}>
        {entries.map((entry) => (
          <Box key={entry.key} display="flex" gap={2} paddingY={0.5}>
            <Typography
              component="dt"
              variant="body2"
              color="text.secondary"
              sx={{ flex: 1, minWidth: 0, overflowWrap: "anywhere" }}
            >
              {entry.label}
            </Typography>
            <Typography
              component="dd"
              variant="body2"
              sx={{ flex: 1, minWidth: 0, m: 0, overflowWrap: "anywhere" }}
            >
              {entry.value}
            </Typography>
          </Box>
        ))}
      </Box>
    </Stack>
  );
  if (isTouchDevice) {
    return {
      button,
      visible,
      controls: (
        <Dialog
          open={visible}
          onClose={closePanel}
          fullWidth
          maxWidth="sm"
          aria-labelledby={`${panelId}-title`}
          container={() =>
            buttonRef.current?.closest<HTMLElement>(".yarl__portal") ?? null
          }
          slotProps={{ paper: panelProps }}
        >
          {content}
        </Dialog>
      ),
    };
  }
  const controls = (
    <>
      <Box ref={containerRef} />
      <Portal
        container={() =>
          containerRef.current?.closest<HTMLElement>(".yarl__portal") ?? null
        }
      >
        <ThemeProvider theme={darkTheme}>
          <Box
            className={`media-lightbox__metadata-edge media-lightbox__metadata-edge--${anchor}`}
            onPointerLeave={() =>
              setInteraction((previous) => ({ ...previous, hovered: false }))
            }
          >
            <Box
              className="media-lightbox__metadata-trigger"
              aria-hidden
              onPointerEnter={(event) => {
                if (event.pointerType === "mouse") {
                  setInteraction((previous) => ({
                    ...previous,
                    hovered: true,
                  }));
                }
              }}
            />
            <Drawer
              anchor={anchor}
              variant="persistent"
              open={visible}
              slotProps={{
                paper: {
                  ...panelProps,
                  role: "region",
                  "aria-hidden": !visible,
                  sx: {
                    position: "absolute",
                    width: "100%",
                    pointerEvents: visible ? "auto" : "none",
                    touchAction: "pan-y",
                    userSelect: "text",
                  },
                },
              }}
            >
              {content}
            </Drawer>
          </Box>
        </ThemeProvider>
      </Portal>
    </>
  );
  return { button: null, controls, visible };
};
