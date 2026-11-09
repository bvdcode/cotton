import { useEffect } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  routinesApi,
  routineRunSchema,
  RoutineRunStatus,
  type Routine,
} from "@shared/api/routinesApi";
import { useAuthStore } from "@shared/store/authStore";
import { eventHub } from "@shared/signalr/eventHub";
import { HUB_METHODS } from "@shared/signalr/hubMethods";

export const useRoutines = () => {
  const userId = useAuthStore((state) => state.user?.id);
  const client = useQueryClient();
  const key = ["routines", userId];
  const query = useQuery({
    queryKey: key,
    queryFn: routinesApi.list,
    enabled: !!userId,
  });
  useEffect(
    () =>
      eventHub.on(HUB_METHODS.RoutineProgress, (...args) => {
        const parsed = routineRunSchema.safeParse(args[0]);
        if (!parsed.success) {
          return;
        }
        const run = parsed.data;
        client.setQueryData<Routine[]>(["routines", userId], (current) =>
          current?.map((routine) => {
            if (
              routine.id !== run.routineId ||
              (routine.latestRun && routine.latestRun.createdAt > run.createdAt)
            ) {
              return routine;
            }
            return { ...routine, latestRun: run };
          }),
        );
        if (run.status !== RoutineRunStatus.Running) {
          void client.invalidateQueries({ queryKey: ["routines", userId] });
          void client.invalidateQueries({
            queryKey: ["routineRuns", userId, run.routineId],
          });
        }
      }),
    [client, userId],
  );
  return {
    ...query,
    refresh: () => client.invalidateQueries({ queryKey: key }),
  };
};
