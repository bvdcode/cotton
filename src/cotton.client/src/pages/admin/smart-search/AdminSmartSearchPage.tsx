import ManageSearchIcon from "@mui/icons-material/ManageSearch";
import RefreshIcon from "@mui/icons-material/Refresh";
import {
  Alert,
  Button,
  Chip,
  Divider,
  Skeleton,
  Stack,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { getApiErrorMessage } from "@shared/api/httpClient";
import {
  useEnableVectorExtensionMutation,
  useVectorExtensionStatusQuery,
} from "@shared/api/queries/admin";
import { AdminPageHeader } from "../components/AdminPageHeader";
import { AdminPageSurface } from "../components/AdminPageSurface";

export const AdminSmartSearchPage = () => {
  const { t, i18n } = useTranslation("admin");
  const statusQuery = useVectorExtensionStatusQuery();
  const enableMutation = useEnableVectorExtensionMutation();
  const status = statusQuery.data;

  return (
    <AdminPageSurface>
      <Stack p={3} spacing={3} divider={<Divider flexItem />}>
        <AdminPageHeader
          title={t("smartSearch.title")}
          description={t("smartSearch.description")}
          icon={<ManageSearchIcon color="primary" />}
          action={
            <Button
              variant="outlined"
              color="inherit"
              startIcon={<RefreshIcon />}
              onClick={() => void statusQuery.refetch()}
              disabled={statusQuery.isFetching || enableMutation.isPending}
            >
              {t("smartSearch.actions.refresh")}
            </Button>
          }
        />

        {statusQuery.isPending && (
          <Stack
            spacing={2}
            aria-label={t("smartSearch.loading")}
            role="status"
          >
            <Skeleton variant="rounded" height={72} />
            <Skeleton variant="rounded" height={72} />
          </Stack>
        )}

        {statusQuery.isError && (
          <Alert severity="error">
            {getApiErrorMessage(statusQuery.error) ??
              t("smartSearch.errors.loadFailed")}
          </Alert>
        )}

        {status && (
          <Stack spacing={3}>
            <Stack
              direction={{ xs: "column", sm: "row" }}
              justifyContent="space-between"
              alignItems={{ xs: "flex-start", sm: "center" }}
              spacing={2}
            >
              <Stack spacing={1} alignItems="flex-start">
                <Typography component="h2" variant="subtitle1" fontWeight={600}>
                  {t("smartSearch.extension")}
                </Typography>
                <Chip
                  size="small"
                  color={status.extensionEnabled ? "success" : "default"}
                  label={t(
                    status.extensionEnabled
                      ? "smartSearch.enabled"
                      : "smartSearch.disabled",
                  )}
                />
              </Stack>
              {!status.extensionEnabled && (
                <Button
                  variant="contained"
                  loading={enableMutation.isPending}
                  disabled={statusQuery.isFetching || statusQuery.isError}
                  onClick={() => enableMutation.mutate()}
                >
                  {t("smartSearch.actions.enable")}
                </Button>
              )}
            </Stack>

            {enableMutation.isError && (
              <Alert severity="error">
                {getApiErrorMessage(enableMutation.error) ??
                  t("smartSearch.errors.enableFailed")}
              </Alert>
            )}

            <Stack spacing={0.5}>
              <Typography component="h2" variant="subtitle1" fontWeight={600}>
                {t("smartSearch.vectorCount")}
              </Typography>
              <Typography variant="h4">
                {new Intl.NumberFormat(i18n.language).format(
                  status.vectorCount,
                )}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {t("smartSearch.vectorCountDescription")}
              </Typography>
            </Stack>

            <Alert severity="info">{t("smartSearch.setupNote")}</Alert>
          </Stack>
        )}
      </Stack>
    </AdminPageSurface>
  );
};
