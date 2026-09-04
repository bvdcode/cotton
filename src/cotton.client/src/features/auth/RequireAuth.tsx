import { useAuth } from "./useAuth";
import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { getSafeAuthReturnPath } from "../../shared/utils/authReturnPath";
import { Button, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { AuthActionShell } from "../../shared/ui/AuthActionShell";

type Props = {
  children: ReactNode;
};

export function RequireAuth({ children }: Props) {
  const { phase, restoreSession } = useAuth();
  const location = useLocation();
  const { t } = useTranslation("common");

  if (phase === "booting") {
    return null;
  }

  if (phase === "anonymous") {
    return (
      <Navigate
        to="/login"
        state={{ from: getSafeAuthReturnPath(location.pathname) }}
        replace
      />
    );
  }

  if (phase === "unavailable") {
    return (
      <AuthActionShell
        logoAlt="Cotton"
        maxWidth="xs"
        title={t("errors.serverUnavailableTitle")}
      >
        <Stack spacing={3} sx={{ mt: 2.5 }}>
          <Typography color="text.secondary">
            {t("errors.serverUnavailableDescription")}
          </Typography>
          <Button variant="contained" onClick={() => void restoreSession()}>
            {t("actions.retry")}
          </Button>
        </Stack>
      </AuthActionShell>
    );
  }

  return <>{children}</>;
}
