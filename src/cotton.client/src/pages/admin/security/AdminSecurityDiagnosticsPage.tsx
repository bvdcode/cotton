import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  LinearProgress,
  Skeleton,
  Stack,
  TextField,
  Typography,
  type AlertColor,
} from "@mui/material";
import AutorenewIcon from "@mui/icons-material/Autorenew";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import KeyIcon from "@mui/icons-material/VpnKey";
import SecurityIcon from "@mui/icons-material/Security";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { useTranslation } from "react-i18next";
import type { TFunction } from "i18next";
import {
  useRef,
  useState,
  type ChangeEvent,
  type FormEvent,
  type ReactNode,
} from "react";
import { getApiErrorMessage } from "../../../shared/api/httpClient";
import {
  useCreateKeyringRecoverySlotMutation,
  useExportKeyringRecoveryKitMutation,
  useImportKeyringRecoveryKitMutation,
  useReencryptKeyringChunksMutation,
  useRotateKeyringUnlockMutation,
  useSecurityDiagnosticsQuery,
} from "../../../shared/api/queries/admin";
import type {
  KeyringRecoveryKitDto,
  LinuxProcessSecurityDto,
  SecurityDiagnosticWarningDto,
  SecurityDiagnosticsDto,
} from "../../../shared/api/adminApi";
import { AdminPageSurface } from "../components/AdminPageSurface";
import { toast } from "../../../shared/ui/notifications";
import {
  generateRecoveryPhrase,
  recoveryPhraseToKdfSecret,
} from "../../../shared/crypto/recoveryKey";

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
  "keyring-v2-disabled",
  "keyring-diagnostics-failed",
  "keyring-not-loaded",
  "keyring-replicas-incomplete",
  "keyring-legacy-debt",
  "keyring-recovery-missing",
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
): string | null =>
  knownThreatVectorCodes.has(warning.code)
    ? t(`securityDiagnostics.threatVectors.${warning.code}`)
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
  onRotateUnlock: () => void;
  onCreateRecoveryPhrase: () => void;
  onExportRecoveryKit: () => void;
  onImportRecoveryKit: () => void;
  onReencryptChunks: () => void;
  exportingRecoveryKit: boolean;
  importingRecoveryKit: boolean;
  reencryptingChunks: boolean;
}

const SecurityDiagnosticsContent = ({
  diagnostics,
  t,
  onRotateUnlock,
  onCreateRecoveryPhrase,
  onExportRecoveryKit,
  onImportRecoveryKit,
  onReencryptChunks,
  exportingRecoveryKit,
  importingRecoveryKit,
  reencryptingChunks,
}: SecurityDiagnosticsContentProps) => (
  <Stack spacing={3} divider={<Divider flexItem />}>
    <SecurityScoreSummary diagnostics={diagnostics} t={t} />
    <SecurityRiskSection warnings={diagnostics.warnings} t={t} />
    <InstanceDiagnosticsSection diagnostics={diagnostics} t={t} />
    <MasterKeyDiagnosticsSection diagnostics={diagnostics} t={t} />
    <KeyringDiagnosticsSection
      diagnostics={diagnostics}
      t={t}
      onRotateUnlock={onRotateUnlock}
      onCreateRecoveryPhrase={onCreateRecoveryPhrase}
      onExportRecoveryKit={onExportRecoveryKit}
      onImportRecoveryKit={onImportRecoveryKit}
      onReencryptChunks={onReencryptChunks}
      exportingRecoveryKit={exportingRecoveryKit}
      importingRecoveryKit={importingRecoveryKit}
      reencryptingChunks={reencryptingChunks}
    />
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
      <Box>
        <LinearProgress
          variant="determinate"
          value={Math.max(0, Math.min(100, scorePercent))}
          color={level.color}
          sx={{ height: 8, borderRadius: 1 }}
        />
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
    <Chip
      size="small"
      color={
        diagnostics.keyring.loaded
          ? "success"
          : diagnostics.keyring.enabled
            ? "warning"
            : "default"
      }
      label={
        diagnostics.keyring.loaded
          ? t("securityDiagnostics.chips.keyringLoaded")
          : diagnostics.keyring.enabled
            ? t("securityDiagnostics.chips.keyringEnabled")
            : t("securityDiagnostics.chips.keyringDisabled")
      }
    />
  </Stack>
);

type SecurityRiskSectionProps = {
  warnings: SecurityDiagnosticWarningDto[];
  t: TFunction<"admin">;
};

const SecurityRiskSection = ({ warnings, t }: SecurityRiskSectionProps) => (
  <DiagnosticsSection title={t("securityDiagnostics.sections.risks")}>
    {warnings.length > 0 ? (
      warnings.map((warning) => (
        <SecurityRiskAlert key={warning.code} warning={warning} t={t} />
      ))
    ) : (
      <Alert severity="success">{t("securityDiagnostics.risks.empty")}</Alert>
    )}
  </DiagnosticsSection>
);

type SecurityRiskAlertProps = {
  warning: SecurityDiagnosticWarningDto;
  t: TFunction<"admin">;
};

const SecurityRiskAlert = ({ warning, t }: SecurityRiskAlertProps) => {
  const threatVector = getThreatVector(warning, t);

  return (
    <Alert severity={getSeverityColor(warning)} icon={<WarningAmberIcon />}>
      <Stack spacing={0.5}>
        <Stack direction="row" spacing={1} alignItems="center" useFlexGap sx={{ flexWrap: "wrap" }}>
          <Typography variant="subtitle2" fontWeight={700}>
            {getSeverityLabel(warning.severity, t)}
          </Typography>
          <Chip size="small" variant="outlined" label={warning.code} />
        </Stack>
        <Typography variant="body2">{warning.message}</Typography>
        {threatVector && (
          <Typography variant="body2" color="text.secondary">
            {threatVector}
          </Typography>
        )}
      </Stack>
    </Alert>
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

type KeyringDiagnosticsSectionProps = DiagnosticsContentSectionProps & {
  onRotateUnlock: () => void;
  onCreateRecoveryPhrase: () => void;
  onExportRecoveryKit: () => void;
  onImportRecoveryKit: () => void;
  onReencryptChunks: () => void;
  exportingRecoveryKit: boolean;
  importingRecoveryKit: boolean;
  reencryptingChunks: boolean;
};

const KeyringDiagnosticsSection = ({
  diagnostics,
  t,
  onRotateUnlock,
  onCreateRecoveryPhrase,
  onExportRecoveryKit,
  onImportRecoveryKit,
  onReencryptChunks,
  exportingRecoveryKit,
  importingRecoveryKit,
  reencryptingChunks,
}: KeyringDiagnosticsSectionProps) => (
  <DiagnosticsSection title={t("securityDiagnostics.sections.keyring")}>
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.keyringEnabled")}
      value={yesNo(diagnostics.keyring.enabled, t)}
      color={diagnostics.keyring.enabled ? "success" : "default"}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.keyringLoaded")}
      value={yesNo(diagnostics.keyring.loaded, t)}
      color={
        diagnostics.keyring.loaded
          ? "success"
          : diagnostics.keyring.enabled
            ? "error"
            : "default"
      }
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.keyringAccessEnvelope")}
      value={yesNo(diagnostics.keyring.accessEnvelopePresent, t)}
      color={
        diagnostics.keyring.accessEnvelopePresent
          ? "success"
          : diagnostics.keyring.enabled
            ? "error"
            : "default"
      }
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.keyringStateSnapshot")}
      value={yesNo(diagnostics.keyring.stateSnapshotPresent, t)}
      color={
        diagnostics.keyring.stateSnapshotPresent
          ? "success"
          : diagnostics.keyring.enabled
            ? "error"
            : "default"
      }
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.keyringRootEpoch")}
      value={formatNullable(diagnostics.keyring.rootEpoch, t)}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.keyringAccessGeneration")}
      value={formatNullable(diagnostics.keyring.accessGeneration, t)}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.keyringStateGeneration")}
      value={formatNullable(diagnostics.keyring.stateGeneration, t)}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.keyringKeys")}
      value={formatNullable(diagnostics.keyring.keyCount, t)}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.keyringRecoverySlots")}
      value={formatNullable(diagnostics.keyring.recoverySlotCount, t)}
      color={
        diagnostics.keyring.recoverySlotCount === null
          ? "default"
          : diagnostics.keyring.recoverySlotCount > 0
            ? "success"
            : "warning"
      }
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.keyringLegacyKeys")}
      value={formatNullable(diagnostics.keyring.legacyDecryptOnlyKeyCount, t)}
      color={
        diagnostics.keyring.legacyDecryptOnlyKeyCount &&
        diagnostics.keyring.legacyDecryptOnlyKeyCount > 0
          ? "warning"
          : "success"
      }
    />
    {diagnostics.keyring.enabled && diagnostics.keyring.loaded && (
      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
        <Button
          variant="outlined"
          size="small"
          startIcon={<KeyIcon fontSize="small" />}
          onClick={onRotateUnlock}
        >
          {t("securityDiagnostics.actions.rotateUnlock")}
        </Button>
        <Button
          variant="outlined"
          size="small"
          startIcon={<AutorenewIcon fontSize="small" />}
          onClick={onReencryptChunks}
          disabled={reencryptingChunks}
        >
          {reencryptingChunks
            ? t("securityDiagnostics.actions.reencryptingChunks")
            : t("securityDiagnostics.actions.reencryptChunks")}
        </Button>
        <Button
          variant="outlined"
          size="small"
          startIcon={<KeyIcon fontSize="small" />}
          onClick={onCreateRecoveryPhrase}
        >
          {t("securityDiagnostics.actions.createRecoveryPhrase")}
        </Button>
        <Button
          variant="outlined"
          size="small"
          startIcon={<FileDownloadIcon fontSize="small" />}
          onClick={onExportRecoveryKit}
          disabled={exportingRecoveryKit}
        >
          {exportingRecoveryKit
            ? t("securityDiagnostics.actions.exportingRecoveryKit")
            : t("securityDiagnostics.actions.exportRecoveryKit")}
        </Button>
        <Button
          variant="outlined"
          size="small"
          startIcon={<UploadFileIcon fontSize="small" />}
          onClick={onImportRecoveryKit}
          disabled={importingRecoveryKit}
        >
          {importingRecoveryKit
            ? t("securityDiagnostics.actions.importingRecoveryKit")
            : t("securityDiagnostics.actions.importRecoveryKit")}
        </Button>
      </Stack>
    )}
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

const createRecoveryKitFilename = (kit: KeyringRecoveryKitDto) => {
  const instanceId = kit.instanceId.replace(/[^a-zA-Z0-9-]/g, "");
  const exportedAt = kit.exportedAtUtc.replace(/[^0-9T]/g, "").slice(0, 15);
  return `cotton-keyring-kit-${instanceId || "instance"}-g${kit.stateGeneration}-${exportedAt || "export"}.json`;
};

const downloadRecoveryKit = (kit: KeyringRecoveryKitDto) => {
  const blob = new Blob([JSON.stringify(kit, null, 2)], {
    type: "application/json",
  });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = createRecoveryKitFilename(kit);
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
};

const generateUnlockSecret = () => {
  const bytes = new Uint8Array(24);
  globalThis.crypto.getRandomValues(bytes);
  let binary = "";
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte);
  });
  return btoa(binary);
};

type KeyringRotateUnlockDialogProps = {
  open: boolean;
  onClose: () => void;
  onRotated: () => void;
};

const KeyringRotateUnlockDialog = ({
  open,
  onClose,
  onRotated,
}: KeyringRotateUnlockDialogProps) => {
  const { t } = useTranslation("admin");
  const rotateMutation = useRotateKeyringUnlockMutation();
  const [currentUnlockSecret, setCurrentUnlockSecret] = useState("");
  const [newUnlockSecret, setNewUnlockSecret] = useState("");
  const [confirmUnlockSecret, setConfirmUnlockSecret] = useState("");
  const [error, setError] = useState<string | null>(null);
  const rotating = rotateMutation.isPending;
  const canSubmit =
    currentUnlockSecret.trim().length > 0 &&
    newUnlockSecret.trim().length > 0 &&
    confirmUnlockSecret.trim().length > 0 &&
    !rotating;

  const resetAndClose = () => {
    if (rotating) {
      return;
    }

    setCurrentUnlockSecret("");
    setNewUnlockSecret("");
    setConfirmUnlockSecret("");
    setError(null);
    onClose();
  };

  const handleGenerate = () => {
    const generated = generateUnlockSecret();
    setNewUnlockSecret(generated);
    setConfirmUnlockSecret(generated);
    setError(null);
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const current = currentUnlockSecret.trim();
    const next = newUnlockSecret.trim();
    const confirmation = confirmUnlockSecret.trim();
    if (!current || !next || !confirmation) {
      setError(t("securityDiagnostics.rotateUnlock.errors.required"));
      return;
    }

    if (next !== confirmation) {
      setError(t("securityDiagnostics.rotateUnlock.errors.mismatch"));
      return;
    }

    if (current === next) {
      setError(t("securityDiagnostics.rotateUnlock.errors.same"));
      return;
    }

    try {
      const result = await rotateMutation.mutateAsync({
        currentUnlockSecret: current,
        newUnlockSecret: next,
      });
      toast.success(
        t("securityDiagnostics.rotateUnlock.success", {
          rootEpoch: result.rootEpoch,
        }),
        { toastId: "admin:keyring:rotate-unlock:success" },
      );
      resetAndClose();
      onRotated();
    } catch (apiError) {
      setError(
        getApiErrorMessage(apiError) ??
          t("securityDiagnostics.rotateUnlock.errors.failed"),
      );
    }
  };

  return (
    <Dialog open={open} onClose={resetAndClose} maxWidth="xs" fullWidth>
      <DialogTitle>{t("securityDiagnostics.rotateUnlock.title")}</DialogTitle>
      <DialogContent dividers>
        <Box component="form" id="keyring-rotate-unlock-form" onSubmit={handleSubmit}>
          <Stack spacing={2} pt={0.5}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              label={t("securityDiagnostics.rotateUnlock.current")}
              type="password"
              value={currentUnlockSecret}
              onChange={(event) => setCurrentUnlockSecret(event.target.value)}
              autoComplete="current-password"
              disabled={rotating}
              fullWidth
            />
            <TextField
              label={t("securityDiagnostics.rotateUnlock.next")}
              type="password"
              value={newUnlockSecret}
              onChange={(event) => setNewUnlockSecret(event.target.value)}
              autoComplete="new-password"
              disabled={rotating}
              fullWidth
            />
            <TextField
              label={t("securityDiagnostics.rotateUnlock.confirm")}
              type="password"
              value={confirmUnlockSecret}
              onChange={(event) => setConfirmUnlockSecret(event.target.value)}
              autoComplete="new-password"
              disabled={rotating}
              fullWidth
            />
            <Button
              variant="outlined"
              size="small"
              startIcon={<KeyIcon fontSize="small" />}
              onClick={handleGenerate}
              disabled={rotating}
              sx={{ alignSelf: "flex-start" }}
            >
              {t("securityDiagnostics.rotateUnlock.generate")}
            </Button>
          </Stack>
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={resetAndClose} disabled={rotating}>
          {t("securityDiagnostics.rotateUnlock.cancel")}
        </Button>
        <Button
          type="submit"
          form="keyring-rotate-unlock-form"
          variant="contained"
          disabled={!canSubmit}
        >
          {rotating
            ? t("securityDiagnostics.rotateUnlock.saving")
            : t("securityDiagnostics.rotateUnlock.submit")}
        </Button>
      </DialogActions>
    </Dialog>
  );
};

type KeyringRecoveryPhraseDialogProps = {
  open: boolean;
  onClose: () => void;
};

const KeyringRecoveryPhraseDialog = ({
  open,
  onClose,
}: KeyringRecoveryPhraseDialogProps) => {
  const { t } = useTranslation("admin");
  const createMutation = useCreateKeyringRecoverySlotMutation();
  const [currentUnlockSecret, setCurrentUnlockSecret] = useState("");
  const [recoveryPhrase, setRecoveryPhrase] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const creating = createMutation.isPending;

  const resetAndClose = () => {
    if (creating) {
      return;
    }

    setCurrentUnlockSecret("");
    setRecoveryPhrase(null);
    setError(null);
    onClose();
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const current = currentUnlockSecret.trim();
    if (!current) {
      setError(t("securityDiagnostics.recoveryPhrase.errors.required"));
      return;
    }

    try {
      const phrase = generateRecoveryPhrase();
      const result = await createMutation.mutateAsync({
        currentUnlockSecret: current,
        recoverySecret: recoveryPhraseToKdfSecret(phrase),
      });
      setRecoveryPhrase(phrase);
      setError(null);
      downloadRecoveryKit(result.recoveryKit);
      toast.success(
        t("securityDiagnostics.recoveryPhrase.success", {
          accessGeneration: result.accessGeneration,
        }),
        { toastId: "admin:keyring:recovery-phrase:success" },
      );
    } catch (apiError) {
      setError(
        getApiErrorMessage(apiError) ??
          t("securityDiagnostics.recoveryPhrase.errors.failed"),
      );
    }
  };

  return (
    <Dialog open={open} onClose={resetAndClose} maxWidth="sm" fullWidth>
      <DialogTitle>{t("securityDiagnostics.recoveryPhrase.title")}</DialogTitle>
      <DialogContent dividers>
        <Box component="form" id="keyring-recovery-phrase-form" onSubmit={handleSubmit}>
          <Stack spacing={2} pt={0.5}>
            {error && <Alert severity="error">{error}</Alert>}
            {recoveryPhrase && (
              <Alert severity="success">
                {t("securityDiagnostics.recoveryPhrase.created")}
              </Alert>
            )}
            <TextField
              label={t("securityDiagnostics.recoveryPhrase.current")}
              type="password"
              value={currentUnlockSecret}
              onChange={(event) => setCurrentUnlockSecret(event.target.value)}
              autoComplete="current-password"
              disabled={creating || recoveryPhrase !== null}
              fullWidth
            />
            {recoveryPhrase && (
              <TextField
                label={t("securityDiagnostics.recoveryPhrase.phrase")}
                value={recoveryPhrase}
                multiline
                minRows={4}
                slotProps={{ input: { readOnly: true } }}
                fullWidth
              />
            )}
          </Stack>
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={resetAndClose} disabled={creating}>
          {recoveryPhrase
            ? t("securityDiagnostics.recoveryPhrase.close")
            : t("securityDiagnostics.recoveryPhrase.cancel")}
        </Button>
        {!recoveryPhrase && (
          <Button
            type="submit"
            form="keyring-recovery-phrase-form"
            variant="contained"
            disabled={creating || currentUnlockSecret.trim().length === 0}
          >
            {creating
              ? t("securityDiagnostics.recoveryPhrase.creating")
              : t("securityDiagnostics.recoveryPhrase.create")}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
};

export const AdminSecurityDiagnosticsPage = () => {
  const { t } = useTranslation("admin");
  const diagnosticsQuery = useSecurityDiagnosticsQuery();
  const exportRecoveryKitMutation = useExportKeyringRecoveryKitMutation();
  const importRecoveryKitMutation = useImportKeyringRecoveryKitMutation();
  const reencryptChunksMutation = useReencryptKeyringChunksMutation();
  const recoveryKitInputRef = useRef<HTMLInputElement | null>(null);
  const [rotationOpen, setRotationOpen] = useState(false);
  const [recoveryPhraseOpen, setRecoveryPhraseOpen] = useState(false);
  const loadError = diagnosticsQuery.isError
    ? getApiErrorMessage(diagnosticsQuery.error) ??
      t("securityDiagnostics.errors.loadFailed")
    : null;

  const handleRotationCompleted = () => {
    toast.warning(t("securityDiagnostics.rotateUnlock.recoveryReminder"), {
      toastId: "admin:keyring:rotate-unlock:recovery-reminder",
      autoClose: false,
      action: (snackbarId) => (
        <Button
          color="inherit"
          size="small"
          onClick={() => {
            toast.dismiss(snackbarId);
            setRecoveryPhraseOpen(true);
          }}
        >
          {t("securityDiagnostics.rotateUnlock.createRecoveryAction")}
        </Button>
      ),
    });
  };

  const handleExportRecoveryKit = async () => {
    try {
      const kit = await exportRecoveryKitMutation.mutateAsync();
      downloadRecoveryKit(kit);
      toast.success(t("securityDiagnostics.recoveryKit.success"), {
        toastId: "admin:keyring:recovery-kit:success",
      });
    } catch (apiError) {
      toast.error(
        getApiErrorMessage(apiError) ??
          t("securityDiagnostics.recoveryKit.errors.failed"),
        { toastId: "admin:keyring:recovery-kit:failed" },
      );
    }
  };

  const handleImportRecoveryKit = () => {
    recoveryKitInputRef.current?.click();
  };

  const handleReencryptChunks = async () => {
    const limit = 100;
    let offset = 0;
    let reencrypted = 0;
    let scanned = 0;
    let failed = 0;
    let missing = 0;

    try {
      while (true) {
        const result = await reencryptChunksMutation.mutateAsync({ offset, limit });
        reencrypted += result.reencrypted;
        scanned += result.scanned;
        failed += result.failed;
        missing += result.missing;
        offset = result.nextOffset;

        if (result.completed) {
          break;
        }
      }

      const toastType = failed > 0 || missing > 0 ? toast.warning : toast.success;
      toastType(
        t("securityDiagnostics.reencryptChunks.success", {
          reencrypted,
          scanned,
          failed,
          missing,
        }),
        { toastId: "admin:keyring:reencrypt-chunks:success" },
      );
    } catch (apiError) {
      toast.error(
        getApiErrorMessage(apiError) ??
          t("securityDiagnostics.reencryptChunks.errors.failed"),
        { toastId: "admin:keyring:reencrypt-chunks:failed" },
      );
    }
  };

  const handleRecoveryKitFileSelected = async (
    event: ChangeEvent<HTMLInputElement>,
  ) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) {
      return;
    }

    try {
      const kit = JSON.parse(await file.text()) as KeyringRecoveryKitDto;
      const result = await importRecoveryKitMutation.mutateAsync(kit);
      toast.success(
        t("securityDiagnostics.recoveryKit.importSuccess", {
          stateGeneration: result.stateGeneration,
        }),
        { toastId: "admin:keyring:recovery-kit:import-success" },
      );
    } catch (apiError) {
      toast.error(
        getApiErrorMessage(apiError) ??
          t("securityDiagnostics.recoveryKit.errors.importFailed"),
        { toastId: "admin:keyring:recovery-kit:import-failed" },
      );
    }
  };

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
              onRotateUnlock={() => setRotationOpen(true)}
              onCreateRecoveryPhrase={() => setRecoveryPhraseOpen(true)}
              onExportRecoveryKit={handleExportRecoveryKit}
              onImportRecoveryKit={handleImportRecoveryKit}
              onReencryptChunks={handleReencryptChunks}
              exportingRecoveryKit={exportRecoveryKitMutation.isPending}
              importingRecoveryKit={importRecoveryKitMutation.isPending}
              reencryptingChunks={reencryptChunksMutation.isPending}
            />
          )}
        </Stack>
      </AdminPageSurface>
      <input
        ref={recoveryKitInputRef}
        type="file"
        accept="application/json,.json"
        hidden
        onChange={handleRecoveryKitFileSelected}
      />
      <KeyringRecoveryPhraseDialog
        open={recoveryPhraseOpen}
        onClose={() => setRecoveryPhraseOpen(false)}
      />
      <KeyringRotateUnlockDialog
        open={rotationOpen}
        onClose={() => setRotationOpen(false)}
        onRotated={handleRotationCompleted}
      />
    </Stack>
  );
};
