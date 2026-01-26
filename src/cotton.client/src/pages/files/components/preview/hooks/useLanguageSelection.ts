/**
 * Language Selection Hook
 * 
 * Single Responsibility: Manages programming language selection state
 * Encapsulates language detection and override logic
 */

import { useState, useCallback } from 'react';
import { usePreferencesStore } from '../../../../../shared/store/preferencesStore';

/**
 * Detect programming language from file extension
 * Same logic as in CodeEditor.tsx
 */
function detectLanguageFromFileName(fileName: string): string {
  const name = fileName.toLowerCase();
  const ext = name.split('.').pop() || '';
  
  // Special case: Dockerfile
  if (name === 'dockerfile' || name.startsWith('dockerfile.')) {
    return 'dockerfile';
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
    
    // Basic syntax colorization
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
    'md': 'markdown',
    'markdown': 'markdown',
    'diff': 'diff',
    'patch': 'diff',
    'java': 'java',
    'coffee': 'coffeescript',
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
    'go': 'go',
    'rs': 'rust',
    'swift': 'swift',
    'sh': 'shell',
    'bash': 'shell',
    'zsh': 'shell',
    'yaml': 'yaml',
    'yml': 'yaml',
    'sql': 'sql',
    'dockerfile': 'dockerfile',
    'bat': 'bat',
    'cmd': 'bat',
    'fs': 'fsharp',
    'fsi': 'fsharp',
  };

  return languageMap[ext] || 'plaintext';
}

interface UseLanguageSelectionOptions {
  fileName: string;
  fileId: string;
}

interface UseLanguageSelectionResult {
  language: string;
  setLanguage: (language: string) => void;
  resetLanguage: () => void;
}

/**
 * Hook for managing language selection with localStorage persistence
 * 
 * @param options - Configuration options
 * @returns Current language, setter, and reset function
 */
export function useLanguageSelection({
  fileName,
  fileId,
}: UseLanguageSelectionOptions): UseLanguageSelectionResult {
  const { editorPreferences, setLanguageOverride: persistLanguage, removeLanguageOverride } = usePreferencesStore();

  const [language, setLanguageState] = useState<string>(() => {
    const stored = editorPreferences.languageOverrides[fileId];
    
    if (stored) {
      return stored;
    }
    
    return detectLanguageFromFileName(fileName);
  });

  const setLanguage = useCallback((newLanguage: string) => {
    setLanguageState(newLanguage);
    persistLanguage(fileId, newLanguage);
  }, [fileId, persistLanguage]);

  const resetLanguage = useCallback(() => {
    const detected = detectLanguageFromFileName(fileName);
    setLanguageState(detected);
    removeLanguageOverride(fileId);
  }, [fileName, fileId, removeLanguageOverride]);

  return { language, setLanguage, resetLanguage };
}
