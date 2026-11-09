import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { PropsWithChildren } from "react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import {
  routinesApi,
  type Routine,
  type RoutineRun,
} from "@shared/api/routinesApi";
import { createFolder } from "../../test/fileFixtures";
import { defaultRoutine } from "./routineForm";
import { useRoutines } from "./useRoutines";

const hub = vi.hoisted(() => ({
  progress: null as ((run: RoutineRun) => void) | null,
  connected: null as ((isReconnect: boolean) => void) | null,
  offProgress: vi.fn(),
  offConnected: vi.fn(),
  start: vi.fn(async () => {}),
}));
vi.mock("@shared/store/authStore", () => ({
  useAuthStore: () => "user-1",
}));
vi.mock("@shared/signalr/eventHub", () => ({
  eventHub: {
    start: hub.start,
    on: (_method: string, callback: (run: RoutineRun) => void) => {
      hub.progress = callback;
      return hub.offProgress;
    },
    onConnected: (callback: (isReconnect: boolean) => void) => {
      hub.connected = callback;
      return hub.offConnected;
    },
  },
}));
const run: RoutineRun = {
  id: "run-1",
  routineId: "routine-1",
  status: 0,
  createdAt: "2026-11-09T10:00:00.123456Z",
  updatedAt: "2026-11-09T10:00:00Z",
  finishedAt: null,
  processedCount: 0,
  deletedCount: 0,
  error: null,
};
const routine: Routine = {
  ...defaultRoutine(),
  id: run.routineId,
  name: "Cleanup",
  targetNodeId: "folder-1",
  targetNode: createFolder(),
  createdAt: run.createdAt,
  updatedAt: run.updatedAt,
  lastExecutionAt: run.createdAt,
  nextExecutionAt: null,
  timezone: "UTC",
  latestRun: run,
};
let client: QueryClient;
const wrapper = ({ children }: PropsWithChildren) => (
  <QueryClientProvider client={client}>{children}</QueryClientProvider>
);
beforeEach(() => {
  client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  client.setQueryData(["routines", "user-1"], [routine]);
});
afterEach(() => {
  cleanup();
  client.clear();
  vi.restoreAllMocks();
  vi.clearAllMocks();
});
it("accepts the same run with different fractional precision and rejects an older run", () => {
  const { result } = renderHook(useRoutines, { wrapper });
  act(() =>
    hub.progress?.({
      ...run,
      createdAt: "2026-11-09T10:00:00.1234567Z",
      processedCount: 500,
    }),
  );
  expect(
    client.getQueryData<Routine[]>(["routines", "user-1"])?.[0].latestRun
      ?.processedCount,
  ).toBe(500);
  act(() =>
    hub.progress?.({
      ...run,
      id: "older",
      createdAt: "2026-11-09T09:00:00Z",
      processedCount: 1,
    }),
  );
  expect(
    client.getQueryData<Routine[]>(["routines", "user-1"])?.[0].latestRun
      ?.processedCount,
  ).toBe(500);
  expect(result.current.isError).toBe(false);
});
it("resynchronizes the list and open history after a missed completion event", async () => {
  const finished = {
    ...routine,
    latestRun: { ...run, status: 1 as const, processedCount: 500 },
  };
  const list = vi.spyOn(routinesApi, "list").mockResolvedValue([finished]);
  const historyKey = ["routineRuns", "user-1", routine.id];
  client.setQueryData(historyKey, [run]);
  const { result, unmount } = renderHook(useRoutines, { wrapper });
  act(() => hub.connected?.(false));
  expect(list).not.toHaveBeenCalled();
  act(() => hub.connected?.(true));
  await waitFor(() =>
    expect(result.current.data?.[0].latestRun?.status).toBe(1),
  );
  expect(list).toHaveBeenCalledOnce();
  expect(client.getQueryState(historyKey)?.isInvalidated).toBe(true);
  unmount();
  expect(hub.offProgress).toHaveBeenCalledOnce();
  expect(hub.offConnected).toHaveBeenCalledOnce();
});
