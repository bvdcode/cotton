import {
  Alert,
  Box,
  Chip,
  Divider,
  LinearProgress,
  Paper,
  Skeleton,
  Stack,
  Typography,
  type AlertColor,
} from "@mui/material";
import { alpha } from "@mui/material/styles";
import BuildOutlinedIcon from "@mui/icons-material/BuildOutlined";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import SecurityIcon from "@mui/icons-material/Security";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { useTranslation } from "react-i18next";
import type { TFunction } from "i18next";
import type { ReactNode } from "react";
import { getApiErrorMessage } from "../../../shared/api/httpClient";
import {
  useSecurityDiagnosticsQuery,
} from "../../../shared/api/queries/admin";
import type {
  LinuxProcessSecurityDto,
  SecurityDiagnosticWarningDto,
  SecurityDiagnosticsDto,
} from "../../../shared/api/adminApi";
import { AdminPageSurface } from "../components/AdminPageSurface";

const knownThreatVectorCodes = new Set([
  "public-instance",
  "master-key-from-environment",
  "admins-without-2fa",
  "dotnet-diagnostics-enabled",
  "process-dumpable",
  "sys-ptrace-capability",
  "new-privileges-allowed",
  "seccomp-disabled",
  "running-as-root",
  "process-hardening-failed",
  "db-integrity-unsigned-rows",
  "db-integrity-bridge-mode",
  "root-filesystem-writable",
  "docker-socket-mounted",
  "host-pid-namespace",
  "mandatory-access-control-unconfined",
  "core-dumps-enabled",
]);

interface SecurityLevel {
  title: string;
  summary: string;
  color: AlertColor;
}

const getSecurityLevel = (
  score: number,
  maxScore: number,
  t: TFunction<"admin">,
): SecurityLevel => {
  const normalizedScore = maxScore > 0 ? (score / maxScore) * 10 : 0;

  if (normalizedScore >= 9) {
    return {
      title: t("securityDiagnostics.levels.strong.title"),
      summary: t("securityDiagnostics.levels.strong.summary"),
      color: "success",
    };
  }

  if (normalizedScore >= 7) {
    return {
      title: t("securityDiagnostics.levels.good.title"),
      summary: t("securityDiagnostics.levels.good.summary"),
      color: "success",
    };
  }

  if (normalizedScore >= 5) {
    return {
      title: t("securityDiagnostics.levels.home.title"),
      summary: t("securityDiagnostics.levels.home.summary"),
      color: "warning",
    };
  }

  if (normalizedScore >= 3) {
    return {
      title: t("securityDiagnostics.levels.exposed.title"),
      summary: t("securityDiagnostics.levels.exposed.summary"),
      color: "warning",
    };
  }

  return {
    title: t("securityDiagnostics.levels.unsafe.title"),
    summary: t("securityDiagnostics.levels.unsafe.summary"),
    color: "error",
  };
};

const paletteKeyFor = (
  severity: SecurityDiagnosticWarningDto["severity"],
): "error" | "warning" | "info" =>
  severity === "critical"
    ? "error"
    : severity === "warning"
      ? "warning"
      : "info";

const severityRank = (
  severity: SecurityDiagnosticWarningDto["severity"],
): number =>
  severity === "critical" ? 0 : severity === "warning" ? 1 : 2;

const getSeverityLabel = (
  severity: SecurityDiagnosticWarningDto["severity"],
  t: TFunction<"admin">,
) => {
  if (severity === "critical") {
    return t("securityDiagnostics.severity.critical");
  }

  if (severity === "warning") {
    return t("securityDiagnostics.severity.warning");
  }

  return t("securityDiagnostics.severity.info");
};

const getThreatVector = (
  warning: SecurityDiagnosticWarningDto,
  t: TFunction<"admin">,
): string | null =>
  knownThreatVectorCodes.has(warning.code)
    ? t(`securityDiagnostics.threatVectors.${warning.code}`)
    : null;

const getFixText = (
  warning: SecurityDiagnosticWarningDto,
  t: TFunction<"admin">,
): string | null =>
  knownThreatVectorCodes.has(warning.code)
    ? t(`securityDiagnostics.fixes.${warning.code}`)
    : null;

const formatNullable = (
  value: string | number | boolean | null | undefined,
  t: TFunction<"admin">,
) =>
  value === null || value === undefined || value === ""
    ? t("securityDiagnostics.values.unknown")
    : String(value);

const yesNo = (
  value: boolean | null | undefined,
  t: TFunction<"admin">,
) => {
  if (value === null || value === undefined) {
    return t("securityDiagnostics.values.unknown");
  }

  return value
    ? t("securityDiagnostics.values.yes")
    : t("securityDiagnostics.values.no");
};

const getDumpableLabel = (
  linuxProcess: LinuxProcessSecurityDto,
  t: TFunction<"admin">,
) => {
  if (linuxProcess.dumpable === 0) {
    return t("securityDiagnostics.values.notDumpable");
  }

  if (linuxProcess.dumpable === 1) {
    return t("securityDiagnostics.values.dumpable");
  }

  return formatNullable(linuxProcess.dumpable, t);
};

interface DiagnosticsRowProps {
  label: string;
  value: string;
  color?: AlertColor | "default";
}

const DiagnosticsRow = ({
  label,
  value,
  color = "default",
}: DiagnosticsRowProps) => (
  <Box
    sx={{
      display: "grid",
      gridTemplateColumns: { xs: "1fr", sm: "220px minmax(0, 1fr)" },
      gap: { xs: 0.5, sm: 2 },
      alignItems: "center",
    }}
  >
    <Typography variant="body2" color="text.secondary">
      {label}
    </Typography>
    <Box>
      <Chip size="small" color={color} variant="outlined" label={value} />
    </Box>
  </Box>
);

interface DiagnosticsSectionProps {
  title: string;
  children: ReactNode;
}

const DiagnosticsSection = ({
  title,
  children,
}: DiagnosticsSectionProps) => (
  <Stack spacing={1.5}>
    <Typography variant="subtitle1" fontWeight={700}>
      {title}
    </Typography>
    <Stack spacing={1}>{children}</Stack>
  </Stack>
);

interface SecurityDiagnosticsContentProps {
  diagnostics: SecurityDiagnosticsDto;
  t: TFunction<"admin">;
}

const SecurityDiagnosticsContent = ({
  diagnostics,
  t,
}: SecurityDiagnosticsContentProps) => (
  <Stack spacing={3} divider={<Divider flexItem />}>
    <SecurityScoreSummary diagnostics={diagnostics} t={t} />
    <SecurityRiskSection warnings={diagnostics.warnings} t={t} />
    <InstanceDiagnosticsSection diagnostics={diagnostics} t={t} />
    <MasterKeyDiagnosticsSection diagnostics={diagnostics} t={t} />
    <MemoryDiagnosticsSection diagnostics={diagnostics} t={t} />
    <ContainerDiagnosticsSection diagnostics={diagnostics} t={t} />
    <RuntimeDiagnosticsSection diagnostics={diagnostics} t={t} />
  </Stack>
);

type DiagnosticsContentSectionProps = {
  diagnostics: SecurityDiagnosticsDto;
  t: TFunction<"admin">;
};

const SecurityScoreSummary = ({
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
          {diagnostics.securityScore}/{diagnostics.maxSecurityScore} - {level.title}
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

const getScorePercent = (diagnostics: SecurityDiagnosticsDto) =>
  diagnostics.maxSecurityScore > 0
    ? (diagnostics.securityScore / diagnostics.maxSecurityScore) * 100
    : 0;

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

type SecurityRiskSectionProps = {
  warnings: SecurityDiagnosticWarningDto[];
  t: TFunction<"admin">;
};

const SecurityRiskSection = ({ warnings, t }: SecurityRiskSectionProps) => {
  const sorted = [...warnings].sort(
    (a, b) => severityRank(a.severity) - severityRank(b.severity),
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

type SecurityRiskCardProps = {
  warning: SecurityDiagnosticWarningDto;
  t: TFunction<"admin">;
};

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

const RiskLabeledBlock = ({ label, text }: { label: string; text: string }) => (
  <Box>
    <Typography
      variant="caption"
      color="text.secondary"
      fontWeight={700}
      sx={{ display: "block", textTransform: "uppercase", letterSpacing: 0.4 }}
    >
      {label}
    </Typography>
    <Typography variant="body2">{text}</Typography>
  </Box>
);

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

const InstanceDiagnosticsSection = ({
  diagnostics,
  t,
}: DiagnosticsContentSectionProps) => (
  <DiagnosticsSection title={t("securityDiagnostics.sections.instance")}>
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.publicInstance")}
      value={yesNo(diagnostics.isPublicInstance, t)}
      color={diagnostics.isPublicInstance ? "warning" : "success"}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.admins")}
      value={
        String(diagnostics.adminTotp.adminsWithTotp) +
        "/" +
        String(diagnostics.adminTotp.adminCount)
      }
      color={diagnostics.adminTotp.adminsWithoutTotp > 0 ? "warning" : "success"}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.adminsWithoutTotp")}
      value={String(diagnostics.adminTotp.adminsWithoutTotp)}
      color={diagnostics.adminTotp.adminsWithoutTotp > 0 ? "warning" : "success"}
    />
  </DiagnosticsSection>
);

const MasterKeyDiagnosticsSection = ({
  diagnostics,
  t,
}: DiagnosticsContentSectionProps) => (
  <DiagnosticsSection title={t("securityDiagnostics.sections.masterKey")}>
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.masterKeySource")}
      value={formatNullable(diagnostics.masterKeySource, t)}
      color={
        diagnostics.masterKeyEnvironmentVariableWasConfigured
          ? "warning"
          : "success"
      }
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.envWasConfigured")}
      value={yesNo(diagnostics.masterKeyEnvironmentVariableWasConfigured, t)}
      color={
        diagnostics.masterKeyEnvironmentVariableWasConfigured
          ? "warning"
          : "success"
      }
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.envPresent")}
      value={yesNo(diagnostics.masterKeyEnvironmentVariablePresentInProcess, t)}
      color={
        diagnostics.masterKeyEnvironmentVariablePresentInProcess
          ? "warning"
          : "success"
      }
    />
  </DiagnosticsSection>
);

const MemoryDiagnosticsSection = ({
  diagnostics,
  t,
}: DiagnosticsContentSectionProps) => (
  <DiagnosticsSection title={t("securityDiagnostics.sections.memory")}>
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.dotnetDiagnostics")}
      value={yesNo(diagnostics.dotNetDiagnostics.disabled, t)}
      color={diagnostics.dotNetDiagnostics.disabled ? "success" : "warning"}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.processHardening")}
      value={yesNo(diagnostics.linuxProcess.hardeningApplied, t)}
      color={diagnostics.linuxProcess.hardeningApplied ? "success" : "warning"}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.dumpable")}
      value={getDumpableLabel(diagnostics.linuxProcess, t)}
      color={diagnostics.linuxProcess.dumpable === 0 ? "success" : "warning"}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.sysPtrace")}
      value={yesNo(diagnostics.linuxProcess.hasSysPtraceCapability, t)}
      color={
        diagnostics.linuxProcess.hasSysPtraceCapability ? "error" : "success"
      }
    />
  </DiagnosticsSection>
);


const getLimitSummary = (
  softLimit: string | null | undefined,
  hardLimit: string | null | undefined,
  t: TFunction<"admin">,
) => {
  const soft = formatNullable(softLimit, t);
  const hard = formatNullable(hardLimit, t);
  return `${soft} / ${hard}`;
};

const isUnconfinedAppArmorProfile = (profile: string | null | undefined) =>
  profile?.toLowerCase().startsWith("unconfined") ?? false;

const booleanStatusColor = (
  value: boolean | null | undefined,
  trueColor: AlertColor | "default",
  falseColor: AlertColor | "default",
): AlertColor | "default" => {
  if (value === null || value === undefined) {
    return "default";
  }

  return value ? trueColor : falseColor;
};

const ContainerDiagnosticsSection = ({
  diagnostics,
  t,
}: DiagnosticsContentSectionProps) => (
  <DiagnosticsSection title={t("securityDiagnostics.sections.containerBoundary")}>
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.rootFilesystemReadOnly")}
      value={yesNo(diagnostics.linuxContainer.rootFilesystemReadOnly, t)}
      color={booleanStatusColor(
        diagnostics.linuxContainer.rootFilesystemReadOnly,
        "success",
        "warning",
      )}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.dockerSocketMounted")}
      value={yesNo(diagnostics.linuxContainer.dockerSocketMounted, t)}
      color={booleanStatusColor(
        diagnostics.linuxContainer.dockerSocketMounted,
        "error",
        "success",
      )}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.hostPidNamespace")}
      value={yesNo(diagnostics.linuxContainer.hostPidNamespaceLikely, t)}
      color={booleanStatusColor(
        diagnostics.linuxContainer.hostPidNamespaceLikely,
        "error",
        "success",
      )}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.procOneCommandLine")}
      value={formatNullable(diagnostics.linuxContainer.procOneCommandLine, t)}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.coreDumpLimit")}
      value={getLimitSummary(
        diagnostics.linuxContainer.coreDumpSoftLimit,
        diagnostics.linuxContainer.coreDumpHardLimit,
        t,
      )}
      color={booleanStatusColor(
        diagnostics.linuxContainer.coreDumpSoftLimitDisabled,
        "success",
        "warning",
      )}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.corePattern")}
      value={formatNullable(diagnostics.linuxContainer.corePattern, t)}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.appArmorProfile")}
      value={formatNullable(diagnostics.linuxContainer.appArmorProfile, t)}
      color={
        diagnostics.linuxContainer.appArmorProfile
          ? booleanStatusColor(
              isUnconfinedAppArmorProfile(diagnostics.linuxContainer.appArmorProfile),
              "warning",
              "success",
            )
          : "default"
      }
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.selinuxContext")}
      value={formatNullable(diagnostics.linuxContainer.selinuxContext, t)}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.selinuxEnforcing")}
      value={yesNo(diagnostics.linuxContainer.selinuxEnforcing, t)}
      color={booleanStatusColor(
        diagnostics.linuxContainer.selinuxEnforcing,
        "success",
        "warning",
      )}
    />
  </DiagnosticsSection>
);

const RuntimeDiagnosticsSection = ({
  diagnostics,
  t,
}: DiagnosticsContentSectionProps) => (
  <DiagnosticsSection title={t("securityDiagnostics.sections.runtime")}>
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.os")}
      value={diagnostics.operatingSystem}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.container")}
      value={yesNo(diagnostics.isContainer, t)}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.euid")}
      value={formatNullable(diagnostics.linuxProcess.effectiveUserId, t)}
      color={diagnostics.linuxProcess.runningAsRoot === true ? "warning" : "default"}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.noNewPrivileges")}
      value={formatNullable(diagnostics.linuxProcess.noNewPrivileges, t)}
      color={diagnostics.linuxProcess.noNewPrivileges === 1 ? "success" : "warning"}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.seccomp")}
      value={formatNullable(diagnostics.linuxProcess.seccompMode, t)}
      color={diagnostics.linuxProcess.seccompMode === 0 ? "warning" : "success"}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.capabilities")}
      value={formatNullable(diagnostics.linuxProcess.effectiveCapabilitiesHex, t)}
    />
  </DiagnosticsSection>
);

export const AdminSecurityDiagnosticsPage = () => {
  const { t } = useTranslation("admin");
  const diagnosticsQuery = useSecurityDiagnosticsQuery();
  const loadError = diagnosticsQuery.isError
    ? getApiErrorMessage(diagnosticsQuery.error) ??
      t("securityDiagnostics.errors.loadFailed")
    : null;

  return (
    <Stack>
      <AdminPageSurface>
        <Stack p={3} spacing={3} divider={<Divider flexItem />}>
          <Stack direction="row" spacing={1.5} alignItems="center">
            <SecurityIcon color="primary" />
            <Stack spacing={0.5}>
              <Typography variant="h5" fontWeight={700}>
                {t("securityDiagnostics.title")}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {t("securityDiagnostics.description")}
              </Typography>
            </Stack>
          </Stack>

          {diagnosticsQuery.isPending && (
            <Stack spacing={1.5}>
              <Skeleton variant="rounded" height={96} />
              <Skeleton variant="rounded" height={72} />
              <Skeleton variant="rounded" height={180} />
            </Stack>
          )}

          {loadError && <Alert severity="error">{loadError}</Alert>}

          {diagnosticsQuery.data && (
            <SecurityDiagnosticsContent
              diagnostics={diagnosticsQuery.data}
              t={t}
            />
          )}
        </Stack>
      </AdminPageSurface>
    </Stack>
  );
};
