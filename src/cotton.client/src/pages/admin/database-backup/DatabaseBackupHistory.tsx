import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Box,
  Chip,
  Stack,
  Typography,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { useTranslation } from "react-i18next";
import type { LatestDatabaseBackupDto } from "../../../shared/api/adminApi";
import { formatBytes } from "../../../shared/utils/formatBytes";
import { HelpButton } from "../../../shared/ui/HelpButton";

type Props = { backups: LatestDatabaseBackupDto[] };

export const DatabaseBackupHistory = ({ backups }: Props) => {
  const { t, i18n } = useTranslation("admin");
  const formatDateTime = (value: string) => {
    const normalized = /([zZ]|[+-]\d{2}:\d{2})$/.test(value)
      ? value
      : `${value}Z`;
    const date = new Date(normalized);
    if (Number.isNaN(date.getTime())) {
      return value;
    }
    return new Intl.DateTimeFormat(i18n.resolvedLanguage, {
      dateStyle: "medium",
      timeStyle: "short",
    }).format(date);
  };
  return (
    <Stack spacing={1} minWidth={0}>
      <Stack direction="row" spacing={1} alignItems="center">
        <Typography variant="h6">
          {t("databaseBackup.history.title")}
        </Typography>
        <HelpButton
          title={t("databaseBackup.history.title")}
          content={
            <Stack spacing={2}>
              <Typography variant="body2">
                {t("databaseBackup.history.retention")}
              </Typography>
              <Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>
                {t("databaseBackup.state.restoreIfEmptyHint")}
              </Typography>
            </Stack>
          }
        />
      </Stack>
      <Box>
        {backups.map((backup, index) => (
          <Accordion key={backup.backupId}>
            <AccordionSummary
              expandIcon={<ExpandMoreIcon />}
              aria-controls={`backup-${backup.backupId}-details`}
              id={`backup-${backup.backupId}-header`}
            >
              <Stack spacing={0.5} minWidth={0}>
                <Stack
                  direction="row"
                  spacing={1}
                  useFlexGap
                  flexWrap="wrap"
                  alignItems="center"
                >
                  <Typography>{formatDateTime(backup.createdAtUtc)}</Typography>
                  {index === 0 && (
                    <Chip
                      size="small"
                      label={t("databaseBackup.history.latest")}
                    />
                  )}
                </Stack>
                <Typography variant="body2" color="text.secondary">
                  {formatBytes(backup.dumpSizeBytes)} ·{" "}
                  {t("databaseBackup.fields.chunkCount")}: {backup.chunkCount}
                </Typography>
              </Stack>
            </AccordionSummary>
            <AccordionDetails>
              <Box
                sx={{
                  display: "grid",
                  gap: 2,
                  gridTemplateColumns: {
                    xs: "minmax(0, 1fr)",
                    sm: "repeat(2, minmax(0, 1fr))",
                  },
                }}
              >
                {[
                  ["backupId", backup.backupId],
                  [
                    "pointerUpdatedAtUtc",
                    formatDateTime(backup.pointerUpdatedAtUtc),
                  ],
                  ["dumpContentHash", backup.dumpContentHash],
                  ["sourceDatabase", backup.sourceDatabase],
                  ["sourceHost", backup.sourceHost],
                  ["sourcePort", String(backup.sourcePort)],
                ].map(([field, value]) => (
                  <Box key={field} minWidth={0}>
                    <Typography variant="caption" color="text.secondary">
                      {t(`databaseBackup.fields.${field}`)}
                    </Typography>
                    <Typography
                      variant="body2"
                      sx={{ overflowWrap: "anywhere" }}
                    >
                      {value}
                    </Typography>
                  </Box>
                ))}
              </Box>
            </AccordionDetails>
          </Accordion>
        ))}
      </Box>
    </Stack>
  );
};
