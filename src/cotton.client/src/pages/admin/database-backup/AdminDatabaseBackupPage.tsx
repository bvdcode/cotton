import {
  Alert,
  Button,
  CircularProgress,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "@shared/ui/notifications";
import {
  useDatabaseBackupHistoryQuery,
  useTriggerDatabaseBackupMutation,
} from "../../../shared/api/queries/admin";
import { getApiErrorMessage } from "../../../shared/api/httpClient";
import { AdminPageSurface } from "../components/AdminPageSurface";
import { AdminPageHeader } from "../components/AdminPageHeader";
import { DatabaseBackupHistory } from "./DatabaseBackupHistory";
import { DatabaseBackupAccess } from "./DatabaseBackupAccess";

export const AdminDatabaseBackupPage = () => {
  const { t } = useTranslation(["admin", "common"]);

  const backupQuery = useDatabaseBackupHistoryQuery();
  const backup = backupQuery.data?.[0] ?? null;
  const triggerBackupMutation = useTriggerDatabaseBackupMutation();

  const refreshLatestBackup = useCallback(async () => {
    await backupQuery.refetch();
  }, [backupQuery]);

  const handleTriggerBackup = useCallback(() => {
    triggerBackupMutation.mutate(undefined, {
      onSuccess: () => {
        toast.success(t("databaseBackup.state.triggerSuccess"), {
          toastId: "admin:database-backup:trigger:success",
        });
      },
    });
  }, [triggerBackupMutation, t]);

  const isLoading = backupQuery.isPending || backupQuery.isFetching;
  const loadErrorMessage = backupQuery.isError
    ? (getApiErrorMessage(backupQuery.error) ??
      t("databaseBackup.errors.loadFailed"))
    : null;
  const isTriggering = triggerBackupMutation.isPending;
  const triggerErrorMessage = triggerBackupMutation.isError
    ? getApiErrorMessage(triggerBackupMutation.error) ||
      t("databaseBackup.errors.triggerFailed")
    : null;

  return (
    <Stack spacing={2}>
      <AdminPageSurface>
        <Stack p={3} spacing={3}>
          <AdminPageHeader
            title={t("databaseBackup.title")}
            description={t("databaseBackup.description")}
            action={
              <Stack
                direction={{ xs: "column", sm: "row" }}
                spacing={1}
                useFlexGap
                sx={{ flexWrap: "wrap" }}
              >
                <Button
                  variant="outlined"
                  color="inherit"
                  onClick={() => void refreshLatestBackup()}
                  disabled={isLoading || isTriggering}
                >
                  {t("databaseBackup.actions.refresh")}
                </Button>
                <Button
                  variant="contained"
                  onClick={handleTriggerBackup}
                  disabled={isTriggering}
                >
                  {isTriggering ? (
                    <Stack direction="row" spacing={1} alignItems="center">
                      <CircularProgress
                        size={16}
                        color="inherit"
                        aria-label={t("databaseBackup.actions.triggering")}
                      />
                      <Typography variant="button">
                        {t("databaseBackup.actions.triggering")}
                      </Typography>
                    </Stack>
                  ) : (
                    t("databaseBackup.actions.trigger")
                  )}
                </Button>
              </Stack>
            }
          />

          {loadErrorMessage && (
            <Alert severity="error">{loadErrorMessage}</Alert>
          )}

          {triggerErrorMessage && (
            <Alert severity="error">{triggerErrorMessage}</Alert>
          )}

          <Stack minHeight={4}>
            <LinearProgress
              aria-label={t("databaseBackup.state.loading")}
              aria-hidden={!isLoading}
              sx={{
                opacity: isLoading ? 1 : 0,
                transition: "opacity 120ms ease",
              }}
            />
          </Stack>

          {!isLoading && !loadErrorMessage && backup === null && (
            <Alert severity="info">{t("databaseBackup.state.empty")}</Alert>
          )}

          <Alert
            severity="info"
            variant="outlined"
            sx={{ "& .MuiAlert-message": { overflowWrap: "anywhere" } }}
          >
            {t("databaseBackup.state.restoreIfEmptyHint")}
          </Alert>
          <DatabaseBackupHistory backups={backupQuery.data ?? []} />
        </Stack>
      </AdminPageSurface>
      <DatabaseBackupAccess />
    </Stack>
  );
};
