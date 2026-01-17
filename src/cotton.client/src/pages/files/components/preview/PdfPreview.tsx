import { Box, CircularProgress, Typography } from "@mui/material";
import { useState } from "react";

interface PdfPreviewProps {
  fileUrl: string;
  fileName: string;
}

export const PdfPreview = ({ fileUrl, fileName }: PdfPreviewProps) => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const handleLoad = () => {
    setLoading(false);
  };

  const handleError = () => {
    setLoading(false);
    setError("Failed to load PDF");
  };

  return (
    <Box
      sx={{
        width: "100%",
        height: "100%",
        display: "flex",
        flexDirection: "column",
        position: "relative",
      }}
    >
      {loading && (
        <Box
          sx={{
            position: "absolute",
            top: "50%",
            left: "50%",
            transform: "translate(-50%, -50%)",
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            gap: 2,
          }}
        >
          <CircularProgress />
          <Typography variant="body2" color="text.secondary">
            Loading PDF...
          </Typography>
        </Box>
      )}
      {error && (
        <Box
          sx={{
            position: "absolute",
            top: "50%",
            left: "50%",
            transform: "translate(-50%, -50%)",
            textAlign: "center",
          }}
        >
          <Typography variant="body1" color="error">
            {error}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
            {fileName}
          </Typography>
        </Box>
      )}
      <Box
        component="iframe"
        src={fileUrl}
        title={fileName}
        onLoad={handleLoad}
        onError={handleError}
        sx={{
          width: "100%",
          height: "100%",
          border: "none",
          display: loading || error ? "none" : "block",
        }}
      />
    </Box>
  );
};
