import * as React from "react";
import {
  Box,
  CircularProgress,
  IconButton,
  Popover,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import {
  AutoFixHigh,
  ColorLens,
  FilterDrama,
  FormatColorReset,
  SwapVert,
  Texture,
  VerticalAlignBottom,
  WbSunny,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { PreviewModal, PdfPreview, ModelPreview } from "@shared/ui/preview";
import { useModelPreviewControls } from "@shared/ui/preview/hooks/useModelPreviewControls";
import {
  useDefaultModelColor,
  useModelPaletteColors,
} from "@shared/ui/preview/modelPalette";
import type { FileType } from "@shared/utils/fileTypes";
import { sharedFoldersApi } from "../../../shared/api/sharedFoldersApi";
import { previewConfig } from "../../../shared/config/previewConfig";
import { ReadOnlyTextViewer } from "./ReadOnlyTextViewer";

interface SharedFilePreviewModalProps {
  open: boolean;
  token: string;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
  contentType: string | null;
  onClose: () => void;
}

type SharedTextPreviewState = {
  key: string;
  loading: boolean;
  error: string | null;
  content: string | null;
};

type ModelControls = ReturnType<typeof useModelPreviewControls>;

const createIdleTextPreviewState = (key: string): SharedTextPreviewState => ({
  key,
  loading: false,
  error: null,
  content: null,
});

const createLoadingTextPreviewState = (key: string): SharedTextPreviewState => ({
  key,
  loading: true,
  error: null,
  content: null,
});

const buildTextPreviewKey = (args: {
  open: boolean;
  token: string;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
}): string => {
  if (!args.open || args.fileType !== "text" || !args.fileId || !args.fileName) {
    return "";
  }

  return [
    args.token,
    args.fileId,
    args.fileName,
    args.fileSizeBytes ?? "",
  ].join("\u0000");
};

const formatTextPreviewSize = (sizeBytes: number): string => {
  const sizeMB = sizeBytes / 1024 / 1024;
  return sizeMB >= 1
    ? Math.round(sizeMB) + " MB"
    : Math.round(sizeBytes / 1024) + " KB";
};

const useSharedTextPreview = ({
  fileId,
  fileName,
  fileSizeBytes,
  fileType,
  open,
  token,
}: {
  open: boolean;
  token: string;
  fileId: string | null;
  fileName: string | null;
  fileType: FileType | null;
  fileSizeBytes: number | null;
}): SharedTextPreviewState => {
  const { t } = useTranslation(["files"]);
  const textPreviewKey = React.useMemo(
    () =>
      buildTextPreviewKey({
        fileId,
        fileName,
        fileSizeBytes,
        fileType,
        open,
        token,
      }),
    [fileId, fileName, fileSizeBytes, fileType, open, token],
  );
  const sizeError = React.useMemo(() => {
    const sizeBytes = fileSizeBytes;
    if (
      !textPreviewKey ||
      typeof sizeBytes !== "number" ||
      sizeBytes <= previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES
    ) {
      return null;
    }

    return t("preview.errors.fileTooLarge", {
      ns: "files",
      size: formatTextPreviewSize(sizeBytes),
      maxSize: Math.round(previewConfig.MAX_TEXT_PREVIEW_SIZE_BYTES / 1024) + " KB",
    });
  }, [fileSizeBytes, t, textPreviewKey]);
  const [state, setState] = React.useState<SharedTextPreviewState>(() =>
    createIdleTextPreviewState(textPreviewKey),
  );

  React.useEffect(() => {
    if (!textPreviewKey || sizeError || !fileId) {
      return;
    }

    let cancelled = false;

    void (async () => {
      try {
        const inlineUrl = sharedFoldersApi.buildFileContentUrl(
          token,
          fileId,
          "inline",
        );
        const response = await fetch(inlineUrl);

        if (cancelled) return;

        if (!response.ok) {
          throw new Error(t("preview.errors.loadFailed", { ns: "files", error: "" }));
        }

        const content = await response.text();
        if (!cancelled) {
          setState({
            key: textPreviewKey,
            loading: false,
            error: null,
            content,
          });
        }
      } catch (error) {
        if (!cancelled) {
          setState({
            key: textPreviewKey,
            loading: false,
            error:
              error instanceof Error
                ? error.message
                : t("preview.errors.loadFailed", { ns: "files", error: "" }),
            content: null,
          });
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [fileId, token, sizeError, t, textPreviewKey]);

  if (!textPreviewKey) {
    return createIdleTextPreviewState(textPreviewKey);
  }
  if (sizeError) {
    return {
      ...createIdleTextPreviewState(textPreviewKey),
      error: sizeError,
    };
  }

  return state.key === textPreviewKey
    ? state
    : createLoadingTextPreviewState(textPreviewKey);
};

const SharedModelHeaderActions = ({
  controls,
}: {
  controls: ModelControls;
}): React.ReactElement => {
  const { t } = useTranslation(["files"]);

  return (
    <Stack direction="row" spacing={0.5} alignItems="center">
      <Tooltip
        title={t("preview.model.actions.cycleLighting", {
          ns: "files",
          preset: t("preview.model.lighting." + controls.lightingPreset, { ns: "files" }),
        })}
      >
        <IconButton onClick={controls.cycleLightingPreset}>
          <WbSunny />
        </IconButton>
      </Tooltip>
      <Tooltip
        title={t("preview.model.actions.toggleShadows", {
          ns: "files",
          state: t(
            controls.shadowsEnabled
              ? "preview.model.states.on"
              : "preview.model.states.off",
            { ns: "files" },
          ),
        })}
      >
        <IconButton
          color={controls.shadowsEnabled ? "primary" : "default"}
          onClick={controls.toggleShadowsEnabled}
        >
          <FilterDrama />
        </IconButton>
      </Tooltip>
      <Tooltip
        title={t("preview.model.actions.cycleSurface", {
          ns: "files",
          preset: t("preview.model.surface." + controls.surfacePreset, { ns: "files" }),
        })}
      >
        <IconButton onClick={controls.cycleSurfacePreset}>
          <Texture />
        </IconButton>
      </Tooltip>
      <Tooltip title={t("preview.model.actions.flipModel", { ns: "files" })}>
        <IconButton onClick={controls.requestFlip}>
          <SwapVert />
        </IconButton>
      </Tooltip>
      <Tooltip title={t("preview.model.actions.autoOrient", { ns: "files" })}>
        <IconButton onClick={controls.requestAutoOrient}>
          <AutoFixHigh />
        </IconButton>
      </Tooltip>
      <Tooltip title={t("preview.model.actions.autoAlign", { ns: "files" })}>
        <IconButton onClick={controls.requestAutoAlign}>
          <VerticalAlignBottom />
        </IconButton>
      </Tooltip>
      <Tooltip title={t("preview.model.actions.togglePalette", { ns: "files" })}>
        <IconButton
          onClick={(event) => {
            controls.togglePaletteAnchor(event.currentTarget);
          }}
        >
          <ColorLens />
        </IconButton>
      </Tooltip>
    </Stack>
  );
};

const SharedTextPreviewBody = ({
  contentType,
  fileName,
  preview,
}: {
  contentType: string | null;
  fileName: string;
  preview: SharedTextPreviewState;
}): React.ReactElement => {
  const { t } = useTranslation(["share"]);

  return (
    <Box
      sx={{
        height: "100%",
        minHeight: 0,
        minWidth: 0,
        display: "flex",
        flexDirection: "column",
      }}
    >
      {preview.loading && (
        <Box
          sx={{
            flex: 1,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            gap: 1,
          }}
        >
          <CircularProgress size={20} />
          <Typography color="text.secondary">
            {t("loading", { ns: "share" })}
          </Typography>
        </Box>
      )}
      {!preview.loading && preview.error && (
        <Box
          sx={{
            flex: 1,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            px: 2,
          }}
        >
          <Typography color="error">{preview.error}</Typography>
        </Box>
      )}
      {!preview.loading && !preview.error && preview.content !== null && (
        <ReadOnlyTextViewer
          title={fileName}
          fileName={fileName}
          contentType={contentType}
          textContent={preview.content}
        />
      )}
    </Box>
  );
};

const SharedModelPreviewBody = ({
  contentType,
  controls,
  fileId,
  fileName,
  fileSizeBytes,
  token,
}: {
  contentType: string | null;
  controls: ModelControls;
  fileId: string;
  fileName: string;
  fileSizeBytes: number | null;
  token: string;
}): React.ReactElement => (
  <Box sx={{ display: "flex", flex: 1, flexDirection: "column", minHeight: 0 }}>
    <Box sx={{ flex: 1, minHeight: 0 }}>
      <ModelPreview
        source={{
          kind: "url",
          url: sharedFoldersApi.buildFileContentUrl(token, fileId, "inline"),
        }}
        fileName={fileName}
        contentType={contentType}
        fileSizeBytes={fileSizeBytes}
        materialColor={controls.materialColor}
        autoAlignToken={controls.autoAlignToken}
        autoOrientToken={controls.autoOrientToken}
        flipToken={controls.flipToken}
        lightingPreset={controls.lightingPreset}
        shadowsEnabled={controls.shadowsEnabled}
        surfacePreset={controls.surfacePreset}
      />
    </Box>
  </Box>
);

const SharedModelPalette = ({
  controls,
}: {
  controls: ModelControls;
}): React.ReactElement => {
  const { t } = useTranslation(["files"]);
  const colors = useModelPaletteColors();

  return (
    <Popover
      open={Boolean(controls.paletteAnchorEl)}
      anchorEl={controls.paletteAnchorEl}
      onClose={controls.closePalette}
      anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
      transformOrigin={{ vertical: "top", horizontal: "right" }}
      sx={{ mt: 0.5 }}
    >
      <Box
        sx={{
          alignItems: "center",
          display: "flex",
          flexWrap: "wrap",
          gap: 0.5,
          maxWidth: 360,
          px: 1,
          py: 0.75,
        }}
      >
        <Tooltip title={t("preview.model.actions.resetColor", { ns: "files" })}>
          <IconButton
            size="small"
            sx={{ height: 30, width: 30 }}
            onClick={() => {
              controls.setMaterialColor(null);
              controls.closePalette();
            }}
          >
            <FormatColorReset fontSize="small" />
          </IconButton>
        </Tooltip>
        {colors.map((paletteOption) => (
          <IconButton
            key={paletteOption.id}
            onClick={() => {
              controls.setMaterialColor(paletteOption.color);
              controls.closePalette();
            }}
          >
            <Box
              sx={{
                backgroundColor: paletteOption.color,
                border: 1,
                borderColor:
                  controls.materialColor === paletteOption.color
                    ? "text.primary"
                    : "divider",
                borderRadius: "50%",
                height: 18,
                width: 18,
              }}
            />
          </IconButton>
        ))}
      </Box>
    </Popover>
  );
};

const isSupportedSharedPreviewType = (fileType: FileType | null): boolean =>
  fileType === "pdf" || fileType === "text" || fileType === "model";

export const SharedFilePreviewModal: React.FC<SharedFilePreviewModalProps> = ({
  open,
  token,
  fileId,
  fileName,
  fileType,
  fileSizeBytes,
  contentType,
  onClose,
}) => {
  const isModel = fileType === "model";
  const defaultModelColor = useDefaultModelColor();
  const textPreview = useSharedTextPreview({
    open,
    token,
    fileId,
    fileName,
    fileType,
    fileSizeBytes,
  });
  const modelControlsKey =
    open && isModel && fileId
      ? [fileId, defaultModelColor].join("\u0000")
      : "";
  const modelControls = useModelPreviewControls({
    stateKey: modelControlsKey,
    defaultMaterialColor: defaultModelColor,
  });

  if (!open || !fileId || !fileName || !isSupportedSharedPreviewType(fileType)) {
    return null;
  }

  const headerLayout = fileType === "pdf" || isModel;

  return (
    <PreviewModal
      open={open}
      onClose={onClose}
      layout={headerLayout ? "header" : "overlay"}
      title={headerLayout ? fileName : undefined}
      forceFullScreen={isModel}
      headerActions={
        isModel ? <SharedModelHeaderActions controls={modelControls} /> : undefined
      }
    >
      {fileType === "pdf" && (
        <PdfPreview
          source={{
            kind: "url",
            cacheKey: "shared:" + token + ":" + fileId,
            getPreviewUrl: async () =>
              sharedFoldersApi.buildFileContentUrl(token, fileId, "inline"),
          }}
          fileName={fileName}
          fileSizeBytes={fileSizeBytes}
        />
      )}
      {fileType === "text" && (
        <SharedTextPreviewBody
          contentType={contentType}
          fileName={fileName}
          preview={textPreview}
        />
      )}
      {isModel && (
        <>
          <SharedModelPreviewBody
            contentType={contentType}
            controls={modelControls}
            fileId={fileId}
            fileName={fileName}
            fileSizeBytes={fileSizeBytes}
            token={token}
          />
          <SharedModelPalette controls={modelControls} />
        </>
      )}
    </PreviewModal>
  );
};
