import { Box, alpha } from "@mui/material";

export function FloatingBlobs() {
  return (
    <Box
      aria-hidden
      sx={{
        position: "absolute",
        inset: 0,
        pointerEvents: "none",
        overflow: "hidden",
        zIndex: 0,
      }}
    >
      <Blob
        size={360}
        sx={{
          top: "12%",
          left: "14%",
          background: (theme: { palette: { primary: { main: string } } }) =>
            `radial-gradient(circle, ${alpha(theme.palette.primary.main, 0.4)}, transparent 60%)`,
          animation: "floatA 14s ease-in-out infinite",
        }}
      />
      <Blob
        size={420}
        sx={{
          bottom: "-4%",
          right: "-6%",
          background: (theme: { palette: { secondary: { main: string } } }) =>
            `radial-gradient(circle, ${alpha(theme.palette.secondary.main, 0.3)}, transparent 60%)`,
          animation: "floatB 18s ease-in-out infinite",
        }}
      />
      <Blob
        size={280}
        sx={{
          top: "40%",
          right: "20%",
          background: (theme: { palette: { primary: { main: string } } }) =>
            `radial-gradient(circle, ${alpha(theme.palette.primary.main, 0.25)}, transparent 65%)`,
          animation: "floatC 16s ease-in-out infinite",
        }}
      />
    </Box>
  );
}

function Blob({ size, sx }: { size: number; sx: object }) {
  return (
    <Box
      sx={{
        position: "absolute",
        width: size,
        height: size,
        filter: "blur(45px)",
        opacity: 0.8,
        ...sx,
      }}
    />
  );
}
