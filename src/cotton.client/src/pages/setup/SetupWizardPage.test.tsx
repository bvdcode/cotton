import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SetupWizardPage } from "./SetupWizardPage";

const testState = vi.hoisted(() => ({
  stepKey: "telemetry",
  setupInitialized: false as boolean | null,
  getTelemetry: vi.fn(),
  saveSetupStep: vi.fn(),
  navigate: vi.fn(),
  fetchSetupStatus: vi.fn(),
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

vi.mock("react-router-dom", () => ({
  useNavigate: () => testState.navigate,
}));

vi.mock("../../features/auth/useAuth", () => ({
  useAuth: () => ({
    user: { role: 2 },
  }),
}));

vi.mock("../../shared/api/httpClient", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../shared/api/httpClient")>()),
  showApiErrorToast: vi.fn(),
}));

vi.mock("../../shared/api/settingsApi", () => ({
  settingsApi: {
    getTelemetry: testState.getTelemetry,
    saveSetupStep: testState.saveSetupStep,
  },
}));

vi.mock("../../shared/store/setupStatusStore", () => {
  const useSetupStatusStore = ((
    selector: (state: { isInitialized: boolean | null }) => unknown,
  ) =>
    selector({
      isInitialized: testState.setupInitialized,
    })) as typeof import("../../shared/store/setupStatusStore").useSetupStatusStore;

  useSetupStatusStore.getState = () => ({
    isInitialized: testState.setupInitialized,
    loading: false,
    loaded: true,
    error: null,
    fetchSetupStatus: testState.fetchSetupStatus,
    reset: vi.fn(),
  });

  return { useSetupStatusStore };
});

vi.mock("./components", () => ({
  WizardHeader: () => <div>header</div>,
  WizardProgressBar: () => <div>progress</div>,
  FloatingBlobs: () => null,
}));

vi.mock("./useSetupSteps.tsx", () => ({
  useSetupSteps: () => [
    {
      key: testState.stepKey,
      render: () => <div>telemetry step</div>,
      isValid: () => testState.stepKey === "remoteComputationRunnerUrl",
    },
  ],
}));

describe("SetupWizardPage", () => {
  beforeEach(() => {
    testState.stepKey = "telemetry";
    testState.setupInitialized = false;
    testState.getTelemetry.mockReset();
    testState.getTelemetry.mockResolvedValue(false);
    testState.saveSetupStep.mockReset();
    testState.navigate.mockReset();
    testState.fetchSetupStatus.mockReset();
  });

  it("does not prefill setup answers during first server setup", async () => {
    render(<SetupWizardPage />);

    fireEvent.click(screen.getByRole("button", { name: "actions.start" }));

    screen.getByText("telemetry step");
    await Promise.resolve();

    expect(testState.getTelemetry).not.toHaveBeenCalled();
  });

  it("prefills setup answers when the setup wizard is reopened after initialization", async () => {
    testState.setupInitialized = true;

    render(<SetupWizardPage />);

    fireEvent.click(screen.getByRole("button", { name: "actions.start" }));

    await waitFor(() =>
      expect(testState.getTelemetry).toHaveBeenCalledTimes(1),
    );
  });

  it("stays on runner setup when server validation rejects the URL", async () => {
    testState.stepKey = "remoteComputationRunnerUrl";
    testState.saveSetupStep.mockRejectedValueOnce(
      Object.assign(new Error("invalid"), {
        isAxiosError: true,
        response: { data: { code: "InvalidDimensions" } },
      }),
    );
    render(<SetupWizardPage />);
    fireEvent.click(screen.getByRole("button", { name: "actions.start" }));
    fireEvent.click(
      screen.getByRole("button", {
        name: "admin:settings.general.remoteRunner.validateAndSave",
      }),
    );

    expect(
      await screen.findByText(
        "admin:settings.general.remoteRunner.errors.InvalidDimensions",
      ),
    ).toBeInTheDocument();
    expect(testState.navigate).not.toHaveBeenCalled();
    expect(testState.fetchSetupStatus).not.toHaveBeenCalled();
  });
});
