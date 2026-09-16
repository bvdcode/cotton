import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { adminApi } from "@shared/api/adminApi";
import { AdminSmartSearchPage } from "./AdminSmartSearchPage";

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
  vi.spyOn(adminApi, "getVectorExtensionStatus").mockResolvedValue({
    extensionEnabled: false,
    vectorCount: 0,
  });
  vi.spyOn(adminApi, "enableVectorExtension").mockResolvedValue();
});

afterEach(() => {
  vi.restoreAllMocks();
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
      .mockResolvedValueOnce({ extensionEnabled: false, vectorCount: 0 })
      .mockResolvedValue({ extensionEnabled: true, vectorCount: 0 });
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
    expect(screen.getByText("smartSearch.disabled")).toBeInTheDocument();
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
    expect(screen.getByText("smartSearch.disabled")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "smartSearch.actions.enable" }),
    ).toBeEnabled();
    expect(adminApi.getVectorExtensionStatus).toHaveBeenCalledTimes(1);
  });

  it("lets the administrator retry when loading the status fails", async () => {
    vi.mocked(adminApi.getVectorExtensionStatus)
      .mockRejectedValueOnce(new Error("Unavailable"))
      .mockResolvedValue({ extensionEnabled: false, vectorCount: 0 });
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

    expect(await screen.findByText("smartSearch.disabled")).toBeInTheDocument();
    expect(
      screen.queryByText("smartSearch.errors.loadFailed"),
    ).not.toBeInTheDocument();
  });
});
