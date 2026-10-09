import { Alert, Button, Stack, TextField, Typography } from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { adminApi } from "../../../shared/api/adminApi";
import { getApiErrorMessage } from "../../../shared/api/httpClient";
import { AdminPageSurface } from "../components/AdminPageSurface";

export const DatabaseBackupAccess = () => {
  const { t } = useTranslation("admin");
  const [token, setToken] = useState<string>();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string>();
  const [copied, setCopied] = useState(false);
  const issueToken = async () => {
    setPending(true);
    setError(undefined);
    try {
      setToken(await adminApi.createDatabaseBackupToken());
    } catch (failure) {
      setError(
        getApiErrorMessage(failure) ?? t("databaseBackup.access.failed"),
      );
    } finally {
      setPending(false);
    }
  };
  const copyToken = async () => {
    if (!token) {
      return;
    }
    try {
      await navigator.clipboard.writeText(token);
      setCopied(true);
      setError(undefined);
    } catch {
      setError(t("databaseBackup.access.copyFailed"));
    }
  };
  return (
    <AdminPageSurface>
      <Stack p={3} spacing={2}>
        <Typography variant="h6">{t("databaseBackup.access.title")}</Typography>
        <Typography variant="body2" color="text.secondary">
          {t("databaseBackup.access.description")}
        </Typography>
        <Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>
          {t("databaseBackup.access.request", {
            endpoint: "POST /api/v1/server/database-backup",
            header: "X-Cotton-Backup-Token",
          })}
        </Typography>
        {error && <Alert severity="error">{error}</Alert>}
        {token ? (
          <Stack spacing={1}>
            <TextField
              label={t("databaseBackup.access.token")}
              value={token}
              fullWidth
              slotProps={{ input: { readOnly: true } }}
            />
            <Button color="inherit" onClick={() => void copyToken()}>
              {t(
                copied
                  ? "databaseBackup.access.copied"
                  : "databaseBackup.access.copy",
              )}
            </Button>
          </Stack>
        ) : (
          <Button
            variant="outlined"
            color="inherit"
            disabled={pending}
            onClick={() => void issueToken()}
          >
            {t("databaseBackup.access.generate")}
          </Button>
        )}
      </Stack>
    </AdminPageSurface>
  );
};
