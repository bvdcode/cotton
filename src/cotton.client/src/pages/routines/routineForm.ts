import {
  RoutineCalendarFrequency,
  RoutineScheduleKind,
  type Routine,
  type RoutineDraft,
} from "@shared/api/routinesApi";

export const intervalOptions = [1, 24, 168, 720, 8760] as const;
export const defaultRoutine = (): RoutineDraft => ({
  name: "",
  type: 0,
  enabled: true,
  targetNodeId: "",
  intervalHours: 24,
  scheduleKind: RoutineScheduleKind.Interval,
  startAtLocal: null,
  calendar: {
    frequency: RoutineCalendarFrequency.Daily,
    hour: 0,
    minute: 0,
    dayOfWeek: 1,
    dayOfMonth: 1,
    month: 1,
  },
  settings: {
    olderThanDays: 30,
    dateField: 1,
    includeSubfolders: true,
    permanentlyDelete: false,
  },
});
export const routineToDraft = (routine: Routine): RoutineDraft => ({
  name: routine.name,
  type: routine.type,
  enabled: routine.enabled,
  targetNodeId: routine.targetNodeId,
  intervalHours: routine.intervalHours,
  scheduleKind: routine.scheduleKind,
  calendar: routine.calendar ?? defaultRoutine().calendar,
  startAtLocal: routine.startAtLocal,
  settings: { ...routine.settings },
});
