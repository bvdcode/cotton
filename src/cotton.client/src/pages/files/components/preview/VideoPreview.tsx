import { Box, CircularProgress } from "@mui/material";

interface VideoPreviewProps {
  fileUrl: string;
  fileName: string;
}

interface PhotoRenderParams {
  attrs: React.HTMLAttributes<HTMLDivElement> & { style?: React.CSSProperties };
  scale: number;
}

export const VideoPreview: React.FC<VideoPreviewProps> = ({
  fileUrl,
  fileName,
}) => {
  return (
    <Box
      component="video"
      controls
      autoPlay
      sx={{
        maxWidth: "100%",
        maxHeight: "80vh",
        outline: "none",
      }}
    >
      <source src={fileUrl} />
      {fileName}
    </Box>
  );
};

// Video dimensions for gallery
export const VIDEO_WIDTH = 960;
export const VIDEO_HEIGHT = 540;

// Render function for PhotoView custom render
export const renderVideoPreview = (
  getUrl: () => string | null,
  fileName: string,
) => {
  return ({ attrs, scale }: PhotoRenderParams) => {
    const fileUrl = getUrl();
    
    // Calculate actual width from attrs style
    const width = attrs.style?.width
      ? parseFloat(attrs.style.width as string)
      : VIDEO_WIDTH;
    const offset = (width - VIDEO_WIDTH) / VIDEO_WIDTH;
    const childScale = scale === 1 ? scale + offset : 1 + offset;

    return (
      <div {...attrs}>
        <div
          style={{
            width: VIDEO_WIDTH,
            height: VIDEO_HEIGHT,
            transformOrigin: "0 0",
            transform: `scale(${childScale})`,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            backgroundColor: "#000",
          }}
        >
          {fileUrl ? (
            <video
              controls
              autoPlay
              style={{
                width: "100%",
                height: "100%",
                outline: "none",
              }}
              onMouseDown={(e) => e.stopPropagation()}
            >
              <source src={fileUrl} type="video/mp4" />
              <source src={fileUrl} type="video/webm" />
              <source src={fileUrl} type="video/ogg" />
              {fileName}
            </video>
          ) : (
            <CircularProgress sx={{ color: "white" }} />
          )}
        </div>
      </div>
    );
  };
};
