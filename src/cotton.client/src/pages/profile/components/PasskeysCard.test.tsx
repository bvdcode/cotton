import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "../../../i18n";
import ru from "../../../locales/ru.json";
import type { PasskeyCredential } from "../../../shared/api/passkeysApi";
import { PasskeysCard } from "./PasskeysCard";

const passkeysApi = vi.hoisted(() => ({
  list: vi.fn<() => Promise<PasskeyCredential[]>>(),
  beginRegistration: vi.fn(),
  finishRegistration: vi.fn(),
  setLabel:
    vi.fn<
      (credentialId: string, label: string | null) => Promise<PasskeyCredential>
    >(),
  delete: vi.fn<(credentialId: string) => Promise<void>>(),
}));

vi.mock("../../../shared/api/passkeysApi", () => ({ passkeysApi }));

const createCredential = (
  values: Partial<PasskeyCredential> = {},
): PasskeyCredential => ({
  id: "credential-1",
  label: null,
  credentialId: "credential-id",
  transports: ["usb"],
  aaGuid: "00000000-0000-0000-0000-000000000000",
  authenticatorName: null,
  authenticatorKind: "SecurityKey",
  isBackupEligible: false,
  isBackedUp: false,
  createdAt: "2026-07-10T00:00:00Z",
  lastUsedAt: null,
  ...values,
});

const setMobileViewport = (matches: boolean): void => {
  const matchMedia = (query: string): MediaQueryList => ({
    matches,
    media: query,
    onchange: null,
    addListener: () => undefined,
    removeListener: () => undefined,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    dispatchEvent: () => true,
  });
  Object.defineProperty(window, "matchMedia", {
    configurable: true,
    writable: true,
    value: vi.fn(matchMedia),
  });
};

const renderExpandedCard = async (): Promise<void> => {
  render(<PasskeysCard />);
  await waitFor(() => expect(passkeysApi.list).toHaveBeenCalledOnce());

  const heading = screen.getByText(ru.profile.passkeys.title);
  const summary = heading.closest("button");
  if (summary === null) {
    throw new Error("Passkey accordion summary was not rendered as a button.");
  }

  fireEvent.click(summary);
  await waitFor(() =>
    expect(
      screen.queryByText(ru.profile.passkeys.loading),
    ).not.toBeInTheDocument(),
  );
};

beforeEach(async () => {
  vi.clearAllMocks();
  setMobileViewport(false);
  await i18n.changeLanguage("ru");
});

afterEach(async () => {
  await i18n.changeLanguage("en");
});

describe("PasskeysCard", () => {
  it("keeps long labels constrained and renders a localized fallback", async () => {
    const longLabel =
      `Очень длинная пользовательская метка ${"ключ ".repeat(40)}`.trim();
    passkeysApi.list.mockResolvedValue([
      createCredential({ label: longLabel }),
      createCredential({ id: "credential-2" }),
    ]);

    await renderExpandedCard();

    const longTitle = await screen.findByText(longLabel);
    expect(longTitle).toHaveAttribute("title", longLabel);
    expect(longTitle).toHaveStyle({
      overflow: "hidden",
      textOverflow: "ellipsis",
      whiteSpace: "nowrap",
    });
    expect(
      screen.getByText(ru.profile.passkeys.defaultNames.securityKey),
    ).toBeInTheDocument();
    expect(screen.queryByText("Security key")).not.toBeInTheDocument();
    expect(
      screen.getAllByRole("button", {
        name: ru.profile.passkeys.rename.button,
      }),
    ).toHaveLength(2);
    expect(
      screen.getAllByRole("button", { name: ru.profile.passkeys.delete }),
    ).toHaveLength(2);
  });

  it("exposes an accessible form that clears a label on submit", async () => {
    const credential = createCredential({ label: "Рабочий ключ" });
    passkeysApi.list.mockResolvedValue([credential]);
    passkeysApi.setLabel.mockResolvedValue({ ...credential, label: null });

    await renderExpandedCard();
    fireEvent.click(
      await screen.findByRole("button", {
        name: ru.profile.passkeys.rename.button,
      }),
    );

    const dialog = screen.getByRole("dialog", {
      name: ru.profile.passkeys.rename.title,
    });
    const input = screen.getByRole("textbox", {
      name: ru.profile.passkeys.rename.nameLabel,
    });
    expect(dialog).toHaveAccessibleDescription(
      ru.profile.passkeys.rename.description,
    );
    expect(input).toHaveFocus();
    expect(
      screen.getByRole("button", { name: ru.profile.passkeys.rename.save }),
    ).toHaveAttribute("type", "submit");

    fireEvent.change(input, { target: { value: "   " } });
    const form = input.closest("form");
    if (form === null) {
      throw new Error("Passkey rename input is not inside a form.");
    }
    fireEvent.submit(form);

    await waitFor(() =>
      expect(passkeysApi.setLabel).toHaveBeenCalledWith(credential.id, null),
    );
    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );
  });

  it("uses a full-screen rename dialog on mobile", async () => {
    setMobileViewport(true);
    passkeysApi.list.mockResolvedValue([createCredential()]);

    await renderExpandedCard();
    fireEvent.click(
      await screen.findByRole("button", {
        name: ru.profile.passkeys.rename.button,
      }),
    );

    expect(
      screen.getByRole("dialog", { name: ru.profile.passkeys.rename.title }),
    ).toHaveClass("MuiDialog-paperFullScreen");
  });
});
