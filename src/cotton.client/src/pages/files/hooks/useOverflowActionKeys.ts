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
  const actionKeys = React.useMemo(
    () => actions.map((action) => action.key),
    [actions],
  );
  const actionSignature = React.useMemo(
    () => actionKeys.join("\u0000"),
    [actionKeys],
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
    if (!container || actions.length === 0) {
      commitVisibleActionKeys(actionKeys);
      return;
    }

    const ACTION_GAP = 4;
    const MORE_BUTTON_WIDTH = 36;

    const measure = () => {
      const available = container.clientWidth;
      if (available <= 0) {
        commitVisibleActionKeys(actionKeys);
        return;
      }

      const widths = actions.map((action) => {
        const el = actionButtonRefs.current[action.key];
        if (!el) return 36;

        const measured = Math.ceil(el.getBoundingClientRect().width);
        return measured > 0 ? measured : 36;
      });

      const totalWidth =
        widths.reduce((sum, width) => sum + width, 0) +
        Math.max(0, widths.length - 1) * ACTION_GAP;

      if (totalWidth <= available) {
        commitVisibleActionKeys(actionKeys);
        return;
      }

      const maxWithoutOverflow = Math.max(
        0,
        available - MORE_BUTTON_WIDTH - ACTION_GAP,
      );
      const nextVisible: string[] = [];
      let consumed = 0;

      for (let index = 0; index < actions.length; index += 1) {
        const width = widths[index] ?? 36;
        const projected =
          consumed + width + (nextVisible.length > 0 ? ACTION_GAP : 0);

        if (projected > maxWithoutOverflow) {
          break;
        }

        nextVisible.push(actions[index].key);
        consumed = projected;
      }

      if (nextVisible.length === 0 && actions.length > 0) {
        nextVisible.push(actions[0].key);
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
    actions,
    actionsContainerRef,
    commitVisibleActionKeys,
  ]);

  return visibleActionKeys;
};
