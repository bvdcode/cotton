/**
 * Editor Mode Selector Component
 * 
 * Single Responsibility: Provides UI for switching between editor modes
 * Open/Closed: Easy to add new modes by extending configuration
 */

import { Box, ToggleButtonGroup, ToggleButton, Tooltip, Typography, useMediaQuery } from "@mui/material";
import { TextFields, Code, Article } from "@mui/icons-material";
import { EditorMode, type IEditorModeConfig } from "./types";
import { useTheme as useMuiTheme } from "@mui/material/styles";
import { useTranslation } from "react-i18next";

interface EditorModeSelectorProps {
  currentMode: EditorMode;
  onModeChange: (mode: EditorMode) => void;
  disabled?: boolean;
}

/**
 * Editor mode configurations
 * Open/Closed Principle: Configuration-driven approach
 */
const EDITOR_MODES: IEditorModeConfig[] = [
  {
    mode: EditorMode.Text,
    label: "preview.editorModes.text.label",
    icon: <TextFields fontSize="small" />,
    description: "preview.editorModes.text.description",
  },
  {
    mode: EditorMode.Markdown,
    label: "preview.editorModes.markdown.label",
    icon: <Article fontSize="small" />,
    description: "preview.editorModes.markdown.description",
  },
  {
    mode: EditorMode.Code,
    label: "preview.editorModes.code.label",
    icon: <Code fontSize="small" />,
    description: "preview.editorModes.code.description",
  },
];

export const EditorModeSelector: React.FC<EditorModeSelectorProps> = ({
  currentMode,
  onModeChange,
  disabled = false,
}) => {
  const { t } = useTranslation(["files"]);
  const muiTheme = useMuiTheme();
  const isMobile = useMediaQuery(muiTheme.breakpoints.down("sm"));

  return (
    <ToggleButtonGroup
      value={currentMode}
      exclusive
      onChange={(_, newMode) => {
        if (newMode !== null) {
          onModeChange(newMode as EditorMode);
        }
      }}
      size="small"
      disabled={disabled}
      sx={{ 
        bgcolor: 'background.paper',
        '& .MuiToggleButton-root': {
          px: { xs: 1, sm: 1.5 },
          py: 0.5,
          minWidth: { xs: 36, sm: "auto" },
        },
      }}
    >
      {EDITOR_MODES.map((config) => (
        <ToggleButton
          key={config.mode}
          value={config.mode}
          aria-label={t(config.label, { ns: "files" })}
        >
          <Tooltip title={t(config.description || config.label, { ns: "files" })}>
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                gap: 0.5,
              }}
            >
              {config.icon}
              {!isMobile && (
                <Typography variant="caption" sx={{ lineHeight: 1 }}>
                  {t(config.label, { ns: "files" })}
                </Typography>
              )}
            </Box>
          </Tooltip>
        </ToggleButton>
      ))}
    </ToggleButtonGroup>
  );
};
