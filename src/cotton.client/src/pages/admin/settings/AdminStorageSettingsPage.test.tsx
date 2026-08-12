import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AdminStorageSettingsPage } from "./AdminStorageSettingsPage";

const settingsApi = vi.hoisted(() => ({
  getStorageType: vi.fn(),
  getS3Config: vi.fn(),
  getStorageSpaceMode: vi.fn(),
  getDefaultUserStorageQuotaBytes: vi.fn(),
  getDefaultUserTemplateNodeId: vi.fn(),
  getChunkSizeSettings: vi.fn(),
  getStoragePipelineSettings: vi.fn(),
  setDefaultUserStorageQuotaBytes: vi.fn(),
  setDefaultUserTemplateNodeId: vi.fn(),
}));
const translate = vi.hoisted(() => (key: string) => key);

vi.mock("../../../shared/api/settingsApi", () => ({
  settingsApi,
}));

vi.mock("../../../shared/api/httpClient", () => ({
  showApiErrorToast: vi.fn(),
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: translate,
  }),
}));

const findEnabledSaveButton = async (): Promise<HTMLElement> => {
  let enabledButton: HTMLElement | null = null;
  await waitFor(() => {
    const buttons = screen
      .getAllByRole("button", { name: "settings.actions.save" })
      .filter((button) => !button.hasAttribute("disabled"));

    expect(buttons).toHaveLength(1);
    enabledButton = buttons[0];
  });

  if (enabledButton === null) {
    throw new Error("Enabled save button was not found");
  }

  return enabledButton;
};

describe("AdminStorageSettingsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    settingsApi.getStorageType.mockResolvedValue("Local");
    settingsApi.getS3Config.mockResolvedValue({
      endpoint: "",
      region: "",
      bucket: "",
      accessKey: "",
      secretKey: "",
    });
    settingsApi.getStorageSpaceMode.mockResolvedValue("Optimal");
    settingsApi.getDefaultUserStorageQuotaBytes.mockResolvedValue(null);
    settingsApi.getDefaultUserTemplateNodeId.mockResolvedValue(null);
    settingsApi.getChunkSizeSettings.mockResolvedValue({
      maxChunkSizeBytes: 4 * 1024 * 1024,
      supportedMaxChunkSizeBytes: [4 * 1024 * 1024],
    });
    settingsApi.getStoragePipelineSettings.mockResolvedValue({
      compressionLevel: 1,
      minCompressionLevel: 1,
      maxCompressionLevel: 22,
      cipherChunkSizeBytes: 1024 * 1024,
      minCipherChunkSizeBytes: 128 * 1024,
      maxCipherChunkSizeBytes: 64 * 1024 * 1024,
      supportedCipherChunkSizeBytes: [1024 * 1024],
      encryptionThreads: 1,
      minEncryptionThreads: 1,
      maxEncryptionThreads: 1,
      supportedEncryptionThreads: [1],
    });
    settingsApi.setDefaultUserStorageQuotaBytes.mockResolvedValue(undefined);
    settingsApi.setDefaultUserTemplateNodeId.mockResolvedValue(undefined);
  });

  afterEach(() => {
    cleanup();
  });

  it("shows a specific validation message for an invalid quota", async () => {
    render(<AdminStorageSettingsPage />);

    const input = await screen.findByLabelText(
      "storageSettings.quota.fields.defaultUserQuotaGiB",
    );
    await waitFor(() => expect(input).toBeEnabled());
    fireEvent.change(input, { target: { value: "1" } });
    const saveButton = await findEnabledSaveButton();
    fireEvent.change(input, { target: { value: "-1" } });
    fireEvent.click(saveButton);

    expect(
      screen.getByText("storageSettings.errors.quotaInvalid"),
    ).toBeInTheDocument();
    expect(settingsApi.setDefaultUserStorageQuotaBytes).not.toHaveBeenCalled();
  });

  it("loads and saves the default user quota", async () => {
    settingsApi.getDefaultUserStorageQuotaBytes.mockResolvedValue(
      2 * 1024 ** 3,
    );
    render(<AdminStorageSettingsPage />);

    const input = await screen.findByLabelText(
      "storageSettings.quota.fields.defaultUserQuotaGiB",
    );
    await waitFor(() => expect(input).toBeEnabled());
    expect(input).toHaveValue(2);

    fireEvent.change(input, { target: { value: "3.5" } });
    fireEvent.click(await findEnabledSaveButton());

    await waitFor(() =>
      expect(settingsApi.setDefaultUserStorageQuotaBytes).toHaveBeenCalledWith(
        Math.round(3.5 * 1024 ** 3),
      ),
    );
    expect(input).toHaveValue(3.5);
  });

  it("shows a specific validation message for an invalid template node", async () => {
    render(<AdminStorageSettingsPage />);

    const input = await screen.findByLabelText(
      "storageSettings.template.fields.nodeId",
    );
    await waitFor(() => expect(input).toBeEnabled());
    fireEvent.change(input, {
      target: { value: "6f9619ff-8b86-d011-b42d-00cf4fc964ff" },
    });
    const saveButton = await findEnabledSaveButton();
    fireEvent.change(input, { target: { value: "not-a-guid" } });
    fireEvent.click(saveButton);

    expect(
      screen.getByText("storageSettings.errors.templateNodeIdInvalid"),
    ).toBeInTheDocument();
    expect(settingsApi.setDefaultUserTemplateNodeId).not.toHaveBeenCalled();
  });

  it("normalizes and saves the default template node id", async () => {
    render(<AdminStorageSettingsPage />);

    const input = await screen.findByLabelText(
      "storageSettings.template.fields.nodeId",
    );
    await waitFor(() => expect(input).toBeEnabled());
    fireEvent.change(input, {
      target: { value: "6F9619FF-8B86-D011-B42D-00CF4FC964FF" },
    });
    fireEvent.click(await findEnabledSaveButton());

    await waitFor(() =>
      expect(settingsApi.setDefaultUserTemplateNodeId).toHaveBeenCalledWith(
        "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
      ),
    );
    expect(input).toHaveValue("6f9619ff-8b86-d011-b42d-00cf4fc964ff");
  });

  it("keeps settings disabled when the initial load fails", async () => {
    settingsApi.getStorageType.mockRejectedValue(new Error("load failed"));
    render(<AdminStorageSettingsPage />);

    expect(
      await screen.findByText("storageSettings.errors.loadFailed"),
    ).toBeInTheDocument();
    expect(
      screen.getByLabelText("storageSettings.quota.fields.defaultUserQuotaGiB"),
    ).toBeDisabled();
    expect(
      screen.getByLabelText("storageSettings.template.fields.nodeId"),
    ).toBeDisabled();
  });
});
