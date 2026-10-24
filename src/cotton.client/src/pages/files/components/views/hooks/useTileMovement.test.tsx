import { useRef } from "react";
import { act, cleanup, render } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { useTileMovement } from "./useTileMovement";

const { media } = vi.hoisted(() => ({ media: { reduced: false } }));
vi.mock("@mui/material", async (load) => ({
  ...(await load<typeof import("@mui/material")>()),
  useMediaQuery: () => media.reduced,
}));

function Grid({ ids, revision = 0 }: { ids: string[]; revision?: number }) {
  const ref = useRef<HTMLDivElement>(null);
  useTileMovement(ref, ids, 0);
  return (
    <div ref={ref} data-revision={revision}>
      {ids.map((id, index) => (
        <div key={id} data-tile-id={id} data-index={index} />
      ))}
    </div>
  );
}

beforeEach(() => {
  vi.useFakeTimers();
  media.reduced = false;
  vi.stubGlobal(
    "ResizeObserver",
    class {
      observe() {}
      disconnect() {}
    },
  );
  vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(
    function (this: HTMLElement) {
      const index = Number(this.dataset.index ?? 0);
      return DOMRect.fromRect({
        x: (index % 2) * 100,
        y: Math.floor(index / 2) * 100,
        width: 100,
        height: 100,
      });
    },
  );
});
afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

it("animates across row boundaries after deletion and keeps the animation on unrelated renders", () => {
  const cancel = vi.fn();
  const animation = { cancel, playState: "idle" };
  const animate = vi.fn(() => animation);
  vi.stubGlobal("Animation", class {});
  Object.defineProperty(HTMLElement.prototype, "animate", {
    configurable: true,
    value: animate,
  });
  const view = render(<Grid ids={["a", "b", "c"]} />);
  view.rerender(<Grid ids={["b", "c"]} />);
  act(() => vi.advanceTimersByTime(20));
  expect(animate).toHaveBeenCalledWith(
    [{ transform: "translate(100px, 0px)" }, { transform: "translate(0, 0)" }],
    expect.objectContaining({ duration: 300 }),
  );
  expect(animate).toHaveBeenCalledWith(
    [
      { transform: "translate(-100px, 100px)" },
      { transform: "translate(0, 0)" },
    ],
    expect.any(Object),
  );
  view.rerender(<Grid ids={["b", "c"]} revision={1} />);
  expect(cancel).not.toHaveBeenCalled();
  view.unmount();
  expect(cancel).toHaveBeenCalled();
  Reflect.deleteProperty(HTMLElement.prototype, "animate");
});

it("does not animate when reduced motion is requested", () => {
  media.reduced = true;
  const view = render(<Grid ids={["a", "b", "c"]} />);
  view.rerender(<Grid ids={["b", "c"]} />);
  act(() => vi.advanceTimersByTime(20));
  expect(document.querySelectorAll("[data-tile-id]")).toHaveLength(2);
});
