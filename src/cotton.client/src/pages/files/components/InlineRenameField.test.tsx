import { createEvent, fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { InlineRenameField } from "./InlineRenameField";

describe("InlineRenameField", () => {
  it("prevents rename text selection from starting item drag", () => {
    render(
      <InlineRenameField
        value="report.txt"
        onChange={vi.fn()}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    const input = screen.getByDisplayValue("report.txt");
    const event = createEvent.dragStart(input);

    fireEvent(input, event);

    expect(event.defaultPrevented).toBe(true);
  });
});
