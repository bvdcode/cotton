import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { adminApi } from "@shared/api/adminApi";
import { settingsApi } from "@shared/api/settingsApi";
import { readyComputationStatus } from "../../../test/computationStatus";
import { AdminSmartSearchPage } from "./AdminSmartSearchPage";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key, i18n: { language: "en" } }),
}));
vi.mock("@shared/ui/notifications", () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));
beforeEach(() => {
  vi.spyOn(settingsApi, "getAllowGlobalIndexing").mockResolvedValue(false);
  vi.spyOn(settingsApi, "setAllowGlobalIndexing").mockResolvedValue();
  vi.spyOn(settingsApi, "getComputionMode").mockResolvedValue("Local");
  vi.spyOn(settingsApi, "getRemoteComputationRunnerUrl").mockResolvedValue("");
  vi.spyOn(settingsApi, "getComputationStatus").mockResolvedValue({
    isReady: false,
    dimensions: null,
    info: null,
    error: "UnsupportedMode",
  });
  vi.spyOn(settingsApi, "setComputionMode").mockImplementation(async () => {
    vi.mocked(settingsApi.getComputionMode).mockResolvedValue("Cloud");
    vi.mocked(settingsApi.getComputationStatus).mockResolvedValue(
      readyComputationStatus,
    );
    return readyComputationStatus;
  });
  vi.spyOn(adminApi, "getVectorExtensionStatus").mockResolvedValue({
    extensionEnabled: true,
    extensionAvailable: true,
    databaseName: "cotton_test",
    postgresMajorVersion: 18,
    vectorCount: 0,
    fileCount: 4,
    embeddedFileCount: 0,
    indexReady: true,
    indexBuilding: false,
    indexSizeBytes: 8192,
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
const selectCloud = async () => {
  const select = await screen.findByRole("combobox");
  await waitFor(() =>
    expect(select).not.toHaveAttribute("aria-disabled", "true"),
  );
  fireEvent.mouseDown(select);
  expect(
    screen.queryByRole("option", {
      name: "settings.general.computionMode.Local",
    }),
  ).not.toBeInTheDocument();
  fireEvent.click(
    screen.getByRole("option", {
      name: "settings.general.computionMode.Cloud",
    }),
  );
};

describe("smart search setup flow", () => {
  it("connects the service before enabling indexing without saving unrelated settings", async () => {
    renderPage();
    await selectCloud();
    const toggle = await screen.findByRole("switch", {
      name: "smartSearch.wizard.enableSearch",
    });
    expect(settingsApi.setComputionMode).toHaveBeenCalledWith("Cloud");
    expect(settingsApi.setAllowGlobalIndexing).not.toHaveBeenCalled();
    expect(adminApi.triggerFileIndexing).not.toHaveBeenCalled();
    fireEvent.click(toggle);
    await waitFor(() =>
      expect(settingsApi.setAllowGlobalIndexing).toHaveBeenCalledWith(true),
    );
    const run = screen.getByRole("button", {
      name: "smartSearch.actions.triggerIndexing",
    });
    await waitFor(() => expect(run).toBeEnabled());
    fireEvent.click(run);
    await waitFor(() =>
      expect(adminApi.triggerFileIndexing).toHaveBeenCalledOnce(),
    );
  });

  it("stays on the service step when authorization fails", async () => {
    vi.mocked(settingsApi.setComputionMode).mockRejectedValue(
      Object.assign(new Error("Denied"), {
        isAxiosError: true,
        response: { data: { code: "AccessDenied" } },
      }),
    );
    renderPage();
    await selectCloud();
    expect(
      await screen.findByText(
        "settings.general.remoteRunner.errors.AccessDenied",
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("tab", { name: /smartSearch.wizard.search/ }),
    ).toBeDisabled();
    expect(screen.queryByRole("switch")).not.toBeInTheDocument();
    expect(settingsApi.setAllowGlobalIndexing).not.toHaveBeenCalled();
    expect(adminApi.triggerFileIndexing).not.toHaveBeenCalled();
  });
});
