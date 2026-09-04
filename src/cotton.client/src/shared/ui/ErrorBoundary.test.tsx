import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import "../../i18n";
import { ErrorBoundary } from "./ErrorBoundary";

interface TestPageProps {
  fail?: boolean;
}

const TestPage = ({ fail = false }: TestPageProps) => {
  if (fail) {
    throw new Error("render exploded");
  }

  return <div>Page content</div>;
};

describe("ErrorBoundary", () => {
  beforeEach(() => {
    vi.spyOn(console, "error").mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("renders a healthy page normally", () => {
    render(
      <ErrorBoundary resetKey="route-a">
        <TestPage />
      </ErrorBoundary>,
    );

    expect(screen.getByText("Page content")).toBeInTheDocument();
  });

  it("shows a compact recovery fallback without error details", () => {
    render(
      <ErrorBoundary resetKey="route-a">
        <TestPage fail />
      </ErrorBoundary>,
    );

    expect(
      screen.getByRole("heading", { name: "Unable to display this page" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("alert")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reload" })).toBeInTheDocument();
    expect(screen.queryByText(/render exploded/)).not.toBeInTheDocument();
  });

  it("mounts the page again when retry is requested", () => {
    let shouldFail = true;
    const RecoveringPage = () => {
      if (shouldFail) {
        throw new Error("temporary render failure");
      }

      return <div>Recovered page</div>;
    };

    render(
      <ErrorBoundary resetKey="route-a">
        <RecoveringPage />
      </ErrorBoundary>,
    );

    shouldFail = false;
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));

    expect(screen.getByText("Recovered page")).toBeInTheDocument();
  });

  it("recovers when navigation changes the reset key", () => {
    const view = render(
      <ErrorBoundary resetKey="route-a">
        <TestPage fail />
      </ErrorBoundary>,
    );

    view.rerender(
      <ErrorBoundary resetKey="route-b">
        <TestPage />
      </ErrorBoundary>,
    );

    expect(screen.getByText("Page content")).toBeInTheDocument();
  });
});
