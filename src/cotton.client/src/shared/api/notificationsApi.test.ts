import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("react-toastify", () => ({
  toast: { error: vi.fn() },
}));

vi.mock("../i18n/translateError", () => ({
  translateError: (namespace: string, key: string) => `${namespace}:${key}`,
}));

vi.mock("../store/authStore", () => ({
  getRefreshEnabled: () => true,
  useAuthStore: {
    getState: () => ({
      logoutLocal: vi.fn(),
    }),
  },
}));

const { httpClient } = await import("./httpClient");
const { notificationsApi } = await import("./notificationsApi");

const makeNotification = (id: string) => ({
  id,
  createdAt: "2026-05-17T00:00:00Z",
  updatedAt: "2026-05-17T00:00:00Z",
  userId: "user-1",
  title: `Notification ${id}`,
  content: null,
  readAt: null,
});

beforeEach(() => {
  vi.spyOn(console, "error").mockImplementation(() => undefined);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("notificationsApi.list", () => {
  it("passes page and pageSize and parses the body", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: [makeNotification("n1")],
      headers: { "x-total-count": "42" },
    });

    const result = await notificationsApi.list(2, 50);

    expect(get).toHaveBeenCalledWith("/notifications", {
      params: { page: 2, pageSize: 50 },
    });
    expect(result.data.map((notification) => notification.id)).toEqual(["n1"]);
    expect(result.totalCount).toBe(42);
  });

  it("adds the readAt filter for unread-only lists", async () => {
    const get = vi.spyOn(httpClient, "get").mockResolvedValue({
      data: [],
      headers: { "x-total-count": "0" },
    });

    await notificationsApi.list(1, 20, true);

    expect(get).toHaveBeenCalledWith("/notifications", {
      params: { page: 1, pageSize: 20, filter: "readAt=" },
    });
  });

  it("treats missing and invalid x-total-count as zero", async () => {
    vi.spyOn(httpClient, "get").mockResolvedValueOnce({
      data: [],
      headers: {},
    });

    await expect(notificationsApi.list()).resolves.toMatchObject({
      totalCount: 0,
    });

    vi.spyOn(httpClient, "get").mockResolvedValueOnce({
      data: [],
      headers: { "x-total-count": "not-a-number" },
    });

    await expect(notificationsApi.list()).resolves.toMatchObject({
      totalCount: 0,
    });
  });

  it("unwraps supported collection envelopes", async () => {
    vi.spyOn(httpClient, "get").mockResolvedValue({
      data: { notifications: [makeNotification("n1")] },
      headers: { "x-total-count": "1" },
    });

    const result = await notificationsApi.list();

    expect(result.data.map((notification) => notification.id)).toEqual(["n1"]);
  });
});

describe("notificationsApi mutations", () => {
  it("marks one notification as read", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({});

    await notificationsApi.markAsRead("n1");

    expect(patch).toHaveBeenCalledWith("/notifications/n1/read");
  });

  it("marks all notifications as read", async () => {
    const patch = vi.spyOn(httpClient, "patch").mockResolvedValue({});

    await notificationsApi.markAllAsRead();

    expect(patch).toHaveBeenCalledWith("/notifications/mark-all-read");
  });

  it("posts a test notification", async () => {
    const post = vi.spyOn(httpClient, "post").mockResolvedValue({});

    await notificationsApi.test();

    expect(post).toHaveBeenCalledWith("/notifications/test");
  });
});

describe("notificationsApi.getUnreadCount", () => {
  it("returns the parsed unread count", async () => {
    vi.spyOn(httpClient, "get").mockResolvedValue({
      data: { unreadCount: 12 },
    });

    await expect(notificationsApi.getUnreadCount()).resolves.toBe(12);
  });
});
