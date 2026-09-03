import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SetupGate } from "./SetupGate";

const testState = vi.hoisted(() => ({
  isInitialized: null as boolean | null,
  loaded: false,
  loading: false,
  fetchSetupStatus: vi.fn<() => Promise<void>>(),
}));

vi.mock("../auth", () => ({
  useAuth: () => ({
    isAuthenticated: true,
    user: { role: 2 },
  }),
}));

vi.mock("../../shared/store/setupStatusStore", () => ({
  useSetupStatusStore: (
    selector: (state: typeof testState) => unknown,
  ): unknown => selector(testState),
}));

const renderGate = (initialEntry = "/") =>
  render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route
          path="/"
          element={
            <SetupGate>
              <div>dashboard</div>
            </SetupGate>
          }
        />
        <Route
          path="/setup"
          element={
            <SetupGate>
              <div>setup wizard</div>
            </SetupGate>
          }
        />
      </Routes>
    </MemoryRouter>,
  );

describe("SetupGate", () => {
  beforeEach(() => {
    testState.isInitialized = null;
    testState.loaded = false;
    testState.loading = false;
    testState.fetchSetupStatus.mockReset();
    testState.fetchSetupStatus.mockResolvedValue();
  });

  it("renders the requested route while setup status loads in the background", async () => {
    renderGate();

    expect(screen.getByText("dashboard")).toBeInTheDocument();
    await waitFor(() => expect(testState.fetchSetupStatus).toHaveBeenCalled());
  });

  it("redirects to setup after the background result reports incomplete setup", () => {
    testState.isInitialized = false;
    testState.loaded = true;

    renderGate();

    expect(screen.getByText("setup wizard")).toBeInTheDocument();
  });

  it("redirects away from setup after the background result reports completion", () => {
    testState.isInitialized = true;
    testState.loaded = true;

    renderGate("/setup");

    expect(screen.getByText("dashboard")).toBeInTheDocument();
  });
});
