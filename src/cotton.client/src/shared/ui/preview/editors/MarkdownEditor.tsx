/**
 * Markdown Editor Component
 *
 * Single Responsibility: Wraps MDEditor with consistent interface
 * Provides markdown editing and preview capabilities
 */

import MDEditor, { type MDEditorProps } from "@uiw/react-md-editor";
import { Box } from "@mui/material";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import rehypeRewrite from "rehype-rewrite";
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
const previewOptions: NonNullable<MDEditorProps["previewOptions"]> = {
  rehypePlugins: [sanitizePlugin, restoreMarkdownAnchors],
  pluginsFilter: (type, plugins) => {
    if (type === "remark") {
      return plugins;
    }
    const trustedPlugins = plugins.filter(
      (plugin) => Array.isArray(plugin) && plugin[0] === rehypeRewrite,
    );
    return [
      ...plugins.filter((plugin) => !trustedPlugins.includes(plugin)),
      ...trustedPlugins,
    ];
  },
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

  if (!isEditing) {
    return (
      <Box
        component={MDEditor.Markdown}
        source={value}
        {...previewOptions}
        sx={{ minHeight: "100%", p: 2.5 }}
      />
    );
  }

  return (
    <MDEditor
      value={value}
      onChange={(val) => onChange(val || "")}
      preview="edit"
      previewOptions={previewOptions}
      height="100%"
      visibleDragbar={false}
    />
  );
};
