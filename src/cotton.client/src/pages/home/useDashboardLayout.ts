import { useCallback, useMemo, useRef } from "react";
import {
  USER_PREFERENCE_KEYS,
  useUserPreferencesStore,
} from "../../shared/store/userPreferencesStore";
import {
  hideDashboardWidget,
  moveDashboardWidget,
  parseDashboardLayout,
  restoreDashboardWidget,
  serializeDashboardLayout,
  type DashboardLayout,
  type DashboardWidgetId,
} from "./dashboardModel";

export const useDashboardLayout = () => {
  const rawLayout = useUserPreferencesStore(
    (state) => state.preferences[USER_PREFERENCE_KEYS.dashboardLayout],
  );
  const updatePreferences = useUserPreferencesStore(
    (state) => state.updatePreferences,
  );
  const layout = useMemo(() => parseDashboardLayout(rawLayout), [rawLayout]);
  const draggedWidgetRef = useRef<DashboardWidgetId | null>(null);

  const save = useCallback(
    (next: DashboardLayout): void => {
      void updatePreferences({
        [USER_PREFERENCE_KEYS.dashboardLayout]:
          serializeDashboardLayout(next),
      });
    },
    [updatePreferences],
  );

  const hide = useCallback(
    (widgetId: DashboardWidgetId): void => {
      save(hideDashboardWidget(layout, widgetId));
    },
    [layout, save],
  );

  const restore = useCallback(
    (widgetId: DashboardWidgetId): void => {
      save(restoreDashboardWidget(layout, widgetId));
    },
    [layout, save],
  );

  const move = useCallback(
    (widgetId: DashboardWidgetId, offset: -1 | 1): void => {
      const currentIndex = layout.order.indexOf(widgetId);
      const target = layout.order[currentIndex + offset];
      if (!target) {
        return;
      }
      save(moveDashboardWidget(layout, widgetId, target));
    },
    [layout, save],
  );

  const startDrag = useCallback((widgetId: DashboardWidgetId): void => {
    draggedWidgetRef.current = widgetId;
  }, []);

  const endDrag = useCallback((): void => {
    draggedWidgetRef.current = null;
  }, []);

  const drop = useCallback(
    (targetId: DashboardWidgetId): void => {
      const sourceId = draggedWidgetRef.current;
      draggedWidgetRef.current = null;
      if (!sourceId) {
        return;
      }
      save(moveDashboardWidget(layout, sourceId, targetId));
    },
    [layout, save],
  );

  return {
    layout,
    hide,
    restore,
    move,
    startDrag,
    endDrag,
    drop,
  };
};
