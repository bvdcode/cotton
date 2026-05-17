import React from "react";
import { Box } from "@mui/material";
import { alpha } from "@mui/material/styles";

interface BlurredPreviewImageProps {
  previewUrl: string;
  alt: string;
  blurOpacity: number;
  cursor: React.CSSProperties["cursor"];
  shouldLightenBackdrop: boolean;
  invertInDark: boolean;
}

export const BlurredPreviewImage: React.FC<BlurredPreviewImageProps> = ({
  previewUrl,
  alt,
  blurOpacity,
  cursor,
  shouldLightenBackdrop,
  invertInDark,
}) => {
  const [imageFit, setImageFit] = React.useState<"contain" | "cover">(
    "contain",
  );

  const handleLoad = React.useCallback(
    (e: React.SyntheticEvent<HTMLImageElement>) => {
      const img = e.currentTarget;
      const nextFit =
        img.naturalWidth > img.naturalHeight ? "cover" : "contain";
      setImageFit((prev) => (prev === nextFit ? prev : nextFit));
    },
    [],
  );

  return (
    <Box
      sx={{
        width: "100%",
        height: "100%",
        position: "relative",
      }}
    >
      <Box
        component="img"
        src={previewUrl}
        alt=""
        aria-hidden
        draggable={false}
        sx={{
          position: "absolute",
          inset: 0,
          display: "block",
          width: "100%",
          height: "100%",
          objectFit: "cover",
          filter: "blur(24px)",
          transform: "scale(1.15)",
          opacity: blurOpacity,
        }}
      />
      <Box
        component="img"
        src={previewUrl}
        alt={alt}
        loading="lazy"
        decoding="async"
        draggable={false}
        onLoad={handleLoad}
        sx={(theme) => ({
          position: "relative",
          display: "block",
          width: "100%",
          height: "100%",
          objectFit: imageFit,
          cursor,
          ...(shouldLightenBackdrop && {
            backgroundColor: alpha(theme.palette.common.white, 0.75),
          }),
          ...(invertInDark &&
            theme.palette.mode === "dark" && {
              filter: "invert(1)",
            }),
        })}
      />
    </Box>
  );
};
