import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";

const mocks = vi.hoisted(() => ({
  start: vi.fn<() => Promise<void>>(),
  stop: vi.fn<() => Promise<void>>(),
  refreshEnabled: vi.fn<() => boolean>(),
  warning: vi.fn(),
  states: [] as Record<"state", HubConnectionState>[],
}));

vi.mock("../api/httpClient", () => ({
  getAccessToken: () => "token",
  refreshAccessToken: vi.fn(),
  clearAccessToken: vi.fn(),
}));
vi.mock("../store/authStore", () => ({
  getRefreshEnabled: mocks.refreshEnabled,
  useAuthStore: { getState: () => ({ logoutLocal: vi.fn() }) },
}));
vi.mock("../utils/clientDiagnostics", () => ({
  reportClientWarning: mocks.warning,
}));
vi.mock("@microsoft/signalr", async (importOriginal) => {
  const original = await importOriginal<typeof import("@microsoft/signalr")>();
  return {
    ...original,
    HubConnectionBuilder: class {
      withUrl() {
        return this;
      }
      withAutomaticReconnect() {
        return this;
      }
      configureLogging() {
        return this;
      }
      build() {
        const connection = {
          state: original.HubConnectionState.Disconnected,
          start: async () => {
            await mocks.start();
            connection.state = original.HubConnectionState.Connected;
          },
          stop: async () => {
            await mocks.stop();
            connection.state = original.HubConnectionState.Disconnected;
          },
          on: vi.fn(),
          off: vi.fn(),
          onreconnected: vi.fn(),
        };
        mocks.states.push(connection);
        return connection;
      }
    },
  };
});

import { eventHub } from "./eventHub";

beforeEach(() => {
  vi.useFakeTimers();
  vi.resetAllMocks();
  mocks.states.length = 0;
  mocks.refreshEnabled.mockReturnValue(true);
  mocks.start.mockResolvedValue();
  mocks.stop.mockResolvedValue();
});

afterEach(() => {
  eventHub.dispose();
  vi.useRealTimers();
});

it("retries an initial outage once for concurrent consumers and notifies on success", async () => {
  mocks.start
    .mockRejectedValueOnce(new Error("offline"))
    .mockRejectedValueOnce(new Error("offline"));
  const connected = vi.fn();
  const unsubscribe = eventHub.onConnected(connected);
  const first = eventHub.start();
  const second = eventHub.start();
  await vi.advanceTimersByTimeAsync(0);
  expect(mocks.start).toHaveBeenCalledTimes(2);
  expect(mocks.warning).toHaveBeenCalledOnce();
  expect(connected).not.toHaveBeenCalled();
  await vi.advanceTimersByTimeAsync(1000);
  await Promise.all([first, second]);
  expect(mocks.start).toHaveBeenCalledTimes(3);
  expect(connected).toHaveBeenCalledOnce();
  unsubscribe();
});

it("cancels pending retries on dispose", async () => {
  mocks.start.mockRejectedValue(new Error("offline"));
  const starting = eventHub.start();
  await vi.advanceTimersByTimeAsync(0);
  eventHub.dispose();
  await starting;
  await vi.advanceTimersByTimeAsync(60000);
  expect(mocks.start).toHaveBeenCalledTimes(2);
  expect(vi.getTimerCount()).toBe(0);
});

it("does not let an old start completion notify or replace a new session", async () => {
  let finishOld: (() => void) | undefined;
  mocks.start.mockImplementationOnce(
    () =>
      new Promise((resolve) => {
        finishOld = resolve;
      }),
  );
  const connected = vi.fn();
  const unsubscribe = eventHub.onConnected(connected);
  const oldStart = eventHub.start();
  eventHub.dispose();
  await eventHub.start();
  finishOld?.();
  await oldStart;
  await eventHub.start();
  expect(mocks.start).toHaveBeenCalledTimes(2);
  expect(connected).toHaveBeenCalledOnce();
  unsubscribe();
});

it("leaves an established connection's automatic reconnect in charge", async () => {
  await eventHub.start();
  mocks.states[0].state = HubConnectionState.Reconnecting;
  await eventHub.start();
  expect(mocks.start).toHaveBeenCalledOnce();
  expect(mocks.stop).not.toHaveBeenCalled();
});
