import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ComputationModeSetting } from "./ComputationModeSetting";

const settingsApi = vi.hoisted(() => ({
  getComputionMode: vi.fn(),
  getRemoteComputationRunnerUrl: vi.fn(),
  setComputionMode: vi.fn(),
  setRemoteComputationRunnerUrl: vi.fn(),
}));

vi.mock("../../../shared/api/settingsApi", () => ({ settingsApi }));
vi.mock("../../../shared/api/httpClient", () => ({
  showApiErrorToast: vi.fn(),
}));
vi.mock("@shared/ui/notifications", () => ({
  toast: { error: vi.fn() },
}));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

const chooseMode = async (mode: "Local" | "Remote" | "Cloud") => {
  fireEvent.mouseDown(screen.getByRole("combobox"));
  fireEvent.click(
    await screen.findByRole("option", {
      name: `settings.general.computionMode.${mode}`,
    }),
  );
};

describe("ComputationModeSetting", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    settingsApi.getComputionMode.mockResolvedValue("Local");
    settingsApi.getRemoteComputationRunnerUrl.mockResolvedValue("");
    settingsApi.setComputionMode.mockResolvedValue(undefined);
    settingsApi.setRemoteComputationRunnerUrl.mockResolvedValue(undefined);
  });

  afterEach(() => {
    cleanup();
  });

  it("loads the saved remote runner URL", async () => {
    settingsApi.getComputionMode.mockResolvedValue("Remote");
    settingsApi.getRemoteComputationRunnerUrl.mockResolvedValue(
      "https://runner.example",
    );

    render(<ComputationModeSetting />);

    expect(
      await screen.findByRole("textbox", {
        name: "settings.general.fields.remoteComputationRunnerUrl",
      }),
    ).toHaveValue("https://runner.example");
  });

  it("requires a URL before enabling remote mode", async () => {
    render(<ComputationModeSetting />);
    await waitFor(() => expect(screen.getByRole("combobox")).toBeEnabled());

    await chooseMode("Remote");

    expect(
      await screen.findByText("settings.general.validation.required"),
    ).toBeInTheDocument();
    expect(settingsApi.setComputionMode).not.toHaveBeenCalled();
    expect(settingsApi.setRemoteComputationRunnerUrl).not.toHaveBeenCalled();
  });

  it("saves the URL before enabling remote mode", async () => {
    render(<ComputationModeSetting />);
    await waitFor(() => expect(screen.getByRole("combobox")).toBeEnabled());
    await chooseMode("Remote");

    const input = await screen.findByRole("textbox", {
      name: "settings.general.fields.remoteComputationRunnerUrl",
    });
    fireEvent.change(input, { target: { value: " https://runner.example/ " } });
    fireEvent.blur(input);

    await waitFor(() =>
      expect(settingsApi.setRemoteComputationRunnerUrl).toHaveBeenCalledWith(
        "https://runner.example",
      ),
    );
    expect(settingsApi.setComputionMode).toHaveBeenCalledWith("Remote");
    expect(
      settingsApi.setRemoteComputationRunnerUrl.mock.invocationCallOrder[0],
    ).toBeLessThan(settingsApi.setComputionMode.mock.invocationCallOrder[0]);
  });

  it("saves a non-remote mode immediately", async () => {
    settingsApi.getComputionMode.mockResolvedValue("Remote");
    settingsApi.getRemoteComputationRunnerUrl.mockResolvedValue(
      "https://runner.example",
    );
    render(<ComputationModeSetting />);
    await waitFor(() => expect(screen.getByRole("combobox")).toBeEnabled());

    await chooseMode("Local");

    await waitFor(() =>
      expect(settingsApi.setComputionMode).toHaveBeenCalledWith("Local"),
    );
    expect(settingsApi.setRemoteComputationRunnerUrl).not.toHaveBeenCalled();
  });
});
