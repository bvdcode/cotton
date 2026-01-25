/**
 * Markdown Editor Component
 * 
 * Single Responsibility: Wraps MDEditor with consistent interface
 * Provides markdown editing and preview capabilities
 */

import MDEditor from "@uiw/react-md-editor";
import type { IEditorProps } from "./types";

export const MarkdownEditor: React.FC<IEditorProps> = ({
  value,
  onChange,
  isEditing,
}) => {
  return (
    <MDEditor
      value={value}
      onChange={(val) => onChange(val || '')}
      preview={isEditing ? "edit" : "preview"}
      hideToolbar={!isEditing}
      height="100%"
      visibleDragbar={false}
    />
  );
};
