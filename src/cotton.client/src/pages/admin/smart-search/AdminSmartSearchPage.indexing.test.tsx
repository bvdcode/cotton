import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { adminApi } from "@shared/api/adminApi";
import { settingsApi } from "@shared/api/settingsApi";
import { toast } from "@shared/ui/notifications";
import { readyComputationStatus } from "../../../test/computationStatus";
import { AdminSmartSearchPage } from "./AdminSmartSearchPage";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key, i18n: { language: "en" } }),
}));
vi.mock("@shared/ui/notifications", () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

beforeEach(() => {
  vi.clearAllMocks();
  vi.spyOn(settingsApi, "getAllowGlobalIndexing").mockResolvedValue(true);
  vi.spyOn(settingsApi, "getComputationStatus").mockResolvedValue(
    readyComputationStatus,
  );
  vi.spyOn(adminApi, "getVectorExtensionStatus").mockResolvedValue({
    extensionEnabled: true,
    extensionAvailable: true,
    databaseName: "cotton_test",
    postgresMajorVersion: 18,
    vectorCount: 0,
    fileCount: 1,
    embeddedFileCount: 0,
    indexReady: true,
    indexBuilding: false,
    indexSizeBytes: 0,
    indexErrorCode: null,
    indexCreateSql: "",
  });
  vi.spyOn(adminApi, "triggerFileIndexing").mockResolvedValue();
});
afterEach(() => vi.restoreAllMocks());

const renderPage = () =>
  render(
    <QueryClientProvider
      client={
        new QueryClient({
          defaultOptions: {
            queries: { retry: false },
            mutations: { retry: false },
          },
        })
      }
    >
      <MemoryRouter>
        <AdminSmartSearchPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );

describe("manual file indexing", () => {
  it("keeps refresh separate and disables the trigger until its request completes", async () => {
    let complete: (() => void) | undefined;
    vi.mocked(adminApi.triggerFileIndexing).mockReturnValue(
      new Promise<void>((resolve) => {
        complete = resolve;
      }),
    );
    renderPage();
    const trigger = screen.getByRole("button", {
      name: "smartSearch.actions.triggerIndexing",
    });
    expect(trigger).toBeDisabled();
    await waitFor(() => expect(trigger).toBeEnabled());
    fireEvent.click(
      screen.getByRole("button", { name: "smartSearch.actions.refresh" }),
    );
    await waitFor(() =>
      expect(adminApi.getVectorExtensionStatus).toHaveBeenCalledTimes(2),
    );
    expect(adminApi.triggerFileIndexing).not.toHaveBeenCalled();
    fireEvent.click(trigger);
    await waitFor(() => expect(trigger).toBeDisabled());
    expect(toast.success).not.toHaveBeenCalled();
    fireEvent.click(trigger);
    expect(adminApi.triggerFileIndexing).toHaveBeenCalledTimes(1);
    complete?.();
    await waitFor(() =>
      expect(toast.success).toHaveBeenCalledWith(
        "smartSearch.indexingRequested",
      ),
    );
    expect(trigger).toBeEnabled();
  });

  it("shows a scheduling failure and allows retry", async () => {
    vi.mocked(adminApi.triggerFileIndexing).mockRejectedValueOnce(
      new Error("Unavailable"),
    );
    renderPage();
    const trigger = screen.getByRole("button", {
      name: "smartSearch.actions.triggerIndexing",
    });
    await waitFor(() => expect(trigger).toBeEnabled());
    fireEvent.click(trigger);
    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith(
        "smartSearch.errors.triggerFailed",
      ),
    );
    expect(trigger).toBeEnabled();
    fireEvent.click(trigger);
    await waitFor(() =>
      expect(toast.success).toHaveBeenCalledWith(
        "smartSearch.indexingRequested",
      ),
    );
  });

  it("does not offer a manual run when global indexing is disabled", async () => {
    vi.mocked(settingsApi.getAllowGlobalIndexing).mockResolvedValue(false);
    renderPage();
    await screen.findByText("smartSearch.indexingDisabled");
    const trigger = screen.getByRole("button", {
      name: "smartSearch.actions.triggerIndexing",
    });
    expect(trigger).toBeDisabled();
    fireEvent.click(trigger);
    expect(adminApi.triggerFileIndexing).not.toHaveBeenCalled();
  });

  it("does not offer a manual run until the database is prepared", async () => {
    vi.mocked(adminApi.getVectorExtensionStatus).mockResolvedValue({
      extensionEnabled: false,
      extensionAvailable: true,
      databaseName: "cotton_test",
      postgresMajorVersion: 18,
      vectorCount: 0,
      fileCount: 1,
      embeddedFileCount: 0,
      indexReady: false,
      indexBuilding: false,
      indexSizeBytes: 0,
      indexErrorCode: null,
      indexCreateSql: "",
    });
    renderPage();
    await screen.findByRole("button", { name: "smartSearch.actions.enable" });
    expect(
      screen.getByRole("button", {
        name: "smartSearch.actions.triggerIndexing",
      }),
    ).toBeDisabled();
  });
});
