import { useEffect } from "react";
import { Box } from "@mui/material";
import { PhotoView } from "react-photo-view";
import { useImageLoader } from "./useImageLoader";

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
  const { getImageUrl, preloadImage, registerImage } = useImageLoader();
  const src = getImageUrl(nodeFileId, previewUrl);

  useEffect(() => {
    registerImage(nodeFileId);
  }, [nodeFileId, registerImage]);

  const handleClick = () => {
    preloadImage(nodeFileId);
  };

  return (
    <PhotoView src={src}>
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