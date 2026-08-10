import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { UserRole } from "../../../features/auth/types";
import type { AdminUserDto } from "../../../shared/api/adminApi";
import { DeleteUserDialog } from "./DeleteUserDialog";

const { deleteUser } = vi.hoisted(() => ({
  deleteUser: vi.fn(),
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => {
      const translations: Record<string, string> = {
        "users.delete.title": "Delete account",
        "users.delete.warning": "Permanent deletion warning",
        "users.delete.confirmation": "Type the username to confirm",
        "users.delete.confirmationLabel": "Username",
        "users.delete.button": "Delete account",
        "users.delete.deleting": "Deleting...",
        "actions.cancel": "Cancel",
      };

      return translations[key] ?? key;
    },
  }),
}));

vi.mock("../../../shared/api/queries/admin", () => ({
  useDeleteAdminUserMutation: () => ({
    isPending: false,
    mutateAsync: deleteUser,
  }),
}));

const user: AdminUserDto = {
  id: "user-1",
  createdAt: "2026-08-06T00:00:00Z",
  updatedAt: "2026-08-06T00:00:00Z",
  username: "alice",
  email: "alice@example.com",
  role: UserRole.User,
  firstName: null,
  lastName: null,
  birthDate: null,
  isTotpEnabled: false,
  totpEnabledAt: null,
  totpFailedAttempts: 0,
  lastActivityAt: null,
  activeSessionCount: 0,
  storageUsedBytes: 0,
};

describe("DeleteUserDialog", () => {
  beforeEach(() => {
    deleteUser.mockReset();
    deleteUser.mockResolvedValue(undefined);
  });

  it("requires the exact username before permanently deleting the account", async () => {
    const onClose = vi.fn();
    render(<DeleteUserDialog open user={user} onClose={onClose} />);

    const deleteButton = screen.getByRole("button", {
      name: "Delete account",
    });
    expect(deleteButton).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Username"), {
      target: { value: "wrong" },
    });
    expect(deleteButton).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Username"), {
      target: { value: "alice" },
    });
    expect(deleteButton).toBeEnabled();
    fireEvent.click(deleteButton);

    await waitFor(() => expect(deleteUser).toHaveBeenCalledWith("user-1"));
    expect(onClose).toHaveBeenCalledOnce();
  });
});
