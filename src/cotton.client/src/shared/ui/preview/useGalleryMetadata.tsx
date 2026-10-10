import React from "react";
import {
  alpha,
  Box,
  Button,
  Collapse,
  Dialog,
  Divider,
  Drawer,
  IconButton,
  Portal,
  Stack,
  ThemeProvider,
  Typography,
} from "@mui/material";
import {
  Close,
  ExpandLess,
  ExpandMore,
  InfoOutlined,
} from "@mui/icons-material";
import { IconButton as LightboxIconButton } from "yet-another-react-lightbox";
import { useTranslation } from "react-i18next";
import type { MediaItem } from "../../types/mediaLightbox";
import {
  selectGalleryMetadataPosition,
  useUserPreferencesStore,
} from "../../store/userPreferencesStore";
import { darkTheme } from "../../theme";
import { formatBytes } from "../../utils/formatBytes";
import {
  getGalleryMetadataSections,
  type GalleryMetadataEntry,
} from "./galleryMetadata";
import { useGalleryMetadataState } from "./useGalleryMetadataState";

export const useGalleryMetadata = (
  item: MediaItem | undefined,
  open: boolean,
  isTouchDevice: boolean,
) => {
  const { t, i18n } = useTranslation(["files", "common"]);
  const position = useUserPreferencesStore(selectGalleryMetadataPosition);
  const { panelOpen, detailsExpanded, setPanelOpen, setDetailsExpanded } =
    useGalleryMetadataState();
  const panelId = React.useId();
  const buttonRef = React.useRef<HTMLButtonElement>(null);
  const containerRef = React.useRef<HTMLDivElement>(null);
  const visible = open && panelOpen;
  const entries = React.useMemo(
    () =>
      visible
        ? getGalleryMetadataSections(item?.metadata, t, i18n.language)
        : { summary: [], details: [] },
    [visible, item?.metadata, t, i18n.language],
  );
  const closePanel = () => {
    setPanelOpen(false);
    buttonRef.current?.focus();
  };
  const handleEscape = (event: React.KeyboardEvent<HTMLButtonElement>) => {
    if (visible && event.key === "Escape") {
      event.stopPropagation();
      closePanel();
    }
  };

  if (!open || !item) {
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
      onClick={() => setPanelOpen(!panelOpen)}
      onKeyDown={handleEscape}
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
  const renderEntries = (values: GalleryMetadataEntry[]) => (
    <Box component="dl" margin={0}>
      {values.map((entry) => (
        <Box key={entry.key} display="flex" gap={2} paddingY={0.5}>
          <Typography
            component="dt"
            variant="body2"
            color="text.secondary"
            sx={{
              flex: { xs: 1, sm: 2 },
              minWidth: 0,
              overflowWrap: "anywhere",
            }}
          >
            {entry.label}
          </Typography>
          <Typography
            component="dd"
            variant="body2"
            sx={{
              flex: { xs: 1, sm: 3 },
              minWidth: 0,
              margin: 0,
              overflowWrap: "anywhere",
            }}
          >
            {entry.value}
          </Typography>
        </Box>
      ))}
    </Box>
  );
  const content = (
    <Stack
      spacing={2}
      padding={2}
      flex={1}
      minHeight={0}
      sx={
        !isTouchDevice
          ? (theme) => ({
              backgroundColor: alpha(theme.palette.background.paper, 0.9),
              transition: theme.transitions.create("background-color"),
              "&:hover": {
                backgroundColor: alpha(theme.palette.background.paper, 0.98),
              },
            })
          : undefined
      }
    >
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
      <Stack spacing={2} flex={1} minHeight={0} overflow="auto" tabIndex={0}>
        <Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>
          {item.name}
        </Typography>
        {item.sizeBytes !== undefined && (
          <Typography variant="body2" color="text.secondary">
            {formatBytes(item.sizeBytes)}
          </Typography>
        )}
        {renderEntries(entries.summary)}
        {entries.details.length > 0 && (
          <Collapse
            in={detailsExpanded}
            unmountOnExit
            id={`${panelId}-details`}
          >
            <Stack spacing={2}>
              <Divider />
              {renderEntries(entries.details)}
            </Stack>
          </Collapse>
        )}
      </Stack>
      {entries.details.length > 0 && (
        <Button
          color="inherit"
          startIcon={detailsExpanded ? <ExpandLess /> : <ExpandMore />}
          aria-expanded={detailsExpanded}
          aria-controls={`${panelId}-details`}
          onClick={() => setDetailsExpanded(!detailsExpanded)}
        >
          {t(
            detailsExpanded
              ? "preview.metadata.lessDetails"
              : "preview.metadata.moreDetails",
          )}
        </Button>
      )}
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
          slotProps={{
            paper: { ...panelProps, sx: { backgroundImage: "none" } },
          }}
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
            className={`media-lightbox__metadata-container media-lightbox__metadata-container--${position}`}
          >
            <Drawer
              anchor={position}
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
                    backgroundImage: "none",
                    backgroundColor: "transparent",
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
  return { button, controls, visible };
};
