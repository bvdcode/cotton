import { Box, Stack } from "@mui/material";
import { useAuth } from "../../features/auth";
import {
  UserInfoCard,
  AppearanceSettingsCard,
  TotpSettingsCard,
  SessionsCard,
  WebDavTokenCard,
  ShareLinkSettingsCard,
  EditProfileCard,
} from "./components";
import { ChangePasswordCard } from "./components/ChangePasswordCard";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";

export const SettingsPage = () => {
  const { user, setAuthenticated } = useAuth();
  const { t: tCommon } = useTranslation("common");
  const { t: tRoutes } = useTranslation("routes");

  useEffect(() => {
    document.title = `${tCommon("title")} - ${tRoutes("settings")}`;

    return () => {
      document.title = tCommon("title");
    };
  }, [tCommon, tRoutes]);

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
      pb={{
        xs: 2,
        md: 3,
      }}
      width="100%"
      display="flex"
      justifyContent="center"
    >
      <Stack spacing={{ xs: 2, sm: 3 }} width="100%" maxWidth={800}>
        <UserInfoCard user={user} onUserUpdate={handleUserUpdate} />
        <EditProfileCard user={user} onUserUpdate={handleUserUpdate} />
        <AppearanceSettingsCard />
        <TotpSettingsCard user={user} onUserUpdate={handleUserUpdate} />
        <SessionsCard />
        <ShareLinkSettingsCard />
        <ChangePasswordCard />
        <WebDavTokenCard />
      </Stack>
    </Box>
  );
};
