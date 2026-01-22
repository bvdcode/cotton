import { Box, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useAuth } from "../../features/auth";
import { UserInfoCard, TotpSettingsCard, SessionsCard } from "./components";

export const ProfilePage = () => {
  const { t } = useTranslation("profile");
  const { user, setAuthenticated } = useAuth();

  if (!user) {
    return (
      <Box sx={{ p: 2 }}>
        <Typography variant="h5" fontWeight={700}>
          {t("title")}
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          {t("notAuthenticated")}
        </Typography>
      </Box>
    );
  }

  const handleUserUpdate = (updatedUser: typeof user) => {
    setAuthenticated(true, updatedUser);
  };

  return (
    <Box sx={{ p: { xs: 2, sm: 3 } }}>
      <Stack spacing={{ xs: 2, sm: 3 }}>
        <Stack
          direction={{ xs: "column", lg: "row" }}
          spacing={{ xs: 2, sm: 3 }}
        >
          <UserInfoCard user={user} />
          <TotpSettingsCard user={user} onUserUpdate={handleUserUpdate} />
        </Stack>
        <SessionsCard />
      </Stack>
    </Box>
  );
};
