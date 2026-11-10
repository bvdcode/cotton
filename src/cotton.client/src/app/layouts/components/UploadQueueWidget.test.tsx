import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
} from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import type { AppTaskSnapshot } from "@shared/tasks";
import { TaskQueueWidget } from "./UploadQueueWidget";

const store = vi.hoisted(() => ({
  snapshot: null as AppTaskSnapshot | null,
  listeners: new Set<() => void>(),
}));
vi.mock("../../../shared/tasks", () => ({
  taskManager: {
    subscribe: (listener: () => void) => {
      store.listeners.add(listener);
      return () => store.listeners.delete(listener);
    },
    getSnapshot: () => store.snapshot!,
    clearFinished: vi.fn(),
  },
}));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock("./UploadTaskList", () => ({
  UploadTaskList: () => <div role="list" />,
}));
beforeEach(() => {
  vi.useFakeTimers();
  store.snapshot = {
    open: true,
    tasks: [
      {
        id: "task",
        kind: "upload",
        label: "photo.jpg",
        scopeLabel: "Photos",
        bytesTotal: 100,
        bytesCompleted: 0,
        progress01: 0,
        status: "running",
      },
    ],
    overall: {
      bytesTotal: 100,
      bytesCompleted: 0,
      progress01: 0,
      speedBytesPerSec: 0,
    },
  };
});
afterEach(() => {
  cleanup();
  store.listeners.clear();
  vi.useRealTimers();
});
function publish(bytesCompleted: number): void {
  store.snapshot = {
    ...store.snapshot!,
    tasks: store.snapshot!.tasks.map((task) => ({
      ...task,
      bytesCompleted,
      progress01: bytesCompleted / 100,
    })),
    overall: {
      ...store.snapshot!.overall,
      bytesCompleted,
      progress01: bytesCompleted / 100,
    },
  };
  for (const listener of store.listeners) {
    listener();
  }
}
it("collapses after five seconds without changes", () => {
  render(<TaskQueueWidget />);
  act(() => vi.advanceTimersByTime(4999));
  expect(
    screen.getByRole("button", { name: "actions.collapse" }),
  ).toBeInTheDocument();
  act(() => vi.advanceTimersByTime(1));
  expect(
    screen.getByRole("button", { name: "actions.expand" }),
  ).toBeInTheDocument();
});
it("waits for a full idle interval after a progress update", () => {
  render(<TaskQueueWidget />);
  act(() => vi.advanceTimersByTime(4000));
  act(() => publish(50));
  act(() => vi.advanceTimersByTime(4000));
  expect(
    screen.getByRole("button", { name: "actions.collapse" }),
  ).toBeInTheDocument();
  act(() => vi.advanceTimersByTime(1000));
  expect(
    screen.getByRole("button", { name: "actions.expand" }),
  ).toBeInTheDocument();
});
it("does not treat an identical snapshot as activity", () => {
  render(<TaskQueueWidget />);
  act(() => vi.advanceTimersByTime(4000));
  act(() => publish(0));
  act(() => vi.advanceTimersByTime(1000));
  expect(
    screen.getByRole("button", { name: "actions.expand" }),
  ).toBeInTheDocument();
});
it("stays open during scrolling and waits after the last scroll", () => {
  render(<TaskQueueWidget />);
  for (let i = 0; i < 3; i++) {
    act(() => vi.advanceTimersByTime(4000));
    fireEvent.scroll(screen.getByRole("list"));
    expect(
      screen.getByRole("button", { name: "actions.collapse" }),
    ).toBeInTheDocument();
  }
  act(() => vi.advanceTimersByTime(5000));
  expect(
    screen.getByRole("button", { name: "actions.expand" }),
  ).toBeInTheDocument();
});
it("keeps the compact mode on new progress and rearms only after manual expansion", () => {
  render(<TaskQueueWidget />);
  act(() => vi.advanceTimersByTime(5000));
  act(() => publish(50));
  expect(
    screen.getByRole("button", { name: "actions.expand" }),
  ).toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "actions.expand" }));
  act(() => vi.advanceTimersByTime(4999));
  expect(
    screen.getByRole("button", { name: "actions.collapse" }),
  ).toBeInTheDocument();
  act(() => vi.advanceTimersByTime(1));
  expect(
    screen.getByRole("button", { name: "actions.expand" }),
  ).toBeInTheDocument();
});
it("cancels the idle timer on unmount", () => {
  const { unmount } = render(<TaskQueueWidget />);
  unmount();
  expect(vi.getTimerCount()).toBe(0);
});
