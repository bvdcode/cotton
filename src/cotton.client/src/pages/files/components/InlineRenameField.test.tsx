import {
  cleanup,
  act,
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

  it("shows pending feedback and submits once for Enter followed by blur", async () => {
    let finish: (() => void) | undefined;
    const confirm = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          finish = resolve;
        }),
    );
    render(
      <InlineRenameField
        value="Photos"
        onChange={vi.fn()}
        onConfirm={confirm}
        onCancel={vi.fn()}
      />,
    );
    const input = screen.getByRole<HTMLInputElement>("textbox");
    fireEvent.keyDown(input, { key: "Enter" });
    fireEvent.keyDown(input, { key: "Enter" });
    fireEvent.blur(input);
    expect(confirm).toHaveBeenCalledTimes(1);
    expect(input).toHaveAttribute("readonly");
    expect(screen.getByRole("progressbar")).toBeVisible();
    await act(async () => finish?.());
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  });

  it("does not submit on blur after cancelling", () => {
    const confirm = vi.fn();
    const cancel = vi.fn();
    render(
      <InlineRenameField
        value="Photos"
        onChange={vi.fn()}
        onConfirm={confirm}
        onCancel={cancel}
      />,
    );
    const input = screen.getByRole("textbox");
    fireEvent.keyDown(input, { key: "Escape" });
    fireEvent.blur(input);
    expect(cancel).toHaveBeenCalledTimes(1);
    expect(confirm).not.toHaveBeenCalled();
  });
});
