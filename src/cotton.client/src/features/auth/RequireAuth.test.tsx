import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { AuthPhase } from "./types";
import { RequireAuth } from "./RequireAuth";

const testState = vi.hoisted(() => ({
  phase: "booting" as AuthPhase,
}));

vi.mock("./useAuth", () => ({
  useAuth: () => ({ phase: testState.phase }),
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
});
