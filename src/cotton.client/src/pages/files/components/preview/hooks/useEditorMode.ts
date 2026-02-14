/**
 * Editor Mode Management Hook
 * 
 * Single Responsibility: Manages editor mode state and persistence
 * Encapsulates mode detection and storage logic
 */

import { useCallback, useMemo } from 'react';
import { EditorMode } from '../editors/types';
import {
  selectEditorModes,
  useUserPreferencesStore,
} from '../../../../../shared/store/userPreferencesStore';
import { previewConfig } from '../../../../../shared/config/previewConfig';

/**
 * File extensions supported by Monaco Editor
 * Based on official Monaco documentation
 */
const CODE_EXTENSIONS = {
  // Rich IntelliSense languages
  richIntelliSense: [
    'ts', 'tsx',        // TypeScript (but exclude .ts video files by size check)
    'js', 'jsx', 'mjs', 'cjs',  // JavaScript
    'json', 'jsonc',    // JSON
    'html', 'htm',      // HTML
    'css',              // CSS
    'less',             // LESS
    'scss', 'sass',     // SCSS/SASS
  ],
  
  // Basic syntax colorization languages
  basicSyntax: [
    'xml', 'svg',       // XML
    'php', 'phtml',     // PHP
    'cs', 'csx',        // C#
    'cpp', 'cc', 'cxx', 'c', 'h', 'hpp', // C/C++
    'razor', 'cshtml',  // Razor
    'md', 'markdown',   // Markdown (can also use Markdown mode)
    'diff', 'patch',    // Diff
    'java',             // Java
    'vb',               // VB
    'coffee',           // CoffeeScript
    'hbs', 'handlebars', // Handlebars
    'bat', 'cmd',       // Batch
    'pug', 'jade',      // Pug
    'fs', 'fsi', 'fsx', 'fsscript', // F#
    'lua',              // Lua
    'ps1', 'psm1', 'psd1', // PowerShell
    'py', 'pyw', 'pyi', // Python
    'rb', 'rbw',        // Ruby
    'r',                // R
    'm', 'mm',          // Objective-C
    
    // Additional common languages
    'go',               // Go
    'rs',               // Rust
    'swift',            // Swift
    'kt', 'kts',        // Kotlin
    'sh', 'bash', 'zsh', // Shell
    'yaml', 'yml',      // YAML
    'toml',             // TOML
    'ini', 'conf', 'cfg', // Config files
    'sql',              // SQL
    'dockerfile',       // Dockerfile
    'vue',              // Vue
    'svelte',           // Svelte
  ],
} as const;

/**
 * Get all supported code extensions as a flat array
 */
const ALL_CODE_EXTENSIONS = [
  ...CODE_EXTENSIONS.richIntelliSense,
  ...CODE_EXTENSIONS.basicSyntax,
] as readonly string[];

/**
 * Check if file extension indicates a code file
 */
function isCodeExtension(fileName: string): boolean {
  const ext = fileName.toLowerCase().split('.').pop() || '';
  return (ALL_CODE_EXTENSIONS as readonly string[]).includes(ext);
}

/**
 * Check if file is a Dockerfile (special case - no extension)
 */
function isDockerfile(fileName: string): boolean {
  const name = fileName.toLowerCase();
  return name === 'dockerfile' || 
         name.startsWith('dockerfile.') ||
         name === '.dockerignore';
}

/**
 * Detect initial editor mode based on content and filename
 * Strategy Pattern: Different detection strategies for different content types
 * 
 * @param content - File content
 * @param fileName - File name with extension
 * @param fileSize - File size in bytes (optional, for size-based detection)
 */
function detectInitialMode(content: string, fileName: string, fileSize?: number): EditorMode {
  if (fileSize && fileSize > previewConfig.MAX_CODE_FILE_SIZE) {
    return EditorMode.Text;
  }

  // Check for markdown (can be edited in either Markdown or Code mode).
  // Important: do this before generic code-extension detection, since `.md`
  // is included in code extensions for syntax highlighting.
  const lowerName = fileName.toLowerCase();
  if (lowerName.endsWith('.md') || lowerName.endsWith('.markdown')) {
    return EditorMode.Markdown;
  }
  
  // Check for Dockerfile (special case)
  if (isDockerfile(fileName)) {
    return EditorMode.Code;
  }
  
  // Check file extension for code files
  if (isCodeExtension(fileName)) {
    // Special case: .ts can be TypeScript or video transport stream
    // If file is small enough and has code-like content, treat as code
    const ext = fileName.toLowerCase().split('.').pop() || '';
    if (ext === 'ts') {
      // Check if content looks like code (has typical code patterns)
      const codePatterns = [
        /^import\s+/m,
        /^export\s+/m,
        /^function\s+/m,
        /^class\s+/m,
        /^interface\s+/m,
        /^type\s+/m,
        /^const\s+/m,
        /^let\s+/m,
        /^var\s+/m,
      ];
      
      if (codePatterns.some(pattern => pattern.test(content))) {
        return EditorMode.Code;
      }
      
      // If no code patterns and file is large, likely a video
      if (fileSize && fileSize > 100 * 1024) { // > 100 KB
        return EditorMode.Text;
      }
    }
    
    return EditorMode.Code;
  }
  
  // Check content for markdown patterns
  const markdownPatterns = [
    /^#{1,6}\s/m,           // Headers: # ## ###
    /^\*\*[^*]+\*\*/m,      // Bold: **text**
    /^\[.+\]\(.+\)/m,       // Links: [text](url)
    /^[-*+]\s/m,            // Unordered lists
    /^>\s/m,                // Blockquotes
    /^`{3}/m,               // Code blocks
  ];
  
  if (markdownPatterns.some(pattern => pattern.test(content))) {
    return EditorMode.Markdown;
  }
  
  // Default to plain text
  return EditorMode.Text;
}

interface UseEditorModeOptions {
  content: string;
  fileName: string;
  fileId: string;
  fileSize?: number; // File size in bytes for size-based detection
}

interface UseEditorModeResult {
  mode: EditorMode;
  setMode: (mode: EditorMode) => void;
}

/**
 * Hook for managing editor mode with localStorage persistence
 * 
 * @param options - Configuration options
 * @returns Current mode and setter function
 */
export function useEditorMode({
  content,
  fileName,
  fileId,
  fileSize,
}: UseEditorModeOptions): UseEditorModeResult {
  const editorModes = useUserPreferencesStore(selectEditorModes);
  const setEditorMode = useUserPreferencesStore((s) => s.setEditorMode);

  const mode = useMemo<EditorMode>(() => {
    const stored = editorModes[fileId];
    if (stored && Object.values(EditorMode).includes(stored as EditorMode)) {
      return stored as EditorMode;
    }
    return detectInitialMode(content, fileName, fileSize);
  }, [content, editorModes, fileId, fileName, fileSize]);

  const setMode = useCallback((newMode: EditorMode) => {
    setEditorMode(fileId, newMode);
  }, [fileId, setEditorMode]);

  return { mode, setMode };
}
