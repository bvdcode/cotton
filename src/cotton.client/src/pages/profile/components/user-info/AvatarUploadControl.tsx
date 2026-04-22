import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";
import { Avatar, Box, CircularProgress } from "@mui/material";
import type { ChangeEventHandler } from "react";
import { AVATAR_FILE_ACCEPT } from "./avatarUploadUtils";

interface AvatarUploadControlProps {
  alt: string;
  src?: string;
  initials: string;
  inputId: string;
  uploadLabel: string;
  isUploading: boolean;
  onFileSelected: ChangeEventHandler<HTMLInputElement>;
}

export const AvatarUploadControl = ({
  alt,
  src,
  initials,
  inputId,
  uploadLabel,
  isUploading,
  onFileSelected,
}: AvatarUploadControlProps) => {
  return (
    <Box
      sx={{
        position: "relative",
        width: { xs: 84, sm: 104 },
        height: { xs: 84, sm: 104 },
        borderRadius: "50%",
        overflow: "hidden",
        cursor: "pointer",
        "&:hover .avatar-upload-overlay": {
          transform: "translateY(0)",
        },
      }}
    >
      <Avatar
        alt={alt}
        src={src}
        sx={{
          width: "100%",
          height: "100%",
          bgcolor: "primary.main",
        }}
      >
        {!src && initials}
      </Avatar>

      {isUploading ? (
        <Box
          sx={{
            position: "absolute",
            inset: 0,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            color: "common.white",
            bgcolor: "rgba(0, 0, 0, 0.58)",
          }}
        >
          <CircularProgress size={28} sx={{ color: "common.white" }} />
        </Box>
      ) : (
        <Box
          className="avatar-upload-overlay"
          component="label"
          htmlFor={inputId}
          aria-label={uploadLabel}
          sx={{
            position: "absolute",
            bottom: 0,
            left: 0,
            right: 0,
            height: "50%",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            color: "common.white",
            bgcolor: "rgba(0, 0, 0, 0.58)",
            cursor: "pointer",
            transform: "translateY(100%)",
            transition: "transform 0.2s ease, background-color 0.2s ease",
            "&:hover": {
              bgcolor: "rgba(0, 0, 0, 0.72)",
            },
          }}
        >
          <PhotoCameraIcon fontSize="small" />
        </Box>
      )}

      <input
        id={inputId}
        type="file"
        accept={AVATAR_FILE_ACCEPT}
        hidden
        disabled={isUploading}
        onChange={onFileSelected}
      />
    </Box>
  );
};
