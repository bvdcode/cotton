import { act, renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PageHeaderActionItem } from "../components/PageHeader";
import { useTransitionActionItems } from "./useTransitionActionItems";

const makeAction = (key: string): PageHeaderActionItem => ({
  key,
  icon: <span />,
  title: key,
  onClick: vi.fn(),
});

describe("useTransitionActionItems", () => {
  it("keeps departing actions in order until their exit completes", () => {
    const first = makeAction("first");
    const second = makeAction("second");
    const next = makeAction("next");
    const refs = { current: {} };
    const { result, rerender } = renderHook(
      (actions: PageHeaderActionItem[]) =>
        useTransitionActionItems(actions, refs, 0),
      { initialProps: [first, second] },
    );
    rerender([first, next]);
    expect(result.current.actions.map((action) => action.key)).toEqual([
      "first",
      "next",
      "second",
    ]);
    act(() => result.current.handleExited("second"));
    expect(result.current.actions.map((action) => action.key)).toEqual([
      "first",
      "next",
    ]);
  });

  it("preserves an action restored before its exit completes and uses its latest callback", () => {
    const first = makeAction("first");
    const second = makeAction("second");
    const refs = { current: {} };
    const { result, rerender } = renderHook(
      (actions: PageHeaderActionItem[]) =>
        useTransitionActionItems(actions, refs, 0),
      { initialProps: [first, second] },
    );
    rerender([first]);
    const restored = makeAction("second");
    rerender([first, restored]);
    act(() => result.current.handleExited("second"));
    expect(result.current.actions).toEqual([first, restored]);
    result.current.actions[1].onClick();
    expect(restored.onClick).toHaveBeenCalledOnce();
    expect(second.onClick).not.toHaveBeenCalled();
  });
});
