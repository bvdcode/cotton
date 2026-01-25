/**
 * Editor Types and Interfaces
 * 
 * Following SOLID principles:
 * - Single Responsibility: Each type represents one concept
 * - Open/Closed: Easy to extend with new editor modes
 * - Interface Segregation: Minimal, focused interfaces
 */

/**
 * Available editor modes for text content
 */
export const EditorMode = {
  /** Plain text with preserved whitespace */
  Text: 'text',
  /** Markdown with preview and editing */
  Markdown: 'markdown',
  /** Code editor with syntax highlighting */
  Code: 'code',
} as const;

export type EditorMode = typeof EditorMode[keyof typeof EditorMode];

/**
 * Common interface for all editor components
 * Dependency Inversion: Components depend on abstraction, not concrete implementations
 */
export interface IEditorProps {
  /** Current content value */
  value: string | undefined;
  /** Content change handler */
  onChange: (value: string) => void;
  /** Whether the editor is in edit mode */
  isEditing: boolean;
  /** File name for context (e.g., syntax detection) */
  fileName: string;
}

/**
 * Editor mode configuration
 */
export interface IEditorModeConfig {
  mode: EditorMode;
  label: string;
  icon: React.ReactNode;
  description?: string;
}
