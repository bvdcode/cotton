import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AdminPrivacySettingsPage } from "./AdminPrivacySettingsPage";

const settingsApi = vi.hoisted(() => ({
  getTelemetry: vi.fn(),
  setTelemetry: vi.fn(),
  getAllowCrossUserDeduplication: vi.fn(),
  setAllowCrossUserDeduplication: vi.fn(),
  getGeoIpLookupMode: vi.fn(),
  setGeoIpLookupMode: vi.fn(),
  getCustomGeoIpLookupUrl: vi.fn(),
  setCustomGeoIpLookupUrl: vi.fn(),
  testCustomGeoIpLookupUrl: vi.fn(),
}));
const translate = vi.hoisted(() => (key: string) => key);

vi.mock("../../../shared/api/settingsApi", () => ({ settingsApi }));

vi.mock("../../../shared/api/httpClient", () => ({
  showApiErrorToast: vi.fn(),
}));

vi.mock("../../../shared/ui/TelemetryHelpButton", () => ({
  TelemetryHelpButton: () => null,
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: translate }),
}));

const renderPage = (): void => {
  render(
    <MemoryRouter>
      <AdminPrivacySettingsPage />
    </MemoryRouter>,
  );
};

const selectGeoIpMode = async (mode: RegExp): Promise<void> => {
  fireEvent.mouseDown(
    screen.getByRole("combobox", {
      name: "settings.general.fields.geoIpLookupMode",
    }),
  );
  fireEvent.click(await screen.findByRole("option", { name: mode }));
};

describe("AdminPrivacySettingsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    settingsApi.getTelemetry.mockResolvedValue(false);
    settingsApi.setTelemetry.mockResolvedValue(undefined);
    settingsApi.getAllowCrossUserDeduplication.mockResolvedValue(false);
    settingsApi.setAllowCrossUserDeduplication.mockResolvedValue(undefined);
    settingsApi.getGeoIpLookupMode.mockResolvedValue("Disabled");
    settingsApi.setGeoIpLookupMode.mockResolvedValue(undefined);
    settingsApi.getCustomGeoIpLookupUrl.mockResolvedValue("");
    settingsApi.setCustomGeoIpLookupUrl.mockResolvedValue(undefined);
  });

  afterEach(() => {
    cleanup();
  });

  it("shares the loaded telemetry value with the GeoIP control", async () => {
    renderPage();

    await waitFor(() =>
      expect(settingsApi.getTelemetry).toHaveBeenCalledOnce(),
    );
    fireEvent.mouseDown(
      screen.getByRole("combobox", {
        name: "settings.general.fields.geoIpLookupMode",
      }),
    );

    expect(
      await screen.findByRole("option", {
        name: /settings\.general\.geoIpLookupMode\.CottonCloud/,
      }),
    ).toHaveAttribute("aria-disabled", "true");
    expect(settingsApi.getTelemetry).toHaveBeenCalledOnce();
    expect(
      screen.queryByText("settings.general.fields.allowGlobalIndexing"),
    ).not.toBeInTheDocument();
  });

  it("enables Cotton Bridge after telemetry is saved", async () => {
    renderPage();

    const telemetrySwitch = (await screen.findAllByRole("switch"))[0];
    await waitFor(() => expect(telemetrySwitch).toBeEnabled());
    fireEvent.click(telemetrySwitch);
    await waitFor(() =>
      expect(settingsApi.setTelemetry).toHaveBeenCalledWith(true),
    );

    fireEvent.mouseDown(
      screen.getByRole("combobox", {
        name: "settings.general.fields.geoIpLookupMode",
      }),
    );
    expect(
      await screen.findByRole("option", {
        name: /settings\.general\.geoIpLookupMode\.CottonCloud/,
      }),
    ).not.toHaveAttribute("aria-disabled", "true");
  });

  it("commits Custom HTTP immediately when a valid URL already exists", async () => {
    settingsApi.getCustomGeoIpLookupUrl.mockResolvedValue(
      "https://geo.example.test/lookup",
    );
    renderPage();

    await waitFor(() =>
      expect(settingsApi.getGeoIpLookupMode).toHaveBeenCalledOnce(),
    );
    await selectGeoIpMode(/settings\.general\.geoIpLookupMode\.CustomHttp/);

    await waitFor(() =>
      expect(settingsApi.setGeoIpLookupMode).toHaveBeenCalledWith("CustomHttp"),
    );
  });

  it("shows validation instead of pretending an invalid Custom HTTP mode was saved", async () => {
    renderPage();

    await waitFor(() =>
      expect(settingsApi.getGeoIpLookupMode).toHaveBeenCalledOnce(),
    );
    await selectGeoIpMode(/settings\.general\.geoIpLookupMode\.CustomHttp/);

    expect(
      screen.getByText("settings.general.validation.required"),
    ).toBeInTheDocument();
    expect(settingsApi.setGeoIpLookupMode).not.toHaveBeenCalled();
  });
});
