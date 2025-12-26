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
        shape="ellipse"
        sx={{
          top: "-10%",
          left: "-5%",
          background: (theme: { palette: { primary: { main: string } } }) =>
            `radial-gradient(ellipse, ${alpha(theme.palette.primary.main, 0.4)}, transparent 70%)`,
          animation: "floatA 25s ease-in-out infinite",
        }}
      />
      <Blob
        size={420}
        shape="circle"
        sx={{
          bottom: "-15%",
          right: "-10%",
          background: (theme: { palette: { secondary: { main: string } } }) =>
            `radial-gradient(circle, ${alpha(theme.palette.secondary.main, 0.35)}, transparent 65%)`,
          animation: "floatB 30s ease-in-out infinite",
        }}
      />
      <Blob
        size={300}
        shape="ellipse"
        sx={{
          top: "50%",
          right: "-8%",
          background: (theme: { palette: { primary: { main: string } } }) =>
            `radial-gradient(ellipse, ${alpha(theme.palette.primary.main, 0.28)}, transparent 68%)`,
          animation: "floatC 28s ease-in-out infinite",
        }}
      />
      <Blob
        size={340}
        shape="circle"
        sx={{
          top: "70%",
          left: "10%",
          background: (theme: { palette: { secondary: { main: string } } }) =>
            `radial-gradient(circle, ${alpha(theme.palette.secondary.main, 0.25)}, transparent 72%)`,
          animation: "floatD 32s ease-in-out infinite",
        }}
      />
    </Box>
  );
}

function Blob({ size, shape, sx }: { size: number; shape: "circle" | "ellipse"; sx: object }) {
  const isEllipse = shape === "ellipse";
  return (
    <Box
      sx={{
        position: "absolute",
        width: size,
        height: isEllipse ? size * 0.65 : size,
        borderRadius: isEllipse ? "50%" : "50%",
        filter: "blur(50px)",
        opacity: 0.75,
        ...sx,
      }}
    />
  );
}
