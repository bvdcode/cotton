/**
 * Search Result Item Component
 * 
 * Single Responsibility: Renders a single search result (node or file)
 * Liskov Substitution: Can render both nodes and files through unified interface
 */

import React from 'react';
import {
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Typography,
  Chip,
  Box,
} from '@mui/material';
import FolderIcon from '@mui/icons-material/Folder';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { NodeDto } from '../../../shared/api/layoutsApi';
import type { NodeFileManifestDto } from '../../../shared/api/nodesApi';
import { getFileIcon } from '../../files/utils/icons';
import { formatFileSize } from '../../../shared/utils/formatters';

export interface SearchResultItemProps {
  /** The result item - either a node or file */
  item: NodeDto | NodeFileManifestDto;
  /** Type of the result */
  type: 'node' | 'file';
  /** Callback when item is clicked */
  onClick?: (item: NodeDto | NodeFileManifestDto, type: 'node' | 'file') => void;
}

/**
 * Check if item is a File
 */
function isFile(item: NodeDto | NodeFileManifestDto): item is NodeFileManifestDto {
  return 'sizeBytes' in item;
}

/**
 * SearchResultItem component for displaying a single search result
 * 
 * Handles both folder (node) and file results with appropriate icons and metadata
 */
export const SearchResultItem: React.FC<SearchResultItemProps> = ({
  item,
  type,
  onClick,
}) => {
  const { t } = useTranslation(['search', 'files', 'common']);
  const navigate = useNavigate();

  const handleClick = () => {
    if (onClick) {
      onClick(item, type);
      return;
    }

    // Default navigation behavior
    if (type === 'node') {
      navigate(`/files/${item.id}`);
    } else if (type === 'file' && isFile(item)) {
      // For files, navigate to parent node and potentially trigger preview
      navigate(`/files/${item.ownerId}`);
    }
  };

  const renderIcon = () => {
    if (type === 'node') {
      return (
        <ListItemIcon>
          <FolderIcon color="primary" />
        </ListItemIcon>
      );
    }

    // File icon
    const file = item as NodeFileManifestDto;
    const iconResult = getFileIcon(
      file.encryptedFilePreviewHashHex || null,
      file.name,
    );

    if (typeof iconResult === 'string') {
      // Preview image URL
      return (
        <ListItemIcon>
          <Box
            component="img"
            src={iconResult}
            alt={file.name}
            sx={{
              width: 40,
              height: 40,
              objectFit: 'cover',
              borderRadius: 1,
            }}
          />
        </ListItemIcon>
      );
    }

    // React icon component
    return <ListItemIcon>{iconResult}</ListItemIcon>;
  };

  const renderMetadata = () => {
    if (type === 'file' && isFile(item)) {
      return (
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', mt: 0.5 }}>
          <Typography variant="caption" color="text.secondary">
            {formatFileSize(item.sizeBytes)}
          </Typography>
          {item.contentType && (
            <Chip
              label={item.contentType.split('/')[0]}
              size="small"
              variant="outlined"
              sx={{ height: 20, fontSize: '0.65rem' }}
            />
          )}
        </Box>
      );
    }

    return (
      <Typography variant="caption" color="text.secondary">
        {t('search:resultTypes.folder')}
      </Typography>
    );
  };

  return (
    <ListItem disablePadding>
      <ListItemButton onClick={handleClick}>
        {renderIcon()}
        <ListItemText
          primary={item.name}
          secondary={renderMetadata()}
          primaryTypographyProps={{
            sx: {
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            },
          }}
        />
      </ListItemButton>
    </ListItem>
  );
};
