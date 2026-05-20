import {
  Alert,
  Box,
  Chip,
  Divider,
  LinearProgress,
  Skeleton,
  Stack,
  Typography,
  type AlertColor,
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
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

const getSeverityColor = (
  warning: SecurityDiagnosticWarningDto,
): AlertColor => {
  if (warning.severity === "critical") {
    return "error";
  }

  if (warning.severity === "warning") {
    return "warning";
  }

  return "info";
};

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
) =>
  knownThreatVectorCodes.has(warning.code)
    ? t(`securityDiagnostics.threatVectors.${warning.code}`)
    : warning.message;

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
}: SecurityDiagnosticsContentProps) => {
  const level = getSecurityLevel(
    diagnostics.securityScore,
    diagnostics.maxSecurityScore,
    t,
  );
  const scorePercent =
    diagnostics.maxSecurityScore > 0
      ? (diagnostics.securityScore / diagnostics.maxSecurityScore) * 100
      : 0;
  const hasWarnings = diagnostics.warnings.length > 0;

  return (
    <Stack spacing={3} divider={<Divider flexItem />}>
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

        <Box>
          <LinearProgress
            variant="determinate"
            value={Math.max(0, Math.min(100, scorePercent))}
            color={level.color}
            sx={{ height: 8, borderRadius: 1 }}
          />
        </Box>

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
              diagnostics.adminTotp.adminsWithoutTotp > 0
                ? "warning"
                : "success"
            }
            label={t("securityDiagnostics.chips.adminTotp", {
              withTotp: diagnostics.adminTotp.adminsWithTotp,
              total: diagnostics.adminTotp.adminCount,
            })}
          />
        </Stack>
      </Stack>

      <DiagnosticsSection
        title={t("securityDiagnostics.sections.risks")}
      >
        {hasWarnings ? (
          diagnostics.warnings.map((warning) => (
            <Alert
              key={warning.code}
              severity={getSeverityColor(warning)}
              icon={<WarningAmberIcon />}
            >
              <Stack spacing={0.5}>
                <Stack
                  direction="row"
                  spacing={1}
                  alignItems="center"
                  useFlexGap
                  sx={{ flexWrap: "wrap" }}
                >
                  <Typography variant="subtitle2" fontWeight={700}>
                    {getSeverityLabel(warning.severity, t)}
                  </Typography>
                  <Chip
                    size="small"
                    variant="outlined"
                    label={warning.code}
                  />
                </Stack>
                <Typography variant="body2">{warning.message}</Typography>
                <Typography variant="body2" color="text.secondary">
                  {getThreatVector(warning, t)}
                </Typography>
              </Stack>
            </Alert>
          ))
        ) : (
          <Alert severity="success">
            {t("securityDiagnostics.risks.empty")}
          </Alert>
        )}
      </DiagnosticsSection>

      <DiagnosticsSection
        title={t("securityDiagnostics.sections.instance")}
      >
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.publicInstance")}
          value={yesNo(diagnostics.isPublicInstance, t)}
          color={diagnostics.isPublicInstance ? "warning" : "success"}
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.admins")}
          value={`${diagnostics.adminTotp.adminsWithTotp}/${diagnostics.adminTotp.adminCount}`}
          color={
            diagnostics.adminTotp.adminsWithoutTotp > 0
              ? "warning"
              : "success"
          }
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.adminsWithoutTotp")}
          value={String(diagnostics.adminTotp.adminsWithoutTotp)}
          color={
            diagnostics.adminTotp.adminsWithoutTotp > 0 ? "warning" : "success"
          }
        />
      </DiagnosticsSection>

      <DiagnosticsSection
        title={t("securityDiagnostics.sections.masterKey")}
      >
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
          value={yesNo(
            diagnostics.masterKeyEnvironmentVariableWasConfigured,
            t,
          )}
          color={
            diagnostics.masterKeyEnvironmentVariableWasConfigured
              ? "warning"
              : "success"
          }
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.envPresent")}
          value={yesNo(
            diagnostics.masterKeyEnvironmentVariablePresentInProcess,
            t,
          )}
          color={
            diagnostics.masterKeyEnvironmentVariablePresentInProcess
              ? "warning"
              : "success"
          }
        />
      </DiagnosticsSection>

      <DiagnosticsSection
        title={t("securityDiagnostics.sections.memory")}
      >
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.dotnetDiagnostics")}
          value={yesNo(diagnostics.dotNetDiagnostics.disabled, t)}
          color={diagnostics.dotNetDiagnostics.disabled ? "success" : "warning"}
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.processHardening")}
          value={yesNo(diagnostics.linuxProcess.hardeningApplied, t)}
          color={
            diagnostics.linuxProcess.hardeningApplied ? "success" : "warning"
          }
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
            diagnostics.linuxProcess.hasSysPtraceCapability
              ? "error"
              : "success"
          }
        />
      </DiagnosticsSection>

      <DiagnosticsSection
        title={t("securityDiagnostics.sections.runtime")}
      >
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
          value={formatNullable(
            diagnostics.linuxProcess.effectiveUserId,
            t,
          )}
          color={
            diagnostics.linuxProcess.runningAsRoot === true
              ? "warning"
              : "default"
          }
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.noNewPrivileges")}
          value={formatNullable(
            diagnostics.linuxProcess.noNewPrivileges,
            t,
          )}
          color={
            diagnostics.linuxProcess.noNewPrivileges === 1
              ? "success"
              : "warning"
          }
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.seccomp")}
          value={formatNullable(diagnostics.linuxProcess.seccompMode, t)}
          color={
            diagnostics.linuxProcess.seccompMode === 0 ? "warning" : "success"
          }
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.capabilities")}
          value={formatNullable(
            diagnostics.linuxProcess.effectiveCapabilitiesHex,
            t,
          )}
        />
      </DiagnosticsSection>
    </Stack>
  );
};

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
