import { alpha } from "@mui/material/styles";
import { Box, CircularProgress, Typography } from "@mui/material";

interface LoaderProps {
  overlay?: boolean;
  title?: string;
  caption?: string;
}

const Loader: React.FC<LoaderProps> = ({ overlay, title, caption }) => {
  return (
    <Box
      display="flex"
      height="100%"
      width="100%"
      flexDirection="column"
      alignItems="center"
      justifyContent="center"
      sx={{
        top: 0,
        left: 0,
        opacity: 0,
        zIndex: (theme) => (overlay ? theme.zIndex.modal - 1 : "auto"),
        backgroundColor: (theme) =>
          overlay
            ? alpha(theme.palette.background.default, 0.85)
            : theme.palette.background.default,
        position: overlay ? "fixed" : "static",
        pointerEvents: overlay ? "none" : "auto",
        animation: "fadeIn 0.3s ease-in forwards",
        "@keyframes fadeIn": {
          from: {
            opacity: 0,
          },
          to: {
            opacity: 1,
          },
        },
      }}
    >
      <CircularProgress />
      {title && (
        <Typography variant="h6" sx={{ mt: 2 }}>
          {title}
        </Typography>
      )}
      {caption && (
        <Typography variant="caption" color="text.secondary">
          {caption}
        </Typography>
      )}
    </Box>
  );
};

export default Loader;
