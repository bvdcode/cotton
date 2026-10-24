import { useRef } from "react";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { findNameInitial, useNameNavigation } from "./useNameNavigation";

afterEach(cleanup);

it.each([
  ["ф", "Фото"],
  ["a", "Archive"],
  ["é", "E\u0301cole"],
  ["δ", "Δοκιμή"],
  ["文", "文档"],
  ["я", "Январь"],
])("finds the first case-insensitive Unicode initial %s", (key, name) => {
  expect(findNameInitial(["Other", name, name], key)).toBe(1);
});

it.each(["ArrowDown", "F2", " ", "/", "ab"])(
  "ignores non-initial key %s",
  (key) => {
    expect(findNameInitial(["Files"], key)).toBe(-1);
  },
);

it("focuses without opening and leaves typing and shortcuts alone", () => {
  const focus = vi.fn();
  function Browser() {
    const ref = useRef<HTMLDivElement>(null);
    useNameNavigation(ref, () => ["Archive", "Фото"], focus);
    return (
      <div ref={ref}>
        <button>Item</button>
        <input aria-label="Name" />
      </div>
    );
  }
  render(<Browser />);
  fireEvent.keyDown(document.body, { key: "ф" });
  expect(focus).toHaveBeenCalledWith(1);
  focus.mockClear();
  fireEvent.keyDown(document.body, { key: "a", ctrlKey: true });
  fireEvent.keyDown(document.body, { key: "a", isComposing: true });
  const input = screen.getByRole("textbox");
  input.focus();
  fireEvent.keyDown(input, { key: "a" });
  expect(focus).not.toHaveBeenCalled();
});
