import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AdminGeneralSettingsPage } from "./AdminGeneralSettingsPage";

const settingsApi = vi.hoisted(() => ({
  getDisableVersionCheck: vi.fn(),
  setDisableVersionCheck: vi.fn(),
}));

vi.mock("../../../shared/api/settingsApi", () => ({ settingsApi }));
vi.mock("../../../shared/api/httpClient", () => ({
  showApiErrorToast: vi.fn(),
}));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock("./ComputationModeSetting", () => ({
  ComputationModeSetting: () => null,
}));
vi.mock("./PublicBaseUrlSetting", () => ({
  PublicBaseUrlSetting: () => null,
}));
vi.mock("./ServerUsageSetting", () => ({
  ServerUsageSetting: () => null,
}));
vi.mock("./TimezoneSetting", () => ({
  TimezoneSetting: () => null,
}));
vi.mock("./TrustedProxyIpAddressSetting", () => ({
  TrustedProxyIpAddressSetting: () => null,
}));

describe("AdminGeneralSettingsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    settingsApi.getDisableVersionCheck.mockResolvedValue(false);
    settingsApi.setDisableVersionCheck.mockResolvedValue(undefined);
  });

  afterEach(() => {
    cleanup();
  });

  it("saves the disabled version-check setting", async () => {
    render(
      <MemoryRouter>
        <AdminGeneralSettingsPage />
      </MemoryRouter>,
    );

    const versionCheckSwitch = await screen.findByRole("switch");
    await waitFor(() => expect(versionCheckSwitch).toBeEnabled());
    fireEvent.click(versionCheckSwitch);

    await waitFor(() =>
      expect(settingsApi.setDisableVersionCheck).toHaveBeenCalledWith(true),
    );
  });
});
