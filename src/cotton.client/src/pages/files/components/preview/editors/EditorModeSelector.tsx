/**
 * Editor Mode Selector Component
 * 
 * Single Responsibility: Provides UI for switching between editor modes
 * Open/Closed: Easy to add new modes by extending configuration
 */

import { ToggleButtonGroup, ToggleButton, Tooltip } from "@mui/material";
import { TextFields, Code, Article } from "@mui/icons-material";
import { EditorMode, type IEditorModeConfig } from "./types";

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
    label: 'Text',
    icon: <TextFields fontSize="small" />,
    description: 'Plain text with preserved formatting',
  },
  {
    mode: EditorMode.Markdown,
    label: 'Markdown',
    icon: <Article fontSize="small" />,
    description: 'Markdown with preview',
  },
  {
    mode: EditorMode.Code,
    label: 'Code',
    icon: <Code fontSize="small" />,
    description: 'Code editor with syntax highlighting',
  },
];

export const EditorModeSelector: React.FC<EditorModeSelectorProps> = ({
  currentMode,
  onModeChange,
  disabled = false,
}) => {
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
          px: 1.5,
          py: 0.5,
        },
      }}
    >
      {EDITOR_MODES.map((config) => (
        <ToggleButton
          key={config.mode}
          value={config.mode}
          aria-label={config.label}
        >
          <Tooltip title={config.description || config.label}>
            <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
              {config.icon}
              <span>{config.label}</span>
            </span>
          </Tooltip>
        </ToggleButton>
      ))}
    </ToggleButtonGroup>
  );
};
