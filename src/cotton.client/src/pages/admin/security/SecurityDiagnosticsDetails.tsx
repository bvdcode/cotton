import { DIRECT_CONNECTION_IP_ADDRESS } from "../../../shared/api/settingsApi";
import {
  DiagnosticsRow,
  DiagnosticsSection,
  type DiagnosticsContentSectionProps,
} from "./SecurityDiagnosticsPrimitives";
import {
  booleanStatusColor,
  formatNullable,
  getDumpableLabel,
  getLimitSummary,
  isUnconfinedAppArmorProfile,
  yesNo,
} from "./securityDiagnosticsPresentation";

export const InstanceDiagnosticsSection = ({
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
      label={t("securityDiagnostics.trustedProxy.field")}
      value={
        diagnostics.trustedProxyIpAddress === DIRECT_CONNECTION_IP_ADDRESS
          ? t("settings.general.trustedProxy.directMode")
          : formatNullable(diagnostics.trustedProxyIpAddress, t)
      }
      color={diagnostics.trustedProxyIpAddress ? "success" : "warning"}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.admins")}
      value={
        String(diagnostics.adminTotp.adminsWithTotp) +
        "/" +
        String(diagnostics.adminTotp.adminCount)
      }
      color={
        diagnostics.adminTotp.adminsWithoutTotp > 0 ? "warning" : "success"
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
);

export const MasterKeyDiagnosticsSection = ({
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

export const MemoryDiagnosticsSection = ({
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

export const ContainerDiagnosticsSection = ({
  diagnostics,
  t,
}: DiagnosticsContentSectionProps) => (
  <DiagnosticsSection
    title={t("securityDiagnostics.sections.containerBoundary")}
  >
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
              isUnconfinedAppArmorProfile(
                diagnostics.linuxContainer.appArmorProfile,
              ),
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

export const RuntimeDiagnosticsSection = ({
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
      label={t("securityDiagnostics.fields.tempDirectoryPath")}
      value={formatNullable(diagnostics.tempDirectoryPath, t)}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.tempDirectoryWritable")}
      value={yesNo(diagnostics.tempDirectoryWritable, t)}
      color={diagnostics.tempDirectoryWritable ? "success" : "error"}
    />
    {diagnostics.tempDirectoryError && (
      <DiagnosticsRow
        label={t("securityDiagnostics.fields.tempDirectoryError")}
        value={diagnostics.tempDirectoryError}
        color="error"
      />
    )}
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.euid")}
      value={formatNullable(diagnostics.linuxProcess.effectiveUserId, t)}
      color={
        diagnostics.linuxProcess.runningAsRoot === true ? "warning" : "default"
      }
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.noNewPrivileges")}
      value={formatNullable(diagnostics.linuxProcess.noNewPrivileges, t)}
      color={
        diagnostics.linuxProcess.noNewPrivileges === 1 ? "success" : "warning"
      }
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.seccomp")}
      value={formatNullable(diagnostics.linuxProcess.seccompMode, t)}
      color={diagnostics.linuxProcess.seccompMode === 0 ? "warning" : "success"}
    />
    <DiagnosticsRow
      label={t("securityDiagnostics.fields.capabilities")}
      value={formatNullable(
        diagnostics.linuxProcess.effectiveCapabilitiesHex,
        t,
      )}
    />
  </DiagnosticsSection>
);
