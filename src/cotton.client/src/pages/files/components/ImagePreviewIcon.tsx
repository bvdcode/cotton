import { useEffect, useState, useCallback } from "react";
import { Box } from "@mui/material";
import { PhotoView } from "react-photo-view";
import { filesApi } from "../../../shared/api/filesApi";

type ImagePreviewIconProps = {
  nodeFileId: string;
  fileName: string;
  previewUrl: string;
};

export const ImagePreviewIcon: React.FC<ImagePreviewIconProps> = ({
  nodeFileId,
  fileName,
  previewUrl,
}) => {
  const [fullSrc, setFullSrc] = useState<string | null>(null);
  const [isViewerOpen, setIsViewerOpen] = useState(false);

  useEffect(() => {
    if (!isViewerOpen) return;
    
    let cancelled = false;

    (async () => {
      try {
        const url = await filesApi.getDownloadLink(nodeFileId, 60 * 24);
        if (cancelled) return;
        setFullSrc(url);
      } catch (error) {
        console.error("Failed to get download link:", error);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [isViewerOpen, nodeFileId]);

  const handleClick = useCallback(() => {
    setIsViewerOpen(true);
  }, []);

  return (
    <PhotoView src={fullSrc ?? previewUrl}>
      <Box
        component="img"
        src={previewUrl}
        alt={fileName}
        loading="lazy"
        decoding="async"
        onClick={handleClick}
        sx={{
          width: "100%",
          height: "100%",
          objectFit: "cover",
          cursor: "pointer",
        }}
      />
    </PhotoView>
  );
};