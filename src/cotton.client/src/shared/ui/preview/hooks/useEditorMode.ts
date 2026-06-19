/**
 * Editor Mode Management Hook
 * 
 * Single Responsibility: Manages editor mode state and persistence
 * Encapsulates mode detection and storage logic
 */

import { useCallback, useMemo } from 'react';
import { EditorMode } from '../editors/types';
import {
  useLocalPreferencesStore,
} from "../../../store/localPreferencesStore";
import { previewConfig } from "../../../config/previewConfig";
import { isCodePreviewFileName, isDockerfileName } from "../../../utils/codeFileTypes";

/**
 * Detect initial editor mode based on content and filename
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
  if (isDockerfileName(fileName)) {
    return EditorMode.Code;
  }
  
  // Check file extension for code files
  if (isCodePreviewFileName(fileName)) {
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
 * Hook for managing editor mode with persisted preferences
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
  const setEditorMode = useLocalPreferencesStore((s) => s.setEditorMode);

  const storedMode = useLocalPreferencesStore(
    useMemo(
      () =>
        (s) =>
          s.editorModes[fileId] ?? null,
      [fileId],
    ),
  );

  const mode = useMemo<EditorMode>(() => {
    if (
      storedMode &&
      Object.values(EditorMode).includes(storedMode as EditorMode)
    ) {
      return storedMode as EditorMode;
    }
    return detectInitialMode(content, fileName, fileSize);
  }, [content, fileName, fileSize, storedMode]);

  const setMode = useCallback((newMode: EditorMode) => {
    setEditorMode(fileId, newMode);
  }, [fileId, setEditorMode]);

  return { mode, setMode };
}
