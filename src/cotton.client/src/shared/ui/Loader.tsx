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
        zIndex: 1300,
        backgroundColor: "background.default",
        transition: "background-color 1s ease",
        position: overlay ? "fixed" : "static",
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
        <Typography variant="h6" style={{ marginTop: 16 }}>
          {title}
        </Typography>
      )}
      {caption && (
        <Typography variant="caption" color="textSecondary">
          {caption}
        </Typography>
      )}
    </Box>
  );
};

export default Loader;
