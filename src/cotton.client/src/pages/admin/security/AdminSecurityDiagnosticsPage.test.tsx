import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type {
  CpuFeatureAvailabilityDto,
  SecurityDiagnosticsDto,
} from "../../../shared/api/adminApi";
import { AdminSecurityDiagnosticsPage } from "./AdminSecurityDiagnosticsPage";

interface SecurityQueryState {
  isPending: boolean;
  isError: boolean;
  error: Error | null;
  data: SecurityDiagnosticsDto | undefined;
}

const queryState = vi.hoisted<{ current: SecurityQueryState }>(() => ({
  current: {
    isPending: true,
    isError: false,
    error: null,
    data: undefined,
  },
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

vi.mock("../../../shared/api/httpClient", () => ({
  getApiErrorMessage: (error: Error) => error.message,
}));

vi.mock("../../../shared/api/queries/admin", () => ({
  useSecurityDiagnosticsQuery: () => queryState.current,
}));

const availableFeature: CpuFeatureAvailabilityDto = {
  runtimeSupported: true,
  linuxFlagPresent: true,
};

const diagnostics: SecurityDiagnosticsDto = {
  operatingSystem: "Linux",
  isLinux: true,
  isContainer: true,
  isPublicInstance: false,
  trustedProxyIpAddress: "127.0.0.1",
  securityScore: 10,
  maxSecurityScore: 10,
  masterKeySource: "memory",
  masterKeyEnvironmentVariableWasConfigured: false,
  masterKeyEnvironmentVariablePresentInProcess: false,
  tempDirectoryPath: "/tmp",
  tempDirectoryWritable: true,
  tempDirectoryError: null,
  dotNetDiagnostics: {
    disabled: true,
    dotNetEnableDiagnostics: "0",
    comPlusEnableDiagnostics: null,
  },
  linuxProcess: {
    hardeningRequested: true,
    hardeningApplied: true,
    hardeningError: null,
    dumpable: 0,
    effectiveUserId: 1000,
    runningAsRoot: false,
    noNewPrivileges: 1,
    seccompMode: 2,
    seccompFilters: 1,
    effectiveCapabilitiesHex: "0000000000000000",
    hasSysPtraceCapability: false,
  },
  linuxContainer: {
    rootFilesystemReadOnly: true,
    dockerSocketMounted: false,
    hostPidNamespaceLikely: false,
    procOneCommandLine: "dotnet Cotton.Server.dll",
    coreDumpSoftLimit: "0",
    coreDumpHardLimit: "0",
    coreDumpSoftLimitDisabled: true,
    corePattern: "core",
    appArmorProfile: "docker-default",
    selinuxContext: null,
    selinuxEnforcing: null,
  },
  adminTotp: {
    adminCount: 1,
    adminsWithTotp: 1,
    adminsWithoutTotp: 0,
  },
  databaseIntegrity: {
    enabled: true,
    protectedEntityTypes: 4,
    unsignedProtectedRows: 0,
  },
  cpuFeatures: {
    architecture: "X64",
    osArchitecture: "X64",
    logicalProcessorCount: 8,
    vendorId: "GenuineIntel",
    modelName: "Test CPU",
    aesGcmHardwareAccelerationLikely: true,
    aesNi: availableFeature,
    pclmulqdq: availableFeature,
    vaes: availableFeature,
    vpclmulqdq: availableFeature,
    avx2: availableFeature,
    tme: availableFeature,
    tmeMk: availableFeature,
    pconfig: availableFeature,
    linuxCpuFlags: ["aes", "pclmulqdq"],
  },
  warnings: [
    {
      code: "informational-test",
      severity: "info",
      message: "Informational warning",
    },
    {
      code: "running-as-root",
      severity: "critical",
      message: "Critical warning",
    },
  ],
};

describe("AdminSecurityDiagnosticsPage", () => {
  beforeEach(() => {
    queryState.current = {
      isPending: true,
      isError: false,
      error: null,
      data: undefined,
    };
  });

  it("renders the page shell and loading placeholders", () => {
    const { container } = render(<AdminSecurityDiagnosticsPage />);

    expect(screen.getByText("securityDiagnostics.title")).toBeInTheDocument();
    expect(container.querySelectorAll(".MuiSkeleton-root")).toHaveLength(3);
  });

  it("renders the API error", () => {
    queryState.current = {
      isPending: false,
      isError: true,
      error: new Error("Diagnostics unavailable"),
      data: undefined,
    };

    render(<AdminSecurityDiagnosticsPage />);

    expect(screen.getByText("Diagnostics unavailable")).toBeInTheDocument();
  });

  it("renders the diagnostics sections and orders risks by severity", () => {
    queryState.current = {
      isPending: false,
      isError: false,
      error: null,
      data: diagnostics,
    };

    render(<AdminSecurityDiagnosticsPage />);

    expect(screen.getByText("10 / 10")).toBeInTheDocument();
    expect(
      screen.getByText("securityDiagnostics.sections.risks"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("securityDiagnostics.sections.instance"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("securityDiagnostics.sections.runtime"),
    ).toBeInTheDocument();

    const criticalWarning = screen.getByText("Critical warning");
    const informationalWarning = screen.getByText("Informational warning");
    expect(
      criticalWarning.compareDocumentPosition(informationalWarning) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).not.toBe(0);
  });
});
