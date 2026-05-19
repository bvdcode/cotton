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

const threatVectorDefaults: Record<string, string> = {
  "public-instance":
    "Internet visitors can create accounts and store content. Quotas, default demo data, and abuse monitoring become part of the security boundary.",
  "master-key-from-environment":
    "If deployment metadata, shell history, or container inspection exposes environment variables, the master key may sit close to the encrypted data it protects.",
  "admins-without-2fa":
    "An admin password becomes the only factor. Password reuse, phishing, or leaked browser sessions can turn into full instance control.",
  "dotnet-diagnostics-enabled":
    "A local process with enough rights can ask the .NET runtime for dumps or traces, and those dumps may contain live secrets from memory.",
  "process-dumpable":
    "A same-UID process or ptrace-capable debugger can dump the server memory and search for the in-memory master key.",
  "sys-ptrace-capability":
    "A debug or privileged container can inspect and alter process memory. That is exactly the capability memory-only keys try to avoid.",
  "new-privileges-allowed":
    "If compromised code finds a setuid helper or capability-bearing binary, it has a wider path to gain privileges inside the container.",
  "seccomp-disabled":
    "The kernel syscall surface is wider than Docker's normal production baseline, so a container escape bug gets more room to breathe.",
  "running-as-root":
    "Remote code execution would run as root inside the container, which increases damage against mounted volumes and helper processes.",
  "process-hardening-failed":
    "Cotton requested process hardening, but the process still started without the expected dump or ptrace protection.",
};

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
      title: t("securityDiagnostics.levels.strong.title", {
        defaultValue: "Strong posture",
      }),
      summary: t("securityDiagnostics.levels.strong.summary", {
        defaultValue:
          "This instance has the expected production hardening. It is a good fit for public exposure when you also trust the host and backups.",
      }),
      color: "success",
    };
  }

  if (normalizedScore >= 7) {
    return {
      title: t("securityDiagnostics.levels.good.title", {
        defaultValue: "Good self-hosted baseline",
      }),
      summary: t("securityDiagnostics.levels.good.summary", {
        defaultValue:
          "This is good for a family or small team. The remaining items are mostly operational hardening and account hygiene.",
      }),
      color: "success",
    };
  }

  if (normalizedScore >= 5) {
    return {
      title: t("securityDiagnostics.levels.home.title", {
        defaultValue: "Home-use baseline",
      }),
      summary: t("securityDiagnostics.levels.home.summary", {
        defaultValue:
          "This is reasonable for home and daily use, but do not treat the host as hostile. A server compromise may still expose keys or admin access.",
      }),
      color: "warning",
    };
  }

  if (normalizedScore >= 3) {
    return {
      title: t("securityDiagnostics.levels.exposed.title", {
        defaultValue: "Practical attack paths remain",
      }),
      summary: t("securityDiagnostics.levels.exposed.summary", {
        defaultValue:
          "The instance has issues that a local attacker, compromised container, or stolen admin password could realistically use.",
      }),
      color: "warning",
    };
  }

  return {
    title: t("securityDiagnostics.levels.unsafe.title", {
      defaultValue: "Unsafe for sensitive data",
    }),
    summary: t("securityDiagnostics.levels.unsafe.summary", {
      defaultValue:
        "Fix the critical warnings before storing valuable private data. At this level, the master key or admin control may be too easy to reach.",
    }),
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
    return t("securityDiagnostics.severity.critical", {
      defaultValue: "Critical",
    });
  }

  if (severity === "warning") {
    return t("securityDiagnostics.severity.warning", {
      defaultValue: "Warning",
    });
  }

  return t("securityDiagnostics.severity.info", {
    defaultValue: "Info",
  });
};

const getThreatVector = (
  warning: SecurityDiagnosticWarningDto,
  t: TFunction<"admin">,
) =>
  t(`securityDiagnostics.threatVectors.${warning.code}`, {
    defaultValue: threatVectorDefaults[warning.code] ?? warning.message,
  });

const formatNullable = (
  value: string | number | boolean | null | undefined,
  t: TFunction<"admin">,
) =>
  value === null || value === undefined || value === ""
    ? t("securityDiagnostics.values.unknown", { defaultValue: "Unknown" })
    : String(value);

const yesNo = (
  value: boolean | null | undefined,
  t: TFunction<"admin">,
) => {
  if (value === null || value === undefined) {
    return t("securityDiagnostics.values.unknown", { defaultValue: "Unknown" });
  }

  return value
    ? t("securityDiagnostics.values.yes", { defaultValue: "Yes" })
    : t("securityDiagnostics.values.no", { defaultValue: "No" });
};

const getDumpableLabel = (
  linuxProcess: LinuxProcessSecurityDto,
  t: TFunction<"admin">,
) => {
  if (linuxProcess.dumpable === 0) {
    return t("securityDiagnostics.values.notDumpable", {
      defaultValue: "Not dumpable",
    });
  }

  if (linuxProcess.dumpable === 1) {
    return t("securityDiagnostics.values.dumpable", {
      defaultValue: "Dumpable",
    });
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
                ? t("securityDiagnostics.chips.publicInstance", {
                    defaultValue: "Public instance",
                  })
                : t("securityDiagnostics.chips.privateInstance", {
                    defaultValue: "Private instance",
                  })
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
                ? t("securityDiagnostics.chips.envKey", {
                    defaultValue: "Key from environment",
                  })
                : t("securityDiagnostics.chips.memoryUnlock", {
                    defaultValue: "Memory unlock",
                  })
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
              defaultValue: "{{withTotp}}/{{total}} admins have 2FA",
              withTotp: diagnostics.adminTotp.adminsWithTotp,
              total: diagnostics.adminTotp.adminCount,
            })}
          />
        </Stack>
      </Stack>

      <DiagnosticsSection
        title={t("securityDiagnostics.sections.risks", {
          defaultValue: "Threat vectors",
        })}
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
            {t("securityDiagnostics.risks.empty", {
              defaultValue:
                "No warnings were detected by the current server-side checks.",
            })}
          </Alert>
        )}
      </DiagnosticsSection>

      <DiagnosticsSection
        title={t("securityDiagnostics.sections.instance", {
          defaultValue: "Instance and accounts",
        })}
      >
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.publicInstance", {
            defaultValue: "Public registration",
          })}
          value={yesNo(diagnostics.isPublicInstance, t)}
          color={diagnostics.isPublicInstance ? "warning" : "success"}
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.admins", {
            defaultValue: "Admins with 2FA",
          })}
          value={`${diagnostics.adminTotp.adminsWithTotp}/${diagnostics.adminTotp.adminCount}`}
          color={
            diagnostics.adminTotp.adminsWithoutTotp > 0
              ? "warning"
              : "success"
          }
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.adminsWithoutTotp", {
            defaultValue: "Admins without 2FA",
          })}
          value={String(diagnostics.adminTotp.adminsWithoutTotp)}
          color={
            diagnostics.adminTotp.adminsWithoutTotp > 0 ? "warning" : "success"
          }
        />
      </DiagnosticsSection>

      <DiagnosticsSection
        title={t("securityDiagnostics.sections.masterKey", {
          defaultValue: "Master key",
        })}
      >
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.masterKeySource", {
            defaultValue: "Source",
          })}
          value={formatNullable(diagnostics.masterKeySource, t)}
          color={
            diagnostics.masterKeyEnvironmentVariableWasConfigured
              ? "warning"
              : "success"
          }
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.envWasConfigured", {
            defaultValue: "Environment variable configured",
          })}
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
          label={t("securityDiagnostics.fields.envPresent", {
            defaultValue: "Environment variable still present",
          })}
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
        title={t("securityDiagnostics.sections.memory", {
          defaultValue: "Process memory exposure",
        })}
      >
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.dotnetDiagnostics", {
            defaultValue: ".NET diagnostics disabled",
          })}
          value={yesNo(diagnostics.dotNetDiagnostics.disabled, t)}
          color={diagnostics.dotNetDiagnostics.disabled ? "success" : "warning"}
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.processHardening", {
            defaultValue: "Process hardening applied",
          })}
          value={yesNo(diagnostics.linuxProcess.hardeningApplied, t)}
          color={
            diagnostics.linuxProcess.hardeningApplied ? "success" : "warning"
          }
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.dumpable", {
            defaultValue: "Dumpable state",
          })}
          value={getDumpableLabel(diagnostics.linuxProcess, t)}
          color={diagnostics.linuxProcess.dumpable === 0 ? "success" : "warning"}
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.sysPtrace", {
            defaultValue: "CAP_SYS_PTRACE",
          })}
          value={yesNo(diagnostics.linuxProcess.hasSysPtraceCapability, t)}
          color={
            diagnostics.linuxProcess.hasSysPtraceCapability
              ? "error"
              : "success"
          }
        />
      </DiagnosticsSection>

      <DiagnosticsSection
        title={t("securityDiagnostics.sections.runtime", {
          defaultValue: "Runtime facts",
        })}
      >
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.os", {
            defaultValue: "Operating system",
          })}
          value={diagnostics.operatingSystem}
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.container", {
            defaultValue: "Container detected",
          })}
          value={yesNo(diagnostics.isContainer, t)}
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.euid", {
            defaultValue: "Effective UID",
          })}
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
          label={t("securityDiagnostics.fields.noNewPrivileges", {
            defaultValue: "No new privileges",
          })}
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
          label={t("securityDiagnostics.fields.seccomp", {
            defaultValue: "Seccomp mode",
          })}
          value={formatNullable(diagnostics.linuxProcess.seccompMode, t)}
          color={
            diagnostics.linuxProcess.seccompMode === 0 ? "warning" : "success"
          }
        />
        <DiagnosticsRow
          label={t("securityDiagnostics.fields.capabilities", {
            defaultValue: "Effective capabilities",
          })}
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
      t("securityDiagnostics.errors.loadFailed", {
        defaultValue: "Failed to load security diagnostics.",
      })
    : null;

  return (
    <Stack>
      <AdminPageSurface>
        <Stack p={3} spacing={3} divider={<Divider flexItem />}>
          <Stack direction="row" spacing={1.5} alignItems="center">
            <SecurityIcon color="primary" />
            <Stack spacing={0.5}>
              <Typography variant="h5" fontWeight={700}>
                {t("securityDiagnostics.title", {
                  defaultValue: "Security checkup",
                })}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {t("securityDiagnostics.description", {
                  defaultValue:
                    "Server-side checks for account hygiene, master-key handling, and process-memory exposure.",
                })}
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
