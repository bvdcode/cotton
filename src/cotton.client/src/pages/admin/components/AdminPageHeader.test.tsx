import { Button } from "@mui/material";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { AdminPageHeader } from "./AdminPageHeader";

describe("AdminPageHeader", () => {
  it("renders the page title as the primary heading", () => {
    render(
      <AdminPageHeader
        title="Storage"
        description="Configure storage."
        action={<Button>Refresh</Button>}
      />,
    );

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(
      "Storage",
    );
    expect(screen.getByText("Configure storage.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Refresh" })).toBeInTheDocument();
  });
});
