import { Box, Stack } from "@mui/material";
import { useAuth } from "../../features/auth";
import { UserInfoCard, TotpSettingsCard, SessionsCard } from "./components";

export const ProfilePage = () => {
  const { user, setAuthenticated } = useAuth();

  if (!user) {
    return null;
  }

  const handleUserUpdate = (updatedUser: typeof user) => {
    setAuthenticated(true, updatedUser);
  };

  return (
    <Box
      sx={{ p: { xs: 2, sm: 3 }, display: "flex", justifyContent: "center" }}
    >
      <Stack spacing={{ xs: 2, sm: 3 }} sx={{ width: "100%", maxWidth: 800 }}>
        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={{ xs: 2, sm: 3 }}
        >
          <Box sx={{ flex: 1 }}>
            <UserInfoCard user={user} />
          </Box>
          <Box sx={{ flex: 1 }}>
            <TotpSettingsCard user={user} onUserUpdate={handleUserUpdate} />
          </Box>
        </Stack>
        <SessionsCard />
      </Stack>
    </Box>
  );
};
