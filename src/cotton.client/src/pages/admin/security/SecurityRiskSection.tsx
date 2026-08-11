import { Alert, Box, Chip, Paper, Stack, Typography } from "@mui/material";
import { alpha } from "@mui/material/styles";
import BuildOutlinedIcon from "@mui/icons-material/BuildOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import type { TFunction } from "i18next";
import type { SecurityDiagnosticWarningDto } from "../../../shared/api/adminApi";
import {
  DiagnosticsSection,
  RiskLabeledBlock,
} from "./SecurityDiagnosticsPrimitives";
import {
  getFixText,
  getSeverityLabel,
  getThreatVector,
  paletteKeyFor,
  severityRank,
} from "./securityDiagnosticsPresentation";

interface SecurityRiskSectionProps {
  warnings: SecurityDiagnosticWarningDto[];
  t: TFunction<"admin">;
}

export const SecurityRiskSection = ({
  warnings,
  t,
}: SecurityRiskSectionProps) => {
  const sorted = [...warnings].sort(
    (left, right) => severityRank(left.severity) - severityRank(right.severity),
  );

  return (
    <DiagnosticsSection title={t("securityDiagnostics.sections.risks")}>
      {sorted.length > 0 ? (
        <Box
          sx={{
            display: "grid",
            gap: 1.5,
            gridTemplateColumns: {
              xs: "1fr",
              lg: "repeat(2, minmax(0, 1fr))",
            },
          }}
        >
          {sorted.map((warning) => (
            <SecurityRiskCard key={warning.code} warning={warning} t={t} />
          ))}
        </Box>
      ) : (
        <Alert severity="success">{t("securityDiagnostics.risks.empty")}</Alert>
      )}
    </DiagnosticsSection>
  );
};

interface SecurityRiskCardProps {
  warning: SecurityDiagnosticWarningDto;
  t: TFunction<"admin">;
}

const SeverityIcon = ({
  severity,
}: {
  severity: SecurityDiagnosticWarningDto["severity"];
}) => {
  if (severity === "critical") {
    return <ErrorOutlineIcon fontSize="small" />;
  }

  if (severity === "warning") {
    return <WarningAmberIcon fontSize="small" />;
  }

  return <InfoOutlinedIcon fontSize="small" />;
};

const SecurityRiskCard = ({ warning, t }: SecurityRiskCardProps) => {
  const paletteKey = paletteKeyFor(warning.severity);
  const threatVector = getThreatVector(warning, t);
  const fix = getFixText(warning, t);

  return (
    <Paper
      variant="outlined"
      sx={(theme) => ({
        overflow: "hidden",
        display: "flex",
        flexDirection: "column",
        borderLeft: `4px solid ${theme.palette[paletteKey].main}`,
      })}
    >
      <Box
        sx={(theme) => ({
          display: "flex",
          alignItems: "center",
          gap: 1,
          px: 2,
          py: 1,
          color: theme.palette[paletteKey].main,
          bgcolor: alpha(theme.palette[paletteKey].main, 0.1),
        })}
      >
        <SeverityIcon severity={warning.severity} />
        <Typography variant="subtitle2" fontWeight={700} color="inherit">
          {getSeverityLabel(warning.severity, t)}
        </Typography>
        <Chip
          size="small"
          variant="outlined"
          label={warning.code}
          sx={{ ml: "auto" }}
        />
      </Box>
      <Stack spacing={1.25} sx={{ px: 2, py: 1.5 }}>
        <RiskLabeledBlock
          label={t("securityDiagnostics.labels.whatItMeans")}
          text={warning.message}
        />
        {threatVector && (
          <RiskLabeledBlock
            label={t("securityDiagnostics.labels.impact")}
            text={threatVector}
          />
        )}
        {fix && (
          <Box
            sx={{
              display: "flex",
              gap: 1,
              p: 1.25,
              borderRadius: 1,
              bgcolor: "action.hover",
            }}
          >
            <BuildOutlinedIcon
              fontSize="small"
              sx={{ color: "text.secondary", mt: 0.25, flexShrink: 0 }}
            />
            <RiskLabeledBlock
              label={t("securityDiagnostics.labels.howToFix")}
              text={fix}
            />
          </Box>
        )}
      </Stack>
    </Paper>
  );
};
