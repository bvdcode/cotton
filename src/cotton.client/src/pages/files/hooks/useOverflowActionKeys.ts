import React from "react";
import type { PageHeaderActionItem } from "../components/PageHeader";

type UseOverflowActionKeysParams = {
  actions: PageHeaderActionItem[];
  actionsContainerRef: React.RefObject<HTMLDivElement | null>;
  actionButtonRefs: React.MutableRefObject<
    Record<string, HTMLButtonElement | null>
  >;
};

type VisibleActionsState = {
  signature: string;
  keys: string[];
};

const DEFAULT_ACTION_BUTTON_WIDTH = 40;

const sameKeys = (
  left: ReadonlyArray<string>,
  right: ReadonlyArray<string>,
): boolean => {
  return (
    left.length === right.length &&
    left.every((key, index) => key === right[index])
  );
};

/**
 * Computes which header action buttons can stay visible in the available width.
 * Non-fitting actions are expected to be moved into overflow menu by caller.
 */
export const useOverflowActionKeys = ({
  actions,
  actionsContainerRef,
  actionButtonRefs,
}: UseOverflowActionKeysParams): string[] => {
  const measuredActionWidthsRef = React.useRef<Record<string, number>>({});
  const actionSignature = actions.map((action) => action.key).join("\u0000");
  const actionKeys = React.useMemo(
    () => (actionSignature ? actionSignature.split("\u0000") : []),
    [actionSignature],
  );
  const [visibleActionsState, setVisibleActionsState] =
    React.useState<VisibleActionsState>(() => ({
      signature: actionSignature,
      keys: actionKeys,
    }));
  const visibleActionKeys =
    visibleActionsState.signature === actionSignature
      ? visibleActionsState.keys
      : actionKeys;

  const commitVisibleActionKeys = React.useCallback(
    (keys: string[]) => {
      setVisibleActionsState((current) => {
        if (
          current.signature === actionSignature &&
          sameKeys(current.keys, keys)
        ) {
          return current;
        }

        return { signature: actionSignature, keys };
      });
    },
    [actionSignature],
  );

  React.useLayoutEffect(() => {
    const container = actionsContainerRef.current;
    if (!container || actionKeys.length === 0) {
      commitVisibleActionKeys(actionKeys);
      return;
    }

    const measure = () => {
      const available = container.clientWidth;
      if (available <= 0) {
        commitVisibleActionKeys(actionKeys);
        return;
      }

      const gap = parseFloat(window.getComputedStyle(container).columnGap) || 0;
      const widths = actionKeys.map((key) => {
        const el = actionButtonRefs.current[key];
        const previousWidth =
          measuredActionWidthsRef.current[key] ?? DEFAULT_ACTION_BUTTON_WIDTH;
        if (!el) {
          return previousWidth;
        }

        const measured = Math.ceil(el.getBoundingClientRect().width);
        if (measured <= 0) {
          return previousWidth;
        }

        measuredActionWidthsRef.current[key] = measured;
        return measured;
      });

      const totalWidth =
        widths.reduce((sum, width) => sum + width, 0) +
        Math.max(0, widths.length - 1) * gap;

      if (totalWidth <= available) {
        commitVisibleActionKeys(actionKeys);
        return;
      }

      const maxWithoutOverflow = Math.max(
        0,
        available -
          (actionButtonRefs.current.overflow?.getBoundingClientRect().width ||
            DEFAULT_ACTION_BUTTON_WIDTH) -
          gap,
      );
      const nextVisible: string[] = [];
      let consumed = 0;

      for (let index = 0; index < actionKeys.length; index += 1) {
        const width = widths[index] ?? DEFAULT_ACTION_BUTTON_WIDTH;
        const projected = consumed + width + (nextVisible.length > 0 ? gap : 0);

        if (projected > maxWithoutOverflow) {
          break;
        }

        nextVisible.push(actionKeys[index]);
        consumed = projected;
      }

      if (nextVisible.length === 0 && actionKeys.length > 0) {
        nextVisible.push(actionKeys[0]);
      }

      commitVisibleActionKeys(nextVisible);
    };

    measure();

    const rafId = window.requestAnimationFrame(() => {
      measure();
    });

    const observer = new ResizeObserver(() => measure());
    observer.observe(container);

    return () => {
      window.cancelAnimationFrame(rafId);
      observer.disconnect();
    };
  }, [
    actionButtonRefs,
    actionKeys,
    actionsContainerRef,
    commitVisibleActionKeys,
  ]);

  return visibleActionKeys;
};
