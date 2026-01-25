import { Box, Divider, List, ListItemButton, ListItemText, Typography } from "@mui/material";
import type React from "react";
import type { LayoutSearchResultDto } from "../../../shared/api/layoutsApi";
import type { NodeFileManifestDto } from "../../../shared/api/nodesApi";

export interface SearchResultsProps {
  results: LayoutSearchResultDto | null;
  totalCount: number;
  onFolderClick: (nodeId: string) => void;
  onFileClick: (file: NodeFileManifestDto) => void;
}

export const SearchResults: React.FC<SearchResultsProps> = ({
  results,
  totalCount,
  onFolderClick,
  onFileClick,
}) => {
  if (!results) {
    return null;
  }

  const { nodes, files } = results;

  const hasNodes = nodes.length > 0;
  const hasFiles = files.length > 0;

  return (
    <Box sx={{ mt: 1 }}>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
        Found {totalCount} items
      </Typography>

      {hasNodes && (
        <Box sx={{ mb: 2 }}>
          <Typography variant="subtitle2" sx={{ mb: 0.5 }}>
            Folders
          </Typography>
          <List dense>
            {nodes.map((node) => (
              <ListItemButton key={node.id} onClick={() => onFolderClick(node.id)}>
                <ListItemText primary={node.name} />
              </ListItemButton>
            ))}
          </List>
        </Box>
      )}

      {hasNodes && hasFiles && <Divider sx={{ my: 1 }} />}

      {hasFiles && (
        <Box>
          <Typography variant="subtitle2" sx={{ mb: 0.5 }}>
            Files
          </Typography>
          <List dense>
            {files.map((file) => (
              <ListItemButton key={file.id} onClick={() => onFileClick(file)}>
                <ListItemText primary={file.name} />
              </ListItemButton>
            ))}
          </List>
        </Box>
      )}

      {!hasNodes && !hasFiles && (
        <Typography color="text.secondary">No results</Typography>
      )}
    </Box>
  );
};
