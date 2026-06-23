/**
 * Renders the appropriate editor component for the given editor mode.
 */

import { lazy, Suspense } from "react";
import { Box, CircularProgress } from "@mui/material";
import type { IEditorProps } from "../editors/types";
import { EditorMode } from "../editors/types";
import { PlainTextEditor } from "../editors/PlainTextEditor";

const MarkdownEditor = lazy(() =>
  import("../editors/MarkdownEditor").then((module) => ({
    default: module.MarkdownEditor,
  })),
);

const CodeEditor = lazy(() =>
  import("../editors/CodeEditor").then((module) => ({
    default: module.CodeEditor,
  })),
);

const EditorFallback: React.FC = () => (
  <Box
    alignItems="center"
    display="flex"
    height="100%"
    justifyContent="center"
    minHeight={120}
  >
    <CircularProgress size={20} />
  </Box>
);

interface EditorFactoryProps extends IEditorProps {
  mode: EditorMode;
  language?: string; // Language override for Code editor
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
      return (
        <Suspense fallback={<EditorFallback />}>
          <MarkdownEditor {...editorProps} />
        </Suspense>
      );

    case EditorMode.Code:
      return (
        <Suspense fallback={<EditorFallback />}>
          <CodeEditor {...editorProps} />
        </Suspense>
      );

    default:
      // Fallback to text editor for unknown modes
      return <PlainTextEditor {...editorProps} />;
  }
};
