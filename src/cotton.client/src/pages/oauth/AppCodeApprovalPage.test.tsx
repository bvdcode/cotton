import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AppCodeApprovalPage } from "./AppCodeApprovalPage";

const appCodeApi = vi.hoisted(() => ({
  approve: vi.fn<() => Promise<void>>(),
  deny: vi.fn<() => Promise<void>>(),
  getDetails: vi.fn(),
}));

const returnToAppCodeCaller = vi.hoisted(() => vi.fn());
const translate = vi.hoisted(
  () => (key: string, values?: { deviceName?: string; version?: string }) => {
    if (key === "request.versionWithDevice") {
      return `${values?.version} on ${values?.deviceName}`;
    }

    return key;
  },
);

vi.mock("../../shared/api/appCodeApi", () => ({ appCodeApi }));
vi.mock("./appCodeReturnTarget", () => ({ returnToAppCodeCaller }));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: translate }),
}));

const approvalId = "0190a000-0000-7000-8000-000000000001";

const renderPage = () =>
  render(
    <MemoryRouter
      initialEntries={[`/oauth/app-code/${approvalId}?returnTo=mobile`]}
    >
      <Routes>
        <Route path="/oauth/app-code/:id" element={<AppCodeApprovalPage />} />
      </Routes>
    </MemoryRouter>,
  );

describe("AppCodeApprovalPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    appCodeApi.approve.mockResolvedValue();
    appCodeApi.deny.mockResolvedValue();
    appCodeApi.getDetails.mockResolvedValue({
      id: approvalId,
      applicationName: "Cotton Cloud",
      applicationVersion: "1.2.3",
      deviceName: "Galaxy",
      origin: "203.0.113.10",
      requestedAt: "2026-09-09T12:00:00Z",
      expiresAt: "2026-09-09T12:10:00Z",
      status: "pending",
    });
  });

  it("keeps the approval metadata compact and orders deny before allow", async () => {
    renderPage();

    expect(await screen.findByText("Cotton Cloud")).toBeInTheDocument();
    expect(screen.getByText("1.2.3 on Galaxy")).toBeInTheDocument();
    expect(screen.queryByText("203.0.113.10")).not.toBeInTheDocument();
    expect(screen.queryByText("request.origin")).not.toBeInTheDocument();

    const buttons = screen.getAllByRole("button");
    expect(buttons[0]).toHaveTextContent("actions.deny");
    expect(buttons[1]).toHaveTextContent("actions.allow");
  });

  it("returns to the mobile caller after approval", async () => {
    renderPage();

    fireEvent.click(
      await screen.findByRole("button", { name: /actions.allow/ }),
    );

    await waitFor(() => {
      expect(appCodeApi.approve).toHaveBeenCalledWith(approvalId);
      expect(returnToAppCodeCaller).toHaveBeenCalledWith("mobile");
    });
  });

  it("returns to the mobile caller after denial", async () => {
    renderPage();

    fireEvent.click(
      await screen.findByRole("button", { name: /actions.deny/ }),
    );

    await waitFor(() => {
      expect(appCodeApi.deny).toHaveBeenCalledWith(approvalId);
      expect(returnToAppCodeCaller).toHaveBeenCalledWith("mobile");
    });
  });
});
