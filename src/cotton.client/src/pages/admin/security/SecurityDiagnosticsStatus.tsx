import {
  Alert,
  Box,
  Chip,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import type { TFunction } from "i18next";
import {
  DiagnosticsSection,
  PositiveCard,
  RiskLabeledBlock,
  type DiagnosticsContentSectionProps,
} from "./SecurityDiagnosticsPrimitives";
import {
  getPassedCheckCodes,
  getPassedThreatVector,
  getScorePercent,
  getSecurityLevel,
} from "./securityDiagnosticsPresentation";

export const SecurityScoreSummary = ({
  diagnostics,
  t,
}: DiagnosticsContentSectionProps) => {
  const level = getSecurityLevel(
    diagnostics.securityScore,
    diagnostics.maxSecurityScore,
    t,
  );
  const scorePercent = getScorePercent(diagnostics);

  return (
    <Stack spacing={2}>
      <Alert
        severity={level.color}
        icon={level.color === "success" ? <CheckCircleIcon /> : undefined}
      >
        <Typography variant="subtitle2" fontWeight={700}>
          {diagnostics.securityScore}/{diagnostics.maxSecurityScore} -{" "}
          {level.title}
        </Typography>
        <Typography variant="body2">{level.summary}</Typography>
      </Alert>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
        <LinearProgress
          variant="determinate"
          value={Math.max(0, Math.min(100, scorePercent))}
          color={level.color}
          sx={{ flex: 1, height: 8, borderRadius: 1, bgcolor: "action.hover" }}
        />
        <Typography
          variant="body2"
          color="text.secondary"
          sx={{ fontVariantNumeric: "tabular-nums", whiteSpace: "nowrap" }}
        >
          {diagnostics.securityScore} / {diagnostics.maxSecurityScore}
        </Typography>
      </Box>
      <SecuritySummaryChips diagnostics={diagnostics} t={t} />
    </Stack>
  );
};

const SecuritySummaryChips = ({
  diagnostics,
  t,
}: DiagnosticsContentSectionProps) => (
  <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
    <Chip
      size="small"
      color={diagnostics.isPublicInstance ? "warning" : "success"}
      label={
        diagnostics.isPublicInstance
          ? t("securityDiagnostics.chips.publicInstance")
          : t("securityDiagnostics.chips.privateInstance")
      }
    />
    <Chip
      size="small"
      color={
        diagnostics.masterKeyEnvironmentVariableWasConfigured
          ? "warning"
          : "success"
      }
      label={
        diagnostics.masterKeyEnvironmentVariableWasConfigured
          ? t("securityDiagnostics.chips.envKey")
          : t("securityDiagnostics.chips.memoryUnlock")
      }
    />
    <Chip
      size="small"
      color={
        diagnostics.adminTotp.adminsWithoutTotp > 0 ? "warning" : "success"
      }
      label={t("securityDiagnostics.chips.adminTotp", {
        withTotp: diagnostics.adminTotp.adminsWithTotp,
        total: diagnostics.adminTotp.adminCount,
      })}
    />
  </Stack>
);

interface SecurityPassedCardProps {
  code: string;
  t: TFunction<"admin">;
}

const SecurityPassedCard = ({ code, t }: SecurityPassedCardProps) => {
  const guardsAgainst = getPassedThreatVector(code, t);

  return (
    <PositiveCard
      title={
        code === "trusted-proxy-not-configured"
          ? t("securityDiagnostics.trustedProxy.passed")
          : t(`securityDiagnostics.passed.${code}`)
      }
      code={code}
    >
      {guardsAgainst && (
        <RiskLabeledBlock
          label={t("securityDiagnostics.labels.guardsAgainst")}
          text={guardsAgainst}
        />
      )}
    </PositiveCard>
  );
};

export const SecurityPassedSection = ({
  diagnostics,
  t,
}: DiagnosticsContentSectionProps) => {
  const warningCodes = new Set(
    diagnostics.warnings.map((warning) => warning.code),
  );
  const codes = getPassedCheckCodes(diagnostics).filter(
    (code) => !warningCodes.has(code),
  );
  const cpu = diagnostics.cpuFeatures;
  const showAesAcceleration = cpu?.aesGcmHardwareAccelerationLikely === true;
  const cpuDescriptor = cpu
    ? [
        cpu.vendorId?.trim(),
        cpu.architecture?.trim(),
        cpu.logicalProcessorCount ? `${cpu.logicalProcessorCount}×` : undefined,
      ]
        .filter((part): part is string => Boolean(part))
        .join(" · ")
    : "";

  if (codes.length === 0 && !showAesAcceleration) {
    return null;
  }

  return (
    <DiagnosticsSection title={t("securityDiagnostics.sections.passed")}>
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
        {codes.map((code) => (
          <SecurityPassedCard key={code} code={code} t={t} />
        ))}
        {showAesAcceleration && (
          <PositiveCard
            title={t("securityDiagnostics.capabilities.aesAcceleration.title")}
          >
            <Typography variant="body2">
              {t("securityDiagnostics.capabilities.aesAcceleration.body")}
            </Typography>
            {cpuDescriptor && (
              <Typography
                variant="caption"
                color="text.secondary"
                sx={{ fontVariantNumeric: "tabular-nums" }}
              >
                {cpuDescriptor}
              </Typography>
            )}
          </PositiveCard>
        )}
      </Box>
    </DiagnosticsSection>
  );
};
