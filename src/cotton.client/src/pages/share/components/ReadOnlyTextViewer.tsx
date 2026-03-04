import * as React from "react";
import { Box } from "@mui/material";
import { CodeEditor } from "../../files/components/preview/editors/CodeEditor";
import { MarkdownEditor } from "../../files/components/preview/editors/MarkdownEditor";
import { PlainTextEditor } from "../../files/components/preview/editors/PlainTextEditor";
import { detectMonacoLanguageFromFileName } from "../../../shared/utils/languageDetection";

function detectMonacoLanguageFromContentType(
  contentType: string | null,
): string | null {
  if (!contentType) return null;
  const normalized = contentType.toLowerCase();

  if (normalized.includes("json")) return "json";
  if (normalized.includes("xml")) return "xml";
  if (normalized.includes("yaml") || normalized.includes("yml")) return "yaml";

  return null;
}

export interface ReadOnlyTextViewerProps {
  title: string;
  fileName: string | null;
  contentType: string | null;
  textContent: string;
}

export const ReadOnlyTextViewer: React.FC<ReadOnlyTextViewerProps> = ({
  title,
  fileName,
  contentType,
  textContent,
}) => {
  const handleReadOnlyChange = React.useCallback((_nextValue: string) => {
    void _nextValue;
  }, []);

  const resolvedFileName = fileName ?? title;
  const lowerName = resolvedFileName.toLowerCase();
  const isMarkdown = lowerName.endsWith(".md") || lowerName.endsWith(".markdown");

  if (isMarkdown) {
    return (
      <Box width="100%" height="100%" minHeight={0} minWidth={0}>
        <MarkdownEditor
          value={textContent}
          onChange={handleReadOnlyChange}
          isEditing={false}
          fileName={resolvedFileName}
        />
      </Box>
    );
  }

  const languageOverride =
    fileName === null ? detectMonacoLanguageFromContentType(contentType) : null;
  const detectedLanguage = detectMonacoLanguageFromFileName(resolvedFileName);
  const monacoLanguage = languageOverride ?? detectedLanguage;
  const shouldUseCodeEditor = monacoLanguage !== "plaintext";

  return (
    <Box width="100%" height="100%" minHeight={0} minWidth={0}>
      {shouldUseCodeEditor ? (
        <CodeEditor
          value={textContent}
          onChange={handleReadOnlyChange}
          isEditing={false}
          fileName={resolvedFileName}
          language={languageOverride ?? undefined}
        />
      ) : (
        <PlainTextEditor
          value={textContent}
          onChange={handleReadOnlyChange}
          isEditing={false}
          fileName={resolvedFileName}
        />
      )}
    </Box>
  );
};
