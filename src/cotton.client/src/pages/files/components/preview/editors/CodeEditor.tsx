/**
 * Code Editor Component
 * 
 * Single Responsibility: Provides syntax-highlighted code editing
 * Uses Monaco Editor with automatic language detection
 */

import { Editor } from "@monaco-editor/react";
import { Box, CircularProgress } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import type { IEditorProps } from "./types";
import { detectMonacoLanguageFromFileName } from "../../../../../shared/utils/languageDetection";

/**
 * Detect programming language from file extension
 * Open/Closed Principle: Easy to extend with new mappings
 * Based on Monaco Editor supported languages
 */
function detectLanguage(fileName: string): string {
  return detectMonacoLanguageFromFileName(fileName);
}

export const CodeEditor: React.FC<IEditorProps> = ({
  value,
  onChange,
  isEditing,
  fileName,
  language: languageOverride,
}) => {
  const theme = useTheme();
  const language = languageOverride || detectLanguage(fileName);
  const monacoTheme = theme.palette.mode === 'dark' ? 'vs-dark' : 'vs-light';

  return (
    <Box sx={{ height: "100%", width: "100%" }}>
      <Editor
        height="100%"
        language={language}
        value={value || ''}
        onChange={(val) => onChange(val || '')}
        theme={monacoTheme}
        options={{
          readOnly: !isEditing,
          minimap: { enabled: true },
          fontSize: 14,
          lineNumbers: 'on',
          scrollBeyondLastLine: false,
          automaticLayout: true,
          wordWrap: 'on',
          folding: true,
          renderWhitespace: 'selection',
          tabSize: 2,
        }}
        loading={
          <Box
            display="flex"
            justifyContent="center"
            alignItems="center"
            height="100%"
          >
            <CircularProgress />
          </Box>
        }
      />
    </Box>
  );
};
