import { Box, Stack } from "@mui/material";
import { useAuth } from "../../features/auth";
import {
  UserInfoCard,
  TotpSettingsCard,
  SessionsCard,
  WebDavTokenCard,
  ShareLinkSettingsCard,
} from "./components";
import { ChangePasswordCard } from "./components/ChangePasswordCard";
import { useEffect } from "react";

export const ProfilePage = () => {
  const { user, setAuthenticated } = useAuth();

  useEffect(() => {
    document.title = "Cotton - Profile";

    return () => {
      document.title = "Cotton";
    };
  }, []);

  if (!user) {
    return null;
  }

  const handleUserUpdate = (updatedUser: typeof user) => {
    setAuthenticated(true, updatedUser);
  };

  return (
    <Box
      pt={{
        xs: 1,
        md: 3,
      }}
      width="100%"
      display="flex"
      justifyContent="center"
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
        <ShareLinkSettingsCard />
        <ChangePasswordCard />
        <WebDavTokenCard />
      </Stack>
    </Box>
  );
};
