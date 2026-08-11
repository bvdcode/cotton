import type { AlertColor } from "@mui/material";
import type { TFunction } from "i18next";
import type {
  LinuxProcessSecurityDto,
  SecurityDiagnosticWarningDto,
  SecurityDiagnosticsDto,
} from "../../../shared/api/adminApi";

const knownThreatVectorCodes = new Set([
  "public-instance",
  "trusted-proxy-not-configured",
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
  "temp-directory-not-writable",
  "root-filesystem-writable",
  "docker-socket-mounted",
  "host-pid-namespace",
  "mandatory-access-control-unconfined",
  "core-dumps-enabled",
]);

export interface SecurityLevel {
  title: string;
  summary: string;
  color: AlertColor;
}

export const getSecurityLevel = (
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

export const paletteKeyFor = (
  severity: SecurityDiagnosticWarningDto["severity"],
): "error" | "warning" | "info" => {
  if (severity === "critical") {
    return "error";
  }

  if (severity === "warning") {
    return "warning";
  }

  return "info";
};

export const severityRank = (
  severity: SecurityDiagnosticWarningDto["severity"],
): number => {
  if (severity === "critical") {
    return 0;
  }

  if (severity === "warning") {
    return 1;
  }

  return 2;
};

export const getSeverityLabel = (
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

export const getThreatVector = (
  warning: SecurityDiagnosticWarningDto,
  t: TFunction<"admin">,
): string | null => {
  if (!knownThreatVectorCodes.has(warning.code)) {
    return null;
  }

  return warning.code === "trusted-proxy-not-configured"
    ? t("securityDiagnostics.trustedProxy.threatVector")
    : t(`securityDiagnostics.threatVectors.${warning.code}`);
};

export const getFixText = (
  warning: SecurityDiagnosticWarningDto,
  t: TFunction<"admin">,
): string | null => {
  if (!knownThreatVectorCodes.has(warning.code)) {
    return null;
  }

  return warning.code === "trusted-proxy-not-configured"
    ? t("securityDiagnostics.trustedProxy.fix")
    : t(`securityDiagnostics.fixes.${warning.code}`);
};

export const getPassedThreatVector = (
  code: string,
  t: TFunction<"admin">,
): string | null =>
  knownThreatVectorCodes.has(code)
    ? t(`securityDiagnostics.threatVectors.${code}`)
    : null;

export const formatNullable = (
  value: string | number | boolean | null | undefined,
  t: TFunction<"admin">,
) =>
  value === null || value === undefined || value === ""
    ? t("securityDiagnostics.values.unknown")
    : String(value);

export const yesNo = (
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

export const getDumpableLabel = (
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

export const getScorePercent = (diagnostics: SecurityDiagnosticsDto) =>
  diagnostics.maxSecurityScore > 0
    ? (diagnostics.securityScore / diagnostics.maxSecurityScore) * 100
    : 0;

export const isUnconfinedAppArmorProfile = (
  profile: string | null | undefined,
) => profile?.toLowerCase().startsWith("unconfined") ?? false;

export const getPassedCheckCodes = (
  diagnostics: SecurityDiagnosticsDto,
): string[] => {
  const linuxProcess = diagnostics.linuxProcess;
  const linuxContainer = diagnostics.linuxContainer;
  const checks: ReadonlyArray<readonly [string, boolean]> = [
    ["public-instance", diagnostics.isPublicInstance === false],
    [
      "trusted-proxy-not-configured",
      Boolean(diagnostics.trustedProxyIpAddress),
    ],
    [
      "master-key-from-environment",
      diagnostics.masterKeyEnvironmentVariableWasConfigured === false,
    ],
    [
      "admins-without-2fa",
      diagnostics.adminTotp.adminCount > 0 &&
        diagnostics.adminTotp.adminsWithoutTotp === 0,
    ],
    [
      "dotnet-diagnostics-enabled",
      diagnostics.dotNetDiagnostics.disabled === true,
    ],
    ["temp-directory-not-writable", diagnostics.tempDirectoryWritable === true],
    ["process-hardening-failed", linuxProcess.hardeningApplied === true],
    ["process-dumpable", linuxProcess.dumpable === 0],
    ["sys-ptrace-capability", linuxProcess.hasSysPtraceCapability === false],
    ["new-privileges-allowed", linuxProcess.noNewPrivileges === 1],
    [
      "seccomp-disabled",
      linuxProcess.seccompMode !== null &&
        linuxProcess.seccompMode !== undefined &&
        linuxProcess.seccompMode !== 0,
    ],
    ["running-as-root", linuxProcess.runningAsRoot === false],
    [
      "root-filesystem-writable",
      linuxContainer.rootFilesystemReadOnly === true,
    ],
    ["docker-socket-mounted", linuxContainer.dockerSocketMounted === false],
    ["host-pid-namespace", linuxContainer.hostPidNamespaceLikely === false],
    ["core-dumps-enabled", linuxContainer.coreDumpSoftLimitDisabled === true],
    [
      "mandatory-access-control-unconfined",
      (linuxContainer.appArmorProfile !== null &&
        linuxContainer.appArmorProfile !== undefined &&
        !isUnconfinedAppArmorProfile(linuxContainer.appArmorProfile)) ||
        linuxContainer.selinuxEnforcing === true,
    ],
  ];

  return checks.filter(([, passed]) => passed).map(([code]) => code);
};

export const getLimitSummary = (
  softLimit: string | null | undefined,
  hardLimit: string | null | undefined,
  t: TFunction<"admin">,
) => {
  const soft = formatNullable(softLimit, t);
  const hard = formatNullable(hardLimit, t);
  return `${soft} / ${hard}`;
};

export const booleanStatusColor = (
  value: boolean | null | undefined,
  trueColor: AlertColor | "default",
  falseColor: AlertColor | "default",
): AlertColor | "default" => {
  if (value === null || value === undefined) {
    return "default";
  }

  return value ? trueColor : falseColor;
};
