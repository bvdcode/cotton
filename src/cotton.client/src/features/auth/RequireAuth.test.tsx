import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { AuthPhase } from "./types";
import { RequireAuth } from "./RequireAuth";

const testState = vi.hoisted(() => ({
  phase: "booting" as AuthPhase,
  restoreSession: vi.fn<() => Promise<void>>(),
}));

vi.mock("./useAuth", () => ({
  useAuth: () => ({
    phase: testState.phase,
    restoreSession: testState.restoreSession,
  }),
}));

const renderGuard = () =>
  render(
    <MemoryRouter initialEntries={["/private"]}>
      <Routes>
        <Route
          path="/private"
          element={
            <RequireAuth>
              <div>private content</div>
            </RequireAuth>
          }
        />
        <Route path="/login" element={<div>login page</div>} />
      </Routes>
    </MemoryRouter>,
  );

describe("RequireAuth", () => {
  beforeEach(() => {
    testState.phase = "booting";
    testState.restoreSession.mockReset();
    testState.restoreSession.mockResolvedValue();
  });

  it("renders nothing while the single app bootstrap is pending", () => {
    const { container } = renderGuard();

    expect(container).toBeEmptyDOMElement();
  });

  it("redirects anonymous users to login", () => {
    testState.phase = "anonymous";

    renderGuard();

    expect(screen.getByText("login page")).toBeInTheDocument();
  });

  it("renders protected content for authenticated users", () => {
    testState.phase = "authenticated";

    renderGuard();

    expect(screen.getByText("private content")).toBeInTheDocument();
  });

  it("keeps the route in place and offers retry while unavailable", () => {
    testState.phase = "unavailable";

    renderGuard();
    fireEvent.click(screen.getByRole("button", { name: "actions.retry" }));

    expect(screen.queryByText("login page")).not.toBeInTheDocument();
    expect(testState.restoreSession).toHaveBeenCalledTimes(1);
  });
});
