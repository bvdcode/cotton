import React, { useEffect } from "react";
import { Box, Alert, CircularProgress } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useLayoutsStore } from "../../shared/store/layoutsStore";
import { SearchBar } from "./components/SearchBar";
import { SearchResults } from "./components/SearchResults";
import { useLayoutSearch } from "./hooks/useLayoutSearch";
import { downloadFile } from "../files/utils/fileHandlers";
import { useFilePreview } from "../files/hooks/useFilePreview";
import { FilePreviewModal } from "../files/components";

export const SearchPage: React.FC = () => {
  const navigate = useNavigate();
  const { rootNode, ensureHomeData } = useLayoutsStore();

  useEffect(() => {
    void ensureHomeData();
  }, [ensureHomeData]);

  const layoutId = rootNode?.layoutId;

  const searchState = useLayoutSearch({
    layoutId: layoutId ?? "",
    pageSize: 20,
  });

  const { previewState, openPreview, closePreview } = useFilePreview();

  const handleFolderClick = (nodeId: string) => {
    navigate(`/files/${nodeId}`);
  };

  const handleFileClick = async (file: { id: string; name: string }) => {
    const opened = openPreview(file.id, file.name);
    if (!opened) {
      await downloadFile(file.id, file.name);
    }
  };

  const handleSubmit = () => {
    if (!layoutId) return;
    void searchState.search();
  };

  const disabled = !layoutId || searchState.loading;

  return (
    <Box p={3} width="100%">
      <SearchBar
        value={searchState.query}
        onChange={searchState.setQuery}
        onSubmit={handleSubmit}
        disabled={disabled}
      />

      {searchState.error && (
        <Box mb={2}>
          <Alert severity="error">{searchState.error}</Alert>
        </Box>
      )}

      {searchState.loading && (
        <Box display="flex" justifyContent="center" mt={2}>
          <CircularProgress size={24} />
        </Box>
      )}

      <SearchResults
        results={searchState.results}
        totalCount={searchState.totalCount}
        onFolderClick={handleFolderClick}
        onFileClick={handleFileClick}
      />

      <FilePreviewModal
        isOpen={previewState.isOpen}
        fileId={previewState.fileId}
        fileName={previewState.fileName}
        fileType={previewState.fileType}
        onClose={closePreview}
      />
    </Box>
  );
};
