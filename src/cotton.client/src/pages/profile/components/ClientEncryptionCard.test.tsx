import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { UserRole, type User } from "../../../features/auth/types";
import { useVault } from "../../../shared/crypto";
import { useUserPreferencesStore } from "../../../shared/store/userPreferencesStore";
import { ClientEncryptionCard } from "./ClientEncryptionCard";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

const makeUser = (): User => ({
  id: "user-1",
  role: UserRole.User,
  username: "alice",
  preferences: {},
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
});

describe("ClientEncryptionCard", () => {
  afterEach(() => {
    cleanup();
    useVault.getState().lock();
    useUserPreferencesStore.getState().reset();
    vi.restoreAllMocks();
  });

  it("uses a full-width setup action without a separate not-set status chip", () => {
    render(<ClientEncryptionCard user={makeUser()} onUserUpdate={vi.fn()} />);
    fireEvent.click(
      screen.getByRole("button", {
        name: "clientEncryption.sectionTitle clientEncryption.description",
      }),
    );

    expect(
      screen.queryByText("clientEncryption.status.notSetUp"),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", {
        name: "clientEncryption.actions.setup",
      }),
    ).toHaveClass("MuiButton-fullWidth");
  });
});
