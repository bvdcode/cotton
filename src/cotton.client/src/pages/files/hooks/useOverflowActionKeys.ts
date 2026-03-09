import React from "react";
import type { PageHeaderActionItem } from "../components/PageHeader";

type UseOverflowActionKeysParams = {
  actions: PageHeaderActionItem[];
  actionsContainerRef: React.RefObject<HTMLDivElement | null>;
  actionButtonRefs: React.MutableRefObject<Record<string, HTMLButtonElement | null>>;
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
  const [visibleActionKeys, setVisibleActionKeys] = React.useState<string[]>([]);

  React.useEffect(() => {
    setVisibleActionKeys(actions.map((action) => action.key));
  }, [actions]);

  React.useLayoutEffect(() => {
    const container = actionsContainerRef.current;
    if (!container || actions.length === 0) {
      setVisibleActionKeys(actions.map((action) => action.key));
      return;
    }

    const ACTION_GAP = 4;
    const MORE_BUTTON_WIDTH = 36;

    const measure = () => {
      const available = container.clientWidth;
      if (available <= 0) {
        setVisibleActionKeys(actions.map((action) => action.key));
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
        setVisibleActionKeys(actions.map((action) => action.key));
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
        const projected = consumed + width + (nextVisible.length > 0 ? ACTION_GAP : 0);

        if (projected > maxWithoutOverflow) {
          break;
        }

        nextVisible.push(actions[index].key);
        consumed = projected;
      }

      if (nextVisible.length === 0 && actions.length > 0) {
        nextVisible.push(actions[0].key);
      }

      setVisibleActionKeys(nextVisible);
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
  }, [actions, actionsContainerRef, actionButtonRefs]);

  return visibleActionKeys;
};
