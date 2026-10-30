import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useLongPress } from "./useLongPress";

class TestPointerEvent extends MouseEvent {
  readonly pointerId: number;
  readonly isPrimary: boolean;

  constructor(type: string, options: PointerEventInit = {}) {
    super(type, options);
    this.pointerId = options.pointerId ?? 1;
    this.isPrimary = options.isPrimary ?? true;
  }
}

function HoldButton({
  onHold,
  onClick,
  disabled = false,
}: {
  onHold: () => void;
  onClick: () => void;
  disabled?: boolean;
}) {
  const handlers = useLongPress(onHold, disabled);
  return (
    <button {...handlers} onClick={onClick}>
      Up
    </button>
  );
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal("PointerEvent", TestPointerEvent);
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("useLongPress", () => {
  it("preserves short and keyboard clicks, and suppresses the click after a hold", () => {
    const onHold = vi.fn();
    const onClick = vi.fn();
    render(<HoldButton onHold={onHold} onClick={onClick} />);
    const button = screen.getByRole("button");
    fireEvent.pointerDown(button, { button: 0, clientX: 20, clientY: 20 });
    act(() => vi.advanceTimersByTime(200));
    fireEvent.pointerUp(button);
    fireEvent.click(button, { detail: 1 });
    expect(onHold).not.toHaveBeenCalled();
    expect(onClick).toHaveBeenCalledTimes(1);

    fireEvent.pointerDown(button, { button: 0, clientX: 20, clientY: 20 });
    act(() => vi.advanceTimersByTime(600));
    fireEvent.pointerUp(button);
    fireEvent.click(button, { detail: 1 });
    expect(onHold).toHaveBeenCalledTimes(1);
    expect(onClick).toHaveBeenCalledTimes(1);
    fireEvent.click(button, { detail: 0 });
    expect(onClick).toHaveBeenCalledTimes(2);
  });

  it.each(["move", "cancel", "leave", "disabled", "unmount"])(
    "cancels a pending hold on %s",
    (action) => {
      const onHold = vi.fn();
      const onClick = vi.fn();
      const view = render(<HoldButton onHold={onHold} onClick={onClick} />);
      const button = screen.getByRole("button");
      fireEvent.pointerDown(button, { button: 0, clientX: 20, clientY: 20 });
      switch (action) {
        case "move":
          fireEvent.pointerMove(button, { clientX: 40, clientY: 20 });
          break;
        case "cancel":
          fireEvent.pointerCancel(button);
          break;
        case "leave":
          fireEvent.pointerLeave(button);
          break;
        case "disabled":
          view.rerender(
            <HoldButton onHold={onHold} onClick={onClick} disabled />,
          );
          break;
        case "unmount":
          view.unmount();
          break;
      }
      act(() => vi.advanceTimersByTime(600));
      expect(onHold).not.toHaveBeenCalled();
    },
  );
});
