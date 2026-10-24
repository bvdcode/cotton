import {
  cleanup,
  createEvent,
  fireEvent,
  render,
  screen,
} from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { InlineRenameField } from "./InlineRenameField";

afterEach(cleanup);

describe("InlineRenameField", () => {
  it("focuses and selects the initial name", () => {
    render(
      <InlineRenameField
        value="New folder"
        onChange={vi.fn()}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    const input = screen.getByRole<HTMLInputElement>("textbox");
    expect(input).toHaveFocus();
    expect(input.selectionStart).toBe(0);
    expect(input.selectionEnd).toBe("New folder".length);
  });

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
