import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { TrustedProxyIpAddressSetting } from "./TrustedProxyIpAddressSetting";

const settingsApi = vi.hoisted(() => ({
  getTrustedProxyIpAddress: vi.fn(),
  getObservedProxyIpAddress: vi.fn(),
  verifyAndSaveTrustedProxyIpAddress: vi.fn(),
}));

vi.mock("../../../shared/api/settingsApi", () => ({
  DIRECT_CONNECTION_IP_ADDRESS: "0.0.0.0",
  settingsApi,
}));

vi.mock("../../../shared/api/httpClient", () => ({
  showApiErrorToast: vi.fn(),
}));

vi.mock("@shared/ui/notifications", () => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

describe("TrustedProxyIpAddressSetting", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    settingsApi.getTrustedProxyIpAddress.mockResolvedValue("0.0.0.0");
    settingsApi.verifyAndSaveTrustedProxyIpAddress.mockResolvedValue({
      trustedProxyIpAddress: "0.0.0.0",
      observedProxyIpAddress: "198.51.100.25",
      matches: true,
      saved: true,
    });
  });

  afterEach(() => {
    cleanup();
  });

  it("keeps direct mode when its empty-looking input only receives focus", async () => {
    render(<TrustedProxyIpAddressSetting />);

    const input = await screen.findByRole("textbox");
    await waitFor(() => expect(input).toBeEnabled());
    fireEvent.focus(input);
    fireEvent.click(
      screen.getByRole("button", {
        name: "settings.general.trustedProxy.verifyAndSave",
      }),
    );

    await waitFor(() =>
      expect(
        settingsApi.verifyAndSaveTrustedProxyIpAddress,
      ).toHaveBeenCalledWith("0.0.0.0"),
    );
  });
});
