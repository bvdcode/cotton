import { Alert, Paper } from "@mui/material";
import type { User } from "../../../features/auth/types";
import { useUserInfoCard } from "./user-info/useUserInfoCard";
import { UserInfoHeader } from "./user-info/UserInfoHeader";

interface UserInfoCardProps {
  user: User;
  onUserUpdate: (updatedUser: User) => void;
}

export const UserInfoCard = ({ user, onUserUpdate }: UserInfoCardProps) => {
  const {
    avatarUploadInputId,
    avatarUploading,
    avatarStatus,
    emailVerificationSending,
    title,
    avatarInitials,
    birthDateValue,
    birthDateCompactValue,
    handleAvatarFileSelected,
    handleSendEmailVerification,
  } = useUserInfoCard({ user, onUserUpdate });

  return (
    <Paper
      sx={{
        display: "flex",
        flexDirection: "column",
        p: { xs: 2, sm: 3 },
      }}
    >
      {avatarStatus.kind === "error" && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {avatarStatus.message}
        </Alert>
      )}

      <UserInfoHeader
        title={title}
        username={user.username}
        pictureUrl={user.pictureUrl}
        avatarInitials={avatarInitials}
        avatarUploadInputId={avatarUploadInputId}
        avatarUploadInProgress={avatarUploading}
        role={user.role}
        isTotpEnabled={user.isTotpEnabled}
        email={user.email}
        isEmailVerified={user.isEmailVerified}
        birthDateValue={birthDateValue}
        birthDateCompactValue={birthDateCompactValue}
        onAvatarFileSelected={handleAvatarFileSelected}
        onSendEmailVerification={handleSendEmailVerification}
        emailVerificationSending={emailVerificationSending}
      />
    </Paper>
  );
};
