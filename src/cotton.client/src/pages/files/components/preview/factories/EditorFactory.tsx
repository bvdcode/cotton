/**
 * Editor Factory Component
 * 
 * Factory Pattern: Creates appropriate editor based on mode
 * Open/Closed Principle: Easy to add new editor types
 * Dependency Inversion: Depends on IEditorProps abstraction
 */

import type { IEditorProps } from '../editors/types';
import { EditorMode } from '../editors/types';
import { PlainTextEditor } from '../editors/PlainTextEditor';
import { MarkdownEditor } from '../editors/MarkdownEditor';
import { CodeEditor } from '../editors/CodeEditor';

interface EditorFactoryProps extends IEditorProps {
  mode: EditorMode;
}

/**
 * Factory component that renders the appropriate editor based on mode
 * 
 * Strategy Pattern: Selects and renders the correct editor strategy
 */
export const EditorFactory: React.FC<EditorFactoryProps> = ({
  mode,
  ...editorProps
}) => {
  switch (mode) {
    case EditorMode.Text:
      return <PlainTextEditor {...editorProps} />;
    
    case EditorMode.Markdown:
      return <MarkdownEditor {...editorProps} />;
    
    case EditorMode.Code:
      return <CodeEditor {...editorProps} />;
    
    default:
      // Fallback to text editor for unknown modes
      return <PlainTextEditor {...editorProps} />;
  }
};
