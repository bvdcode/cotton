import { Box } from "@mui/material";

interface VideoPreviewProps {
  fileUrl: string;
  fileName: string;
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
