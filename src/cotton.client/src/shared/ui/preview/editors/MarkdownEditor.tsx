/**
 * Markdown Editor Component
 *
 * Single Responsibility: Wraps MDEditor with consistent interface
 * Provides markdown editing and preview capabilities
 */

import MDEditor from "@uiw/react-md-editor";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import rehypeSanitize from "rehype-sanitize";
import { toast } from "../../notifications/toast";
import { containsMarkdownIframe } from "./containsMarkdownIframe";
import {
  markdownSanitizationSchema,
  restoreMarkdownAnchors,
} from "./markdownAnchors";
import type { IEditorProps } from "./types";

const sanitizePlugin: [
  typeof rehypeSanitize,
  typeof markdownSanitizationSchema,
] = [rehypeSanitize, markdownSanitizationSchema];
const previewOptions = {
  rehypePlugins: [sanitizePlugin, restoreMarkdownAnchors],
};

export const MarkdownEditor: React.FC<IEditorProps> = ({
  value,
  onChange,
  isEditing,
}) => {
  const { t } = useTranslation("files");
  const warned = useRef(false);

  useEffect(() => {
    if (
      isEditing ||
      warned.current ||
      !value ||
      !containsMarkdownIframe(value)
    ) {
      return;
    }
    warned.current = true;
    toast.warning(t("preview.markdown.blockedIframe"));
  }, [isEditing, t, value]);

  return (
    <MDEditor
      value={value}
      onChange={(val) => onChange(val || "")}
      preview={isEditing ? "edit" : "preview"}
      previewOptions={previewOptions}
      hideToolbar={!isEditing}
      height="100%"
      visibleDragbar={false}
    />
  );
};
