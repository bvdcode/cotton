import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { adminApi } from "../../../shared/api/adminApi";
import { getApiErrorMessage } from "../../../shared/api/httpClient";
import { HelpButton } from "../../../shared/ui/HelpButton";

export const DatabaseBackupAccess = () => {
  const { t } = useTranslation(["admin", "common"]);
  const [token, setToken] = useState<string>();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string>();
  const [copied, setCopied] = useState<"token" | "command">();
  const issueToken = async () => {
    setPending(true);
    setError(undefined);
    setCopied(undefined);
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
  const copyText = async (text: string, target: "token" | "command") => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(target);
      setError(undefined);
    } catch {
      setError(t("databaseBackup.access.copyFailed"));
    }
  };
  const closeDialog = () => {
    setToken(undefined);
    setError(undefined);
    setCopied(undefined);
  };
  const command = [
    "curl --fail --request POST \\",
    `  --header 'X-Cotton-Backup-Token: ${token ?? ""}' \\`,
    `  '${new URL("/api/v1/server/database-backup", window.location.origin).href}'`,
  ].join("\n");
  return (
    <Stack spacing={2} alignItems="flex-start">
      <Stack direction="row" spacing={1} alignItems="center">
        <Typography variant="h6">{t("databaseBackup.access.title")}</Typography>
        <HelpButton
          title={t("databaseBackup.access.title")}
          content={
            <Stack spacing={2}>
              <Typography variant="body2">
                {t("databaseBackup.access.description")}
              </Typography>
              <Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>
                {t("databaseBackup.access.request", {
                  endpoint: "POST /api/v1/server/database-backup",
                  header: "X-Cotton-Backup-Token",
                })}
              </Typography>
            </Stack>
          }
        />
      </Stack>
      {error && !token && <Alert severity="error">{error}</Alert>}
      <Button
        variant="outlined"
        color="inherit"
        disabled={pending}
        onClick={() => void issueToken()}
      >
        {t("databaseBackup.access.generate")}
      </Button>
      <Dialog
        open={token !== undefined}
        onClose={closeDialog}
        fullWidth
        maxWidth="md"
        aria-labelledby="backup-token-title"
      >
        <DialogTitle id="backup-token-title">
          {t("databaseBackup.access.token")}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} pt={1}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              label={t("databaseBackup.access.token")}
              value={token ?? ""}
              fullWidth
              multiline
              maxRows={4}
              slotProps={{ input: { readOnly: true } }}
            />
            <Button
              color="inherit"
              onClick={() => void copyText(token ?? "", "token")}
            >
              {t(
                copied === "token"
                  ? "databaseBackup.access.copied"
                  : "databaseBackup.access.copy",
              )}
            </Button>
            <TextField
              label={t("databaseBackup.access.curlExample")}
              value={command}
              fullWidth
              multiline
              maxRows={8}
              slotProps={{
                input: { readOnly: true, sx: { fontFamily: "monospace" } },
              }}
            />
            <Button
              color="inherit"
              onClick={() => void copyText(command, "command")}
            >
              {t(
                copied === "command"
                  ? "databaseBackup.access.copied"
                  : "databaseBackup.access.copyCommand",
              )}
            </Button>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button color="inherit" onClick={closeDialog}>
            {t("common:actions.close")}
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};
