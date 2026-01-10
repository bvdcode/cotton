import { useEffect, useState } from "react";
import { Box, Typography } from "@mui/material";
import { PhotoView } from "react-photo-view";
import { filesApi } from "../../../shared/api/filesApi";

type ImagePreviewIconProps = {
  nodeFileId: string;
  fileName: string;
  previewUrl: string;
};

const LazyPhotoViewContent: React.FC<{ nodeFileId: string; fileName: string }> = ({ nodeFileId, fileName }) => {
  const [downloadUrl, setDownloadUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    (async () => {
      try {
        const url = await filesApi.getDownloadLink(nodeFileId, 60 * 24);
        if (cancelled) return;
        setDownloadUrl(url);
      } catch (error) {
        console.error("Failed to get download link:", error);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [nodeFileId]);

  if (loading || !downloadUrl) {
    return (
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          minHeight: "400px",
          color: "white",
        }}
      >
        <Typography>Loading...</Typography>
      </Box>
    );
  }

  return <img src={downloadUrl} alt={fileName} />;
};

export const ImagePreviewIcon: React.FC<ImagePreviewIconProps> = ({
  nodeFileId,
  fileName,
  previewUrl,
}) => {
  return (
    <PhotoView
      render={({ attrs }) => (
        <div {...attrs}>
          <LazyPhotoViewContent nodeFileId={nodeFileId} fileName={fileName} />
        </div>
      )}
    >
      <Box
        component="img"
        src={previewUrl}
        alt={fileName}
        loading="lazy"
        decoding="async"
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
