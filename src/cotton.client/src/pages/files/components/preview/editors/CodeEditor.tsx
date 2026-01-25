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

/**
 * Detect programming language from file extension
 * Open/Closed Principle: Easy to extend with new mappings
 * Based on Monaco Editor supported languages
 */
function detectLanguage(fileName: string): string {
  const name = fileName.toLowerCase();
  const ext = name.split('.').pop() || '';
  
  // Special case: Dockerfile (no extension)
  if (name === 'dockerfile' || name.startsWith('dockerfile.')) {
    return 'dockerfile';
  }
  
  if (name === '.dockerignore') {
    return 'ignore';
  }
  
  const languageMap: Record<string, string> = {
    // Rich IntelliSense languages
    'ts': 'typescript',
    'tsx': 'typescript',
    'js': 'javascript',
    'jsx': 'javascript',
    'mjs': 'javascript',
    'cjs': 'javascript',
    'json': 'json',
    'jsonc': 'json',
    'html': 'html',
    'htm': 'html',
    'css': 'css',
    'less': 'less',
    'scss': 'scss',
    'sass': 'scss',
    
    // Basic syntax colorization languages
    'xml': 'xml',
    'svg': 'xml',
    'php': 'php',
    'phtml': 'php',
    'cs': 'csharp',
    'csx': 'csharp',
    'cpp': 'cpp',
    'cc': 'cpp',
    'cxx': 'cpp',
    'c': 'c',
    'h': 'c',
    'hpp': 'cpp',
    'razor': 'razor',
    'cshtml': 'razor',
    'md': 'markdown',
    'markdown': 'markdown',
    'diff': 'diff',
    'patch': 'diff',
    'java': 'java',
    'vb': 'vb',
    'coffee': 'coffeescript',
    'hbs': 'handlebars',
    'handlebars': 'handlebars',
    'bat': 'bat',
    'cmd': 'bat',
    'pug': 'pug',
    'jade': 'pug',
    'fs': 'fsharp',
    'fsi': 'fsharp',
    'fsx': 'fsharp',
    'fsscript': 'fsharp',
    'lua': 'lua',
    'ps1': 'powershell',
    'psm1': 'powershell',
    'psd1': 'powershell',
    'py': 'python',
    'pyw': 'python',
    'pyi': 'python',
    'rb': 'ruby',
    'rbw': 'ruby',
    'r': 'r',
    'm': 'objective-c',
    'mm': 'objective-c',
    
    // Additional common languages (may have varying Monaco support)
    'go': 'go',
    'rs': 'rust',
    'swift': 'swift',
    'kt': 'kotlin',
    'kts': 'kotlin',
    'sh': 'shell',
    'bash': 'shell',
    'zsh': 'shell',
    'yaml': 'yaml',
    'yml': 'yaml',
    'toml': 'toml',
    'ini': 'ini',
    'conf': 'ini',
    'cfg': 'ini',
    'sql': 'sql',
    'dockerfile': 'dockerfile',
    'vue': 'html', // Vue uses HTML-like syntax
    'svelte': 'html', // Svelte uses HTML-like syntax
  };

  return languageMap[ext] || 'plaintext';
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
