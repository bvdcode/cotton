import React from "react";
import {
  Button,
  IconButton,
  Stack,
  Paper,
  Tooltip,
  Typography,
  useMediaQuery,
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import SaveIcon from "@mui/icons-material/Save";
import CancelIcon from "@mui/icons-material/Cancel";
import { useTranslation } from "react-i18next";
import { useTheme as useMuiTheme } from "@mui/material/styles";
import { EditorModeSelector, LanguageSelector } from "../editors";
import { EditorMode } from "../editors/types";

interface TextPreviewToolbarProps {
  fileName: string;
  mode: EditorMode;
  setMode: (mode: EditorMode) => void;
  language: string;
  setLanguage: (language: string) => void;
  isEditing: boolean;
  setIsEditing: (editing: boolean) => void;
  hasChanges: boolean;
  saving: boolean;
  onSave: () => void;
  onCancel: () => void;
}

export const TextPreviewToolbar: React.FC<TextPreviewToolbarProps> = ({
  fileName,
  mode,
  setMode,
  language,
  setLanguage,
  isEditing,
  setIsEditing,
  hasChanges,
  saving,
  onSave,
  onCancel,
}) => {
  const { t } = useTranslation(["files", "common"]);
  const muiTheme = useMuiTheme();
  const isMobile = useMediaQuery(muiTheme.breakpoints.down("sm"));

  return (
    <Paper
      elevation={0}
      sx={{
        borderBottom: 1,
        borderColor: "divider",
        px: 2,
        py: 1.5,
        borderRadius: {
          xs: "0px 0px 0 0",
          sm: "10px 10px 0 0",
        },
      }}
    >
      <Stack direction="row" spacing={2} alignItems="center" sx={{ mr: 5 }}>
        <Typography variant="subtitle2" sx={{ flexGrow: 1 }}>
          {fileName}
        </Typography>

        <EditorModeSelector
          currentMode={mode}
          onModeChange={setMode}
          disabled={saving}
        />

        {mode === EditorMode.Code && (
          <LanguageSelector
            currentLanguage={language}
            onLanguageChange={setLanguage}
            disabled={saving}
          />
        )}

        {!isEditing &&
          (isMobile ? (
            <Tooltip title={t("preview.actions.edit", { ns: "files" })}>
              <span>
                <IconButton
                  size="small"
                  onClick={() => setIsEditing(true)}
                  aria-label={t("preview.actions.edit", { ns: "files" })}
                >
                  <EditIcon fontSize="small" />
                </IconButton>
              </span>
            </Tooltip>
          ) : (
            <Button
              size="small"
              startIcon={<EditIcon />}
              onClick={() => setIsEditing(true)}
            >
              {t("preview.actions.edit", { ns: "files" })}
            </Button>
          ))}
        {isEditing && (
          <>
            {isMobile ? (
              <>
                <Tooltip title={t("actions.cancel", { ns: "common" })}>
                  <span>
                    <IconButton
                      size="small"
                      onClick={onCancel}
                      disabled={saving}
                      aria-label={t("actions.cancel", { ns: "common" })}
                    >
                      <CancelIcon fontSize="small" />
                    </IconButton>
                  </span>
                </Tooltip>
                <Tooltip title={t("preview.actions.save", { ns: "files" })}>
                  <span>
                    <IconButton
                      size="small"
                      onClick={onSave}
                      disabled={!hasChanges || saving}
                      aria-label={t("preview.actions.save", { ns: "files" })}
                    >
                      <SaveIcon fontSize="small" />
                    </IconButton>
                  </span>
                </Tooltip>
              </>
            ) : (
              <>
                <Button
                  size="small"
                  startIcon={<CancelIcon />}
                  onClick={onCancel}
                  disabled={saving}
                >
                  {t("actions.cancel", { ns: "common" })}
                </Button>
                <Button
                  size="small"
                  variant="contained"
                  startIcon={<SaveIcon />}
                  onClick={onSave}
                  disabled={!hasChanges || saving}
                >
                  {saving
                    ? t("preview.actions.saving", { ns: "files" })
                    : t("preview.actions.save", { ns: "files" })}
                </Button>
              </>
            )}
          </>
        )}
      </Stack>
    </Paper>
  );
};
