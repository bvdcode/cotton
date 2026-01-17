import { Box } from "@mui/material";

interface VideoPreviewProps {
  fileUrl: string;
  fileName: string;
}

interface PhotoRenderParams {
  attrs?: React.HTMLAttributes<HTMLDivElement>;
  scale?: number;
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

// Render function for PhotoView custom render
export const renderVideoPreview = (
  fileUrl: string,
  fileName: string,
  videoWidth = 800,
  videoHeight = 600,
) => {
  return ({ attrs }: PhotoRenderParams) => {
    return (
      <div {...attrs}>
        <Box
          component="video"
          controls
          autoPlay
          loop
          sx={{
            width: videoWidth,
            height: videoHeight,
            maxWidth: "90vw",
            maxHeight: "80vh",
            outline: "none",
            backgroundColor: "#000",
          }}
        >
          <source src={fileUrl} type="video/mp4" />
          <source src={fileUrl} type="video/webm" />
          {fileName}
        </Box>
      </div>
    );
  };
};
