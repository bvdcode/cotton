import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { ConfirmProvider } from "material-ui-confirm";
import i18n from "../../../i18n";
import {
  userPreferencesApi,
  type UserPreferences,
} from "../../../shared/api/userPreferencesApi";
import {
  useUserPreferencesStore,
  selectShareLinkExpireAfterMinutes,
} from "../../../shared/store/userPreferencesStore";
import { ShareLinkSettingsCard } from "./ShareLinkSettingsCard";
import { TotpSettingsCard } from "./TotpSettingsCard";
import { UserRole, type User } from "../../../features/auth/types";

beforeEach(async () => {
  await i18n.changeLanguage("en");
  useUserPreferencesStore.getState().reset();
  let saved: UserPreferences = {};
  vi.spyOn(userPreferencesApi, "update").mockImplementation(async (patch) => {
    saved = { ...saved, ...patch };
    return saved;
  });
});

afterEach(() => vi.restoreAllMocks());

it("persists dismissal, keeps the header warning and resets dismissal after changing expiration", async () => {
  const { unmount } = render(
    <ConfirmProvider>
      <ShareLinkSettingsCard />
    </ConfirmProvider>,
  );
  fireEvent.click(screen.getByRole("button", { name: /Share links/ }));
  const group = screen.getByRole("group");
  for (const [name, minutes] of [
    ["3 years", 1576800],
    ["10 years", 5256000],
    ["Never", null],
  ] as const) {
    fireEvent.click(within(group).getByRole("button", { name }));
    await waitFor(() =>
      expect(useUserPreferencesStore.getState().syncing).toBe(false),
    );
    expect(
      selectShareLinkExpireAfterMinutes(useUserPreferencesStore.getState()),
    ).toBe(minutes);
  }
  expect(screen.getByRole("alert")).toHaveTextContent(
    "unused links never expire",
  );
  fireEvent.click(screen.getByRole("button", { name: "Dismiss" }));
  await waitFor(() =>
    expect(useUserPreferencesStore.getState().syncing).toBe(false),
  );
  expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  expect(
    screen.getByRole("img", { name: "New share links never expire." }),
  ).toBeInTheDocument();
  unmount();
  render(
    <ConfirmProvider>
      <ShareLinkSettingsCard />
    </ConfirmProvider>,
  );
  fireEvent.click(screen.getByRole("button", { name: /Share links/ }));
  expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "Month" }));
  await waitFor(() =>
    expect(useUserPreferencesStore.getState().syncing).toBe(false),
  );
  expect(
    screen.queryByRole("img", { name: "New share links never expire." }),
  ).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "Never" }));
  await waitFor(() =>
    expect(useUserPreferencesStore.getState().syncing).toBe(false),
  );
  expect(screen.getByRole("alert")).toBeInTheDocument();
});

it.each([
  [UserRole.Admin, false, true],
  [UserRole.Admin, true, false],
  [UserRole.User, false, false],
])(
  "warns only administrators without 2FA (role %s, enabled %s)",
  (role, isTotpEnabled, warns) => {
    const user: User = {
      id: "user",
      username: "test",
      role,
      isTotpEnabled,
      createdAt: "",
      updatedAt: "",
    };
    render(<TotpSettingsCard user={user} onUserUpdate={() => undefined} />);
    const warning = screen.queryByRole("img", { name: /administrator access/ });
    expect(Boolean(warning)).toBe(warns);
  },
);
