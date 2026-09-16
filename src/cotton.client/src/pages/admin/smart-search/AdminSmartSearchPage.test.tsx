import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { adminApi, type VectorExtensionStatusDto } from "@shared/api/adminApi";
import { AdminSmartSearchPage } from "./AdminSmartSearchPage";
import { ENABLE_VECTOR_SQL } from "./smartSearchSetup";

const availableStatus: VectorExtensionStatusDto = {
  extensionEnabled: false,
  extensionAvailable: true,
  databaseName: "cotton_test",
  postgresMajorVersion: 18,
  vectorCount: 0,
};

const setupError = (code: string) =>
  Object.assign(new Error("Setup failed"), {
    isAxiosError: true,
    response: { status: 409, data: { code } },
  });

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
    i18n: { language: "en" },
  }),
}));

vi.mock("@shared/ui/notifications", () => ({
  toast: { error: vi.fn() },
}));

const renderPage = () => {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: Infinity },
      mutations: { retry: false },
    },
  });
  return render(
    <QueryClientProvider client={client}>
      <AdminSmartSearchPage />
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.spyOn(adminApi, "getVectorExtensionStatus").mockResolvedValue(
    availableStatus,
  );
  vi.spyOn(adminApi, "enableVectorExtension").mockResolvedValue();
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("AdminSmartSearchPage", () => {
  it("does not offer activation until the status is loaded", () => {
    vi.mocked(adminApi.getVectorExtensionStatus).mockReturnValue(
      new Promise(() => {}),
    );

    renderPage();

    expect(screen.getByRole("status")).toHaveAttribute(
      "aria-label",
      "smartSearch.loading",
    );
    expect(
      screen.queryByRole("button", { name: "smartSearch.actions.enable" }),
    ).not.toBeInTheDocument();
  });

  it("shows stored vectors and no activation button when enabled", async () => {
    vi.mocked(adminApi.getVectorExtensionStatus).mockResolvedValue({
      ...availableStatus,
      extensionEnabled: true,
      vectorCount: 2_000_000,
    });

    renderPage();

    expect(await screen.findByText("smartSearch.enabled")).toBeInTheDocument();
    expect(screen.getByText("2,000,000")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "smartSearch.actions.enable" }),
    ).not.toBeInTheDocument();
  });

  it("enables the extension and reads its new status before removing the button", async () => {
    vi.mocked(adminApi.getVectorExtensionStatus)
      .mockResolvedValueOnce(availableStatus)
      .mockResolvedValue({ ...availableStatus, extensionEnabled: true });
    let completeActivation: (() => void) | undefined;
    vi.mocked(adminApi.enableVectorExtension).mockReturnValue(
      new Promise<void>((resolve) => {
        completeActivation = resolve;
      }),
    );
    renderPage();
    const activate = await screen.findByRole("button", {
      name: "smartSearch.actions.enable",
    });

    fireEvent.click(activate);

    await waitFor(() => expect(activate).toBeDisabled());
    expect(screen.queryByText("smartSearch.enabled")).not.toBeInTheDocument();
    completeActivation?.();
    expect(await screen.findByText("smartSearch.enabled")).toBeInTheDocument();
    expect(adminApi.enableVectorExtension).toHaveBeenCalledTimes(1);
    expect(adminApi.getVectorExtensionStatus).toHaveBeenCalledTimes(2);
    expect(
      screen.queryByRole("button", { name: "smartSearch.actions.enable" }),
    ).not.toBeInTheDocument();
  });

  it("keeps the extension disabled and allows retry after an activation failure", async () => {
    vi.mocked(adminApi.enableVectorExtension).mockRejectedValue(
      new Error("Denied"),
    );
    renderPage();

    fireEvent.click(
      await screen.findByRole("button", { name: "smartSearch.actions.enable" }),
    );

    expect(
      await screen.findByText("smartSearch.errors.enableFailed"),
    ).toBeInTheDocument();
    expect(screen.queryByText("smartSearch.enabled")).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "smartSearch.actions.enable" }),
    ).toBeEnabled();
    expect(adminApi.getVectorExtensionStatus).toHaveBeenCalledTimes(1);
  });

  it("lets the administrator retry when loading the status fails", async () => {
    vi.mocked(adminApi.getVectorExtensionStatus)
      .mockRejectedValueOnce(new Error("Unavailable"))
      .mockResolvedValue(availableStatus);
    renderPage();
    expect(
      await screen.findByText("smartSearch.errors.loadFailed"),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "smartSearch.actions.enable" }),
    ).not.toBeInTheDocument();

    fireEvent.click(
      screen.getByRole("button", { name: "smartSearch.actions.refresh" }),
    );

    expect(
      await screen.findByRole("button", { name: "smartSearch.actions.enable" }),
    ).toBeEnabled();
    expect(
      screen.queryByText("smartSearch.errors.loadFailed"),
    ).not.toBeInTheDocument();
  });

  it("offers installation instructions instead of activation when the package is missing", async () => {
    vi.mocked(adminApi.getVectorExtensionStatus).mockResolvedValue({
      ...availableStatus,
      extensionAvailable: false,
    });
    renderPage();
    expect(
      await screen.findByText("smartSearch.installation.missing"),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "smartSearch.actions.enable" }),
    ).not.toBeInTheDocument();
    fireEvent.change(
      screen.getByRole("textbox", { name: "smartSearch.installation.image" }),
      { target: { value: "postgres:18-bookworm" } },
    );
    expect(
      screen.getByText("image: pgvector/pgvector:pg18-bookworm"),
    ).toBeInTheDocument();
    expect(adminApi.enableVectorExtension).not.toHaveBeenCalled();
  });

  it("advances to activation after the package has been installed", async () => {
    vi.mocked(adminApi.getVectorExtensionStatus)
      .mockResolvedValueOnce({ ...availableStatus, extensionAvailable: false })
      .mockResolvedValue(availableStatus);
    renderPage();
    fireEvent.click(
      await screen.findByRole("button", {
        name: "smartSearch.actions.checkInstallation",
      }),
    );
    expect(
      await screen.findByRole("button", { name: "smartSearch.actions.enable" }),
    ).toBeEnabled();
    await waitFor(() =>
      expect(
        screen.queryByText("smartSearch.installation.missing"),
      ).not.toBeInTheDocument(),
    );
  });

  it("shows the SQL command and rechecks after a permission failure", async () => {
    vi.mocked(adminApi.enableVectorExtension).mockRejectedValue(
      setupError("pgvector_permission_denied"),
    );
    renderPage();
    fireEvent.click(
      await screen.findByRole("button", { name: "smartSearch.actions.enable" }),
    );
    expect(
      await screen.findByText("smartSearch.activation.permissionDenied"),
    ).toBeInTheDocument();
    expect(screen.getByText(ENABLE_VECTOR_SQL)).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "smartSearch.actions.enable" }),
    ).not.toBeInTheDocument();
    fireEvent.click(
      screen.getByRole("button", { name: "smartSearch.actions.checkAgain" }),
    );
    await waitFor(() =>
      expect(adminApi.getVectorExtensionStatus).toHaveBeenCalledTimes(2),
    );
    expect(
      screen.getByText("smartSearch.activation.permissionDenied"),
    ).toBeInTheDocument();
    vi.mocked(adminApi.getVectorExtensionStatus).mockResolvedValue({
      ...availableStatus,
      extensionEnabled: true,
    });
    fireEvent.click(
      screen.getByRole("button", { name: "smartSearch.actions.checkAgain" }),
    );
    expect(await screen.findByText("smartSearch.enabled")).toBeInTheDocument();
    expect(
      screen.queryByText("smartSearch.activation.permissionDenied"),
    ).not.toBeInTheDocument();
  });

  it("returns to package installation when PostgreSQL reports a missing library", async () => {
    vi.mocked(adminApi.enableVectorExtension).mockRejectedValue(
      setupError("pgvector_package_missing"),
    );
    renderPage();
    fireEvent.click(
      await screen.findByRole("button", { name: "smartSearch.actions.enable" }),
    );
    expect(
      await screen.findByText("smartSearch.installation.missing"),
    ).toBeInTheDocument();
  });

  it("copies the complete manual activation command", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal("navigator", { ...navigator, clipboard: { writeText } });
    renderPage();
    fireEvent.click(
      await screen.findByRole("button", {
        name: "smartSearch.activation.manual",
      }),
    );
    fireEvent.click(
      await screen.findByRole("button", { name: "smartSearch.actions.copy" }),
    );
    await waitFor(() =>
      expect(writeText).toHaveBeenCalledWith(ENABLE_VECTOR_SQL),
    );
    vi.unstubAllGlobals();
  });
});
