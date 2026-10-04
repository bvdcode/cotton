import { useEffect, useLayoutEffect, useRef, useState } from "react";
import type { RefObject } from "react";
import type { PageHeaderActionItem } from "../components/PageHeader";

export const useTransitionActionItems = (
  actions: PageHeaderActionItem[],
  buttonRefs: RefObject<Record<string, HTMLButtonElement | null>>,
  duration: number,
) => {
  const signature = actions.map((action) => action.key).join("\u0000");
  const [state, setState] = useState({ signature, actions });
  const positions = useRef<
    Record<string, { left: number; animation?: Animation }>
  >({});

  useLayoutEffect(() => {
    for (const action of state.actions) {
      const button = buttonRefs.current[action.key];
      if (!button) {
        continue;
      }
      const left = button.offsetLeft;
      const previous = positions.current[action.key];
      if (previous && previous.left !== left && duration > 0) {
        const transform = new DOMMatrixReadOnly(
          getComputedStyle(button).transform,
        );
        previous.animation?.cancel();
        const animation = button.animate(
          [
            {
              transform: `translateX(${previous.left + transform.m41 - left}px)`,
            },
            { transform: "translateX(0)" },
          ],
          { duration, easing: "ease-out" },
        );
        positions.current[action.key] = { left, animation };
      } else {
        if (duration === 0) {
          previous?.animation?.cancel();
        }
        positions.current[action.key] = {
          left,
          animation: previous?.animation,
        };
      }
    }
  });

  useEffect(() => {
    const current = positions.current;
    return () =>
      Object.values(current).forEach((position) =>
        position.animation?.cancel(),
      );
  }, []);

  if (state.signature !== signature) {
    const next = [...actions];
    for (let index = state.actions.length - 1; index >= 0; index -= 1) {
      const previous = state.actions[index];
      if (next.some((action) => action.key === previous.key)) {
        continue;
      }
      const following = state.actions
        .slice(index + 1)
        .find((action) =>
          next.some((candidate) => candidate.key === action.key),
        );
      const position = following
        ? next.findIndex((action) => action.key === following.key)
        : next.length;
      next.splice(position, 0, previous);
    }
    setState({ signature, actions: next });
  }

  const handleExited = (key: string) => {
    if (actions.some((action) => action.key === key)) {
      return;
    }
    positions.current[key]?.animation?.cancel();
    delete positions.current[key];
    setState((current) => ({
      ...current,
      actions: current.actions.filter((action) => action.key !== key),
    }));
  };

  return {
    actions: state.actions.map(
      (previous) =>
        actions.find((action) => action.key === previous.key) ?? previous,
    ),
    handleExited,
  };
};
