import {
  Alert,
  Avatar,
  Box,
  Container,
  Divider,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import { ReportProblem } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { StartupBlocker } from "../../shared/api/startupApi";

type StartupBlockedPageProps = {
  blocker: StartupBlocker | null;
};

type DetailRow = {
  label: string;
  value: string | null | undefined;
};

const DetailRows = ({ rows }: { rows: DetailRow[] }): React.ReactElement | null => {
  const visibleRows = rows.filter((row) => row.value);
  if (visibleRows.length === 0) {
    return null;
  }

  return (
    <Stack spacing={1.5}>
      <Divider />
      {visibleRows.map((row) => (
        <Box
          key={row.label}
          sx={{
            display: "flex",
            justifyContent: "space-between",
            gap: 2,
            flexWrap: "wrap",
          }}
        >
          <Typography variant="body2" color="text.secondary">
            {row.label}
          </Typography>
          <Typography variant="body2" fontWeight={600}>
            {row.value}
          </Typography>
        </Box>
      ))}
    </Stack>
  );
};

export const StartupBlockedPage = ({
  blocker,
}: StartupBlockedPageProps): React.ReactElement => {
  const { t } = useTranslation("startup");
  const title = blocker?.title || t("fallbackTitle");
  const message = blocker?.message || t("fallbackMessage");
  const rows: DetailRow[] = [
    {
      label: t("fields.currentVersion"),
      value: blocker?.currentVersion,
    },
    {
      label: t("fields.requiredVersion"),
      value: blocker?.requiredVersion,
    },
    {
      label: t("fields.requiredVersionRange"),
      value: blocker?.requiredVersionRange,
    },
    {
      label: t("fields.lastRecordedVersion"),
      value: blocker?.lastRecordedVersion,
    },
  ];

  return (
    <Box
      sx={{
        minHeight: "100dvh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        py: { xs: 2, sm: 4 },
      }}
    >
      <Container
        maxWidth="sm"
        sx={{
          display: "flex",
          flexDirection: "column",
          px: { xs: 2, sm: 3 },
        }}
      >
        <Paper sx={{ p: { xs: 3, sm: 4 } }}>
          <Stack spacing={2.5}>
            <Box
              sx={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                gap: 2,
              }}
            >
              <Box>
                <Typography variant="h4" component="h1">
                  {title}
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mt: 0.75 }}>
                  {t("caption")}
                </Typography>
              </Box>
              <Avatar src="/assets/icons/icon.svg" alt="Cotton" />
            </Box>
            <Alert severity="error" icon={<ReportProblem />}>
              {message}
            </Alert>
            <DetailRows rows={rows} />
          </Stack>
        </Paper>
      </Container>
    </Box>
  );
};
