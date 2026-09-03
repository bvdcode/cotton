import { LinearProgress, Stack, Typography } from "@mui/material";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import type { LayoutStatsDto } from "../../../shared/api/layoutsApi";
import { queryKeys } from "../../../shared/api/queries/queryKeys";
import { storageQuotaApi } from "../../../shared/api/storageQuotaApi";
import { formatBytes } from "../../../shared/utils/formatBytes";

interface DashboardOverviewWidgetProps {
  stats: LayoutStatsDto | undefined;
}

export const DashboardOverviewWidget = ({
  stats,
}: DashboardOverviewWidgetProps) => {
  const { t } = useTranslation(["home", "common"]);
  const quotaQuery = useQuery({
    queryKey: queryKeys.storageQuota.current(),
    queryFn: storageQuotaApi.getCurrent,
  });
  const quota = quotaQuery.data;
  const quotaPercent = quota?.quotaBytes
    ? Math.min(100, (quota.usedBytes / quota.quotaBytes) * 100)
    : null;

  return (
    <Stack gap={1.25}>
      <Stack direction="row" alignItems="baseline" gap={1} flexWrap="wrap">
        <Typography variant="h4">
          {stats ? formatBytes(stats.sizeBytes) : t("common:placeholder")}
        </Typography>
        {quota?.quotaBytes && (
          <Typography variant="body2" color="text.secondary">
            {t("dashboard.overview.ofQuota", {
              quota: formatBytes(quota.quotaBytes),
            })}
          </Typography>
        )}
      </Stack>
      {quotaPercent !== null && (
        <LinearProgress
          variant="determinate"
          value={quotaPercent}
          aria-label={t("dashboard.overview.storageUsage")}
        />
      )}
      <Stack direction="row" gap={{ xs: 2, sm: 4 }} flexWrap="wrap">
        <Stack>
          <Typography variant="subtitle1">
            {stats?.fileCount.toLocaleString() ?? t("common:placeholder")}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {t("cards.files.title")}
          </Typography>
        </Stack>
        <Stack>
          <Typography variant="subtitle1">
            {stats?.nodeCount.toLocaleString() ?? t("common:placeholder")}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {t("cards.folders.title")}
          </Typography>
        </Stack>
        {quota?.availableBytes !== null && quota?.availableBytes !== undefined && (
          <Stack>
            <Typography variant="subtitle1">
              {formatBytes(quota.availableBytes)}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {t("dashboard.overview.available")}
            </Typography>
          </Stack>
        )}
      </Stack>
    </Stack>
  );
};
