import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ComputationModeSetting } from "./ComputationModeSetting";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { readyComputationStatus } from "../../../test/computationStatus";

const settingsApi = vi.hoisted(() => ({
  getComputionMode: vi.fn(),
  getRemoteComputationRunnerUrl: vi.fn(),
  getComputationStatus: vi.fn(),
  setComputionMode: vi.fn(),
  setRemoteComputationRunnerUrl: vi.fn(),
}));

vi.mock("../../../shared/api/settingsApi", () => ({ settingsApi }));
vi.mock("@shared/ui/notifications", () => ({
  toast: { error: vi.fn() },
}));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

const chooseMode = async (mode: "Local" | "Remote" | "Cloud") => {
  await waitFor(() =>
    expect(screen.getByRole("combobox")).not.toHaveAttribute(
      "aria-disabled",
      "true",
    ),
  );
  fireEvent.mouseDown(screen.getByRole("combobox"));
  fireEvent.click(
    await screen.findByRole("option", {
      name: `settings.general.computionMode.${mode}`,
    }),
  );
};

const renderSetting = () =>
  render(
    <QueryClientProvider
      client={
        new QueryClient({
          defaultOptions: { queries: { retry: false, gcTime: 0 } },
        })
      }
    >
      <ComputationModeSetting />
    </QueryClientProvider>,
  );

describe("ComputationModeSetting", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    settingsApi.getComputionMode.mockResolvedValue("Local");
    settingsApi.getRemoteComputationRunnerUrl.mockResolvedValue("");
    settingsApi.getComputationStatus.mockResolvedValue(readyComputationStatus);
    settingsApi.setComputionMode.mockResolvedValue(undefined);
    settingsApi.setRemoteComputationRunnerUrl.mockResolvedValue(
      readyComputationStatus,
    );
  });

  afterEach(() => {
    cleanup();
  });

  it("loads the saved remote runner URL", async () => {
    settingsApi.getComputionMode.mockResolvedValue("Remote");
    settingsApi.getRemoteComputationRunnerUrl.mockResolvedValue(
      "https://runner.example",
    );

    renderSetting();

    expect(
      await screen.findByRole("textbox", {
        name: "settings.general.fields.remoteComputationRunnerUrl",
      }),
    ).toHaveValue("https://runner.example");
  });

  it("requires a URL before enabling remote mode", async () => {
    renderSetting();
    await waitFor(() => expect(screen.getByRole("combobox")).toBeEnabled());

    await chooseMode("Remote");

    expect(
      await screen.findByText("settings.general.validation.required"),
    ).toBeInTheDocument();
    expect(settingsApi.setComputionMode).not.toHaveBeenCalled();
    expect(settingsApi.setRemoteComputationRunnerUrl).not.toHaveBeenCalled();
  });

  it("validates and saves only on explicit submission", async () => {
    renderSetting();
    await waitFor(() => expect(screen.getByRole("combobox")).toBeEnabled());
    await chooseMode("Remote");

    const input = await screen.findByRole("textbox", {
      name: "settings.general.fields.remoteComputationRunnerUrl",
    });
    fireEvent.change(input, { target: { value: " https://runner.example/ " } });
    fireEvent.blur(input);
    expect(settingsApi.setRemoteComputationRunnerUrl).not.toHaveBeenCalled();
    fireEvent.click(
      screen.getByRole("button", {
        name: "settings.general.remoteRunner.validateAndSave",
      }),
    );

    await waitFor(() =>
      expect(settingsApi.setRemoteComputationRunnerUrl).toHaveBeenCalledWith(
        "https://runner.example",
      ),
    );
    expect(settingsApi.setComputionMode).not.toHaveBeenCalled();
    expect(
      await screen.findByText("settings.general.remoteRunner.connected"),
    ).toBeInTheDocument();
  });

  it("saves a non-remote mode immediately", async () => {
    settingsApi.getComputionMode.mockResolvedValue("Remote");
    settingsApi.getRemoteComputationRunnerUrl.mockResolvedValue(
      "https://runner.example",
    );
    renderSetting();
    await waitFor(() => expect(screen.getByRole("combobox")).toBeEnabled());

    await chooseMode("Local");

    await waitFor(() =>
      expect(settingsApi.setComputionMode).toHaveBeenCalledWith("Local"),
    );
    expect(settingsApi.setRemoteComputationRunnerUrl).not.toHaveBeenCalled();
  });

  it("keeps a rejected URL editable and shows a translated failure", async () => {
    settingsApi.getComputionMode.mockResolvedValue("Remote");
    settingsApi.getRemoteComputationRunnerUrl.mockResolvedValue(
      "https://working.example",
    );
    settingsApi.setRemoteComputationRunnerUrl.mockRejectedValueOnce(
      Object.assign(new Error("invalid"), {
        isAxiosError: true,
        response: { data: { code: "InvalidDimensions" } },
      }),
    );
    renderSetting();
    const input = await screen.findByRole("textbox", {
      name: "settings.general.fields.remoteComputationRunnerUrl",
    });
    fireEvent.change(input, { target: { value: "https://broken.example" } });
    fireEvent.click(
      screen.getByRole("button", {
        name: "settings.general.remoteRunner.validateAndSave",
      }),
    );
    expect(
      await screen.findByText(
        "settings.general.remoteRunner.errors.InvalidDimensions",
      ),
    ).toBeInTheDocument();
    expect(input).toHaveValue("https://broken.example");
    expect(settingsApi.setComputionMode).not.toHaveBeenCalled();
    expect(
      screen.queryByText("settings.general.remoteRunner.connected"),
    ).not.toBeInTheDocument();
  });

  it("allows fresh validation of an unchanged saved URL", async () => {
    settingsApi.getComputionMode.mockResolvedValue("Remote");
    settingsApi.getRemoteComputationRunnerUrl.mockResolvedValue(
      "https://runner.example",
    );
    renderSetting();
    await screen.findByText("settings.general.remoteRunner.connected");
    const button = screen.getByRole("button", {
      name: "settings.general.remoteRunner.validateAndSave",
    });
    fireEvent.click(button);
    await waitFor(() =>
      expect(settingsApi.setRemoteComputationRunnerUrl).toHaveBeenCalledTimes(
        1,
      ),
    );
    await waitFor(() => expect(button).toBeEnabled());
    fireEvent.click(button);
    await waitFor(() =>
      expect(settingsApi.setRemoteComputationRunnerUrl).toHaveBeenCalledTimes(
        2,
      ),
    );
  });
});
