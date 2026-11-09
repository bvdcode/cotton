import type { JsonValue } from "../types/json";
import { z } from "zod";
import { getValidated, httpClient, parseValidated } from "./httpClient";
import { baseDtoSchema, nodeDtoSchema } from "./schemas/node";

export const RoutineScheduleKind = { Interval: 0, Calendar: 1 } as const;
export type RoutineScheduleKind =
  (typeof RoutineScheduleKind)[keyof typeof RoutineScheduleKind];
export const RoutineCalendarFrequency = {
  Daily: 0,
  Weekly: 1,
  Monthly: 2,
  Yearly: 3,
} as const;
export type RoutineCalendarFrequency =
  (typeof RoutineCalendarFrequency)[keyof typeof RoutineCalendarFrequency];
export const RoutineRunStatus = {
  Running: 0,
  Completed: 1,
  Failed: 2,
  Interrupted: 3,
} as const;
export type RoutineRunStatus =
  (typeof RoutineRunStatus)[keyof typeof RoutineRunStatus];

export const calendarSchema = z.object({
  frequency: z.enum(RoutineCalendarFrequency),
  hour: z.number().int().min(0).max(23),
  minute: z.number().int().min(0).max(59),
  dayOfWeek: z.number().int().min(0).max(6),
  dayOfMonth: z.number().int().min(1).max(31),
  month: z.number().int().min(1).max(12),
});
export const routineDraftSchema = z.object({
  name: z.string().trim().min(1).max(200),
  type: z.literal(0),
  enabled: z.boolean(),
  targetNodeId: z.string().min(1),
  intervalHours: z.number().int().min(1).max(8760),
  scheduleKind: z.enum(RoutineScheduleKind),
  calendar: calendarSchema.nullable(),
  startAtLocal: z.string().nullable(),
  settings: z.object({
    olderThanDays: z.number().int().min(1).max(36500),
    dateField: z.union([z.literal(0), z.literal(1)]),
    includeSubfolders: z.boolean(),
    permanentlyDelete: z.boolean(),
  }),
});
export const routineRunSchema = baseDtoSchema.extend({
  routineId: z.string(),
  status: z.enum(RoutineRunStatus),
  finishedAt: z.string().nullable(),
  processedCount: z.number(),
  deletedCount: z.number(),
  error: z.string().nullable(),
});
const routineSchema = baseDtoSchema.merge(routineDraftSchema).extend({
  lastExecutionAt: z.string().nullable(),
  nextExecutionAt: z.string().nullable(),
  timezone: z.string(),
  latestRun: routineRunSchema.nullable(),
  targetNode: nodeDtoSchema.nullable(),
});
export type RoutineDraft = z.infer<typeof routineDraftSchema>;
export type Routine = z.infer<typeof routineSchema>;
export type RoutineRun = z.infer<typeof routineRunSchema>;
export type RoutineCalendar = z.infer<typeof calendarSchema>;

export const routinesApi = {
  options: () =>
    getValidated("/routines/options", z.object({ timezone: z.string() })),
  list: () => getValidated("/routines", z.array(routineSchema)),
  runs: (id: string) =>
    getValidated(`/routines/${id}/runs`, z.array(routineRunSchema)),
  save: async (id: string | null, draft: RoutineDraft) => {
    const body = routineDraftSchema.parse(draft);
    const url = id ? `/routines/${id}` : "/routines";
    const response = id
      ? await httpClient.put<JsonValue>(url, body)
      : await httpClient.post<JsonValue>(url, body);
    return parseValidated(url, response.data, routineSchema);
  },
  run: async (id: string) => {
    await httpClient.post(`/routines/${id}/run`);
  },
  delete: async (id: string) => {
    await httpClient.delete(`/routines/${id}`);
  },
};
