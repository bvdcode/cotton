import { useState } from "react";
import { Box } from "@mui/material";
import type { Guid } from "../../../../shared/api/layoutsApi";
import { EditorFactory } from "./factories/EditorFactory";
import { useEditorMode } from "./hooks/useEditorMode";
import { useLanguageSelection } from "./hooks/useLanguageSelection";
import { useTextFileContent } from "./hooks/useTextFileContent";
import { useTextFileSave } from "./hooks/useTextFileSave";
import {
  TextPreviewLoading,
  TextPreviewError,
} from "./components/TextPreviewStates";
import { TextPreviewToolbar } from "./components/TextPreviewToolbar";
import { EditorMode } from "./editors/types";

interface TextPreviewProps {
  nodeFileId: Guid;
  fileName: string;
  fileSizeBytes: number | null;
  onSaved?: () => void;
}

export function TextPreview({
  nodeFileId,
  fileName,
  fileSizeBytes,
  onSaved,
}: TextPreviewProps) {
  const [isEditing, setIsEditing] = useState(false);

  const {
    content,
    setContent,
    originalContent,
    setOriginalContent,
    loading,
    error: loadError,
    isFileTooLarge,
  } = useTextFileContent(nodeFileId, fileSizeBytes);

  const { saving, error: saveError, handleSave } = useTextFileSave(
    nodeFileId,
    fileName,
    originalContent,
    setOriginalContent,
    () => {
      setIsEditing(false);
      onSaved?.();
    },
  );

  const { mode, setMode } = useEditorMode({
    content: content || "",
    fileName,
    fileId: nodeFileId,
    fileSize: fileSizeBytes ?? undefined,
  });

  const { language, setLanguage } = useLanguageSelection({
    fileName,
    fileId: nodeFileId,
  });

  const handleSaveClick = () => {
    if (content) {
      void handleSave(content);
    }
  };

  const handleCancel = () => {
    setContent(originalContent);
    setIsEditing(false);
  };

  if (loading) {
    return <TextPreviewLoading loading={loading} />;
  }

  const error = loadError || saveError;
  if (error) {
    return (
      <TextPreviewError
        error={error}
        isFileTooLarge={isFileTooLarge}
        nodeFileId={nodeFileId}
        fileName={fileName}
      />
    );
  }

  const hasChanges = content !== originalContent;

  return (
    <Box sx={{ height: "100%", display: "flex", flexDirection: "column" }}>
      <TextPreviewToolbar
        fileName={fileName}
        mode={mode}
        setMode={setMode}
        language={language}
        setLanguage={setLanguage}
        isEditing={isEditing}
        setIsEditing={setIsEditing}
        hasChanges={hasChanges}
        saving={saving}
        onSave={handleSaveClick}
        onCancel={handleCancel}
      />

      <Box sx={{ flexGrow: 1, overflow: "auto" }}>
        <EditorFactory
          mode={mode}
          value={content}
          onChange={setContent}
          isEditing={isEditing}
          fileName={fileName}
          language={mode === EditorMode.Code ? language : undefined}
        />
      </Box>
    </Box>
  );
}
