import {
  act,
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
import type { ComputationStatus } from "../../../shared/api/computation";

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

const renderSetting = (
  client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0, staleTime: 30_000 } },
  }),
) =>
  render(
    <QueryClientProvider client={client}>
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

  it("reloads current settings on reopening even while the cache is fresh", async () => {
    settingsApi.getComputionMode.mockResolvedValue("Remote");
    settingsApi.getRemoteComputationRunnerUrl.mockResolvedValue(
      "https://first.example",
    );
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, staleTime: 30_000 } },
    });
    const initial = renderSetting(client);
    expect(await screen.findByRole("textbox")).toHaveValue(
      "https://first.example",
    );
    initial.unmount();
    settingsApi.getRemoteComputationRunnerUrl.mockResolvedValue(
      "https://second.example",
    );

    renderSetting(client);

    await waitFor(() =>
      expect(screen.getByRole("textbox")).toHaveValue("https://second.example"),
    );
    expect(settingsApi.getRemoteComputationRunnerUrl).toHaveBeenCalledTimes(2);
    client.clear();
  });

  it("keeps the URL and mode editable when the status request fails", async () => {
    settingsApi.getComputionMode.mockResolvedValue("Remote");
    settingsApi.getRemoteComputationRunnerUrl.mockResolvedValue(
      "https://runner.example",
    );
    settingsApi.getComputationStatus.mockRejectedValue(
      new Error("Status unavailable"),
    );
    renderSetting();

    await screen.findByText("settings.general.remoteRunner.statusLoadFailed");
    expect(screen.getByRole("textbox")).toBeEnabled();
    expect(screen.getByRole("textbox")).toHaveValue("https://runner.example");
    expect(
      screen.getByRole("button", {
        name: "settings.general.remoteRunner.validateAndSave",
      }),
    ).toBeEnabled();
    await chooseMode("Local");
    await waitFor(() =>
      expect(settingsApi.setComputionMode).toHaveBeenCalledWith("Local"),
    );
  });

  it("can save while status is pending and ignores its late failure", async () => {
    settingsApi.getComputionMode.mockResolvedValue("Remote");
    settingsApi.getRemoteComputationRunnerUrl.mockResolvedValue(
      "https://runner.example",
    );
    let rejectStatus!: (error: Error) => void;
    settingsApi.getComputationStatus.mockReturnValue(
      new Promise<ComputationStatus>((_resolve, reject) => {
        rejectStatus = reject;
      }),
    );
    renderSetting();
    await waitFor(() =>
      expect(settingsApi.getComputationStatus).toHaveBeenCalledTimes(1),
    );
    const button = screen.getByRole("button", {
      name: "settings.general.remoteRunner.validateAndSave",
    });
    expect(screen.getByRole("textbox")).toBeEnabled();
    expect(button).toBeEnabled();
    fireEvent.click(button);
    await screen.findByText("settings.general.remoteRunner.connected");

    await act(async () => rejectStatus(new Error("Late status failure")));

    expect(
      screen.getByText("settings.general.remoteRunner.connected"),
    ).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
