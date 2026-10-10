import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { DatabaseBackupAccess } from "./DatabaseBackupAccess";

const mocks = vi.hoisted(() => ({
  createToken: vi.fn<() => Promise<string>>(),
  writeText: vi.fn<() => Promise<void>>(),
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock("material-ui-confirm", () => ({ useConfirm: () => vi.fn() }));
vi.mock("../../../shared/api/adminApi", () => ({
  adminApi: { createDatabaseBackupToken: mocks.createToken },
}));
vi.mock("../../../shared/api/httpClient", () => ({
  getApiErrorMessage: (error: Error) => error.message,
}));

describe("DatabaseBackupAccess", () => {
  beforeEach(() => {
    mocks.createToken.mockReset().mockResolvedValue("backup-token");
    mocks.writeText.mockReset().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText: mocks.writeText },
    });
  });

  it("shows the issued token and a copyable curl command in a dismissible dialog", async () => {
    render(<DatabaseBackupAccess />);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    fireEvent.click(
      screen.getByRole("button", { name: "databaseBackup.access.generate" }),
    );
    await screen.findByRole("dialog", { name: "databaseBackup.access.token" });
    expect(
      screen.getByRole("textbox", { name: "databaseBackup.access.token" }),
    ).toHaveValue("backup-token");
    const command = screen.getByRole<HTMLTextAreaElement>("textbox", {
      name: "databaseBackup.access.curlExample",
    });
    expect(command.value).toContain("--request POST");
    expect(command.value).toContain("X-Cotton-Backup-Token: backup-token");
    expect(command.value).toContain(
      `${window.location.origin}/api/v1/server/database-backup`,
    );
    fireEvent.click(
      screen.getByRole("button", { name: "databaseBackup.access.copy" }),
    );
    await waitFor(() =>
      expect(mocks.writeText).toHaveBeenCalledWith("backup-token"),
    );
    expect(
      await screen.findByRole("button", {
        name: "databaseBackup.access.copied",
      }),
    ).toBeInTheDocument();
    fireEvent.click(
      screen.getByRole("button", { name: "databaseBackup.access.copyCommand" }),
    );
    await waitFor(() =>
      expect(mocks.writeText).toHaveBeenCalledWith(
        expect.stringContaining("X-Cotton-Backup-Token: backup-token"),
      ),
    );
    fireEvent.click(
      screen.getByRole("button", { name: "common:actions.close" }),
    );
    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );
    expect(screen.queryByDisplayValue("backup-token")).not.toBeInTheDocument();
  });

  it("keeps generation available after an unsuccessful token request", async () => {
    mocks.createToken.mockRejectedValue(new Error("Token generation failed"));
    render(<DatabaseBackupAccess />);
    fireEvent.click(
      screen.getByRole("button", { name: "databaseBackup.access.generate" }),
    );
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Token generation failed",
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "databaseBackup.access.generate" }),
    ).toBeEnabled();
  });
});
