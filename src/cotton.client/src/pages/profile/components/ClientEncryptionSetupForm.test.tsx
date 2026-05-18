import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ClientEncryptionSetupForm } from "./ClientEncryptionSetupForm";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

const cryptoMocks = vi.hoisted(() => ({
  persistEnvelope: vi.fn(),
  setupEnvelope: vi.fn(),
  unlock: vi.fn(),
}));

vi.mock("../../../shared/crypto", () => ({
  persistEnvelope: cryptoMocks.persistEnvelope,
  setupEnvelope: cryptoMocks.setupEnvelope,
  useVault: (
    selector: (state: { unlock: typeof cryptoMocks.unlock }) => unknown,
  ) => selector({ unlock: cryptoMocks.unlock }),
}));

const recoveryPhrase = Array.from(
  { length: 24 },
  (_, index) => `word${index + 1}`,
).join(" ");

describe("ClientEncryptionSetupForm", () => {
  beforeEach(() => {
    cryptoMocks.persistEnvelope.mockReset();
    cryptoMocks.setupEnvelope.mockReset();
    cryptoMocks.unlock.mockReset();
  });

  afterEach(() => {
    cleanup();
  });

  it("persists the generated envelope and unlocks the vault on finish", async () => {
    const envelope = new Uint8Array([1, 2, 3]);
    const masterKey = {} as CryptoKey;
    const preferences = { cryptoEnvelope: "opaque-envelope" };
    const onSuccess = vi.fn();

    cryptoMocks.setupEnvelope.mockResolvedValue({
      envelope,
      masterKey,
      recoveryPhrase,
    });
    cryptoMocks.persistEnvelope.mockResolvedValue(preferences);

    render(
      <ClientEncryptionSetupForm onCancel={vi.fn()} onSuccess={onSuccess} />,
    );

    fireEvent.click(
      screen.getByLabelText("clientEncryption.setupDialog.acknowledge"),
    );
    fireEvent.click(
      screen.getByRole("button", {
        name: "clientEncryption.setupDialog.continue",
      }),
    );

    fireEvent.change(
      screen.getByLabelText("clientEncryption.setupDialog.passwordLabel"),
      { target: { value: "correct horse" } },
    );
    fireEvent.change(
      screen.getByLabelText("clientEncryption.setupDialog.confirmPasswordLabel"),
      { target: { value: "correct horse" } },
    );
    fireEvent.click(
      screen.getByRole("button", { name: "clientEncryption.setupDialog.next" }),
    );

    expect(await screen.findByText("word1")).toBeInTheDocument();

    fireEvent.click(
      screen.getByLabelText("clientEncryption.setupDialog.phraseStored"),
    );
    fireEvent.click(
      screen.getByRole("button", {
        name: "clientEncryption.setupDialog.finish",
      }),
    );

    await waitFor(() => expect(onSuccess).toHaveBeenCalledWith(preferences));
    expect(cryptoMocks.setupEnvelope).toHaveBeenCalledWith("correct horse");
    expect(cryptoMocks.persistEnvelope).toHaveBeenCalledWith(envelope);
    expect(cryptoMocks.unlock).toHaveBeenCalledWith(masterKey, {
      persistToSession: true,
    });
  });
});
