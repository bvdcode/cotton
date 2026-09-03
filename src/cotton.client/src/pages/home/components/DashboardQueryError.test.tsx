import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DashboardQueryError } from "./DashboardQueryError";

describe("DashboardQueryError", () => {
  it("shows the failure and retries the query", () => {
    const onRetry = vi.fn();

    render(
      <DashboardQueryError message="Could not load files." onRetry={onRetry} />,
    );

    expect(screen.getByText("Could not load files.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button"));
    expect(onRetry).toHaveBeenCalledOnce();
  });
});
