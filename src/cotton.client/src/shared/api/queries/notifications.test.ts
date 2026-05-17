import { QueryClient, type InfiniteData } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import type { NotificationDto } from "../notificationsApi";
import {
  clearNotificationCaches,
  prependCachedNotification,
} from "./notifications";
import { queryKeys } from "./queryKeys";

type NotificationsPage = { data: NotificationDto[]; totalCount: number };
type NotificationsInfinite = InfiniteData<NotificationsPage, number>;

const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

const createNotification = (
  id: string,
  readAt: string | null = null,
): NotificationDto => ({
  id,
  createdAt: "2026-05-16T00:00:00Z",
  updatedAt: "2026-05-16T00:00:00Z",
  userId: "user-id",
  title: `Notification ${id}`,
  content: null,
  readAt,
});

const createInfiniteData = (
  pages: NotificationDto[][],
): NotificationsInfinite => ({
  pages: pages.map((data) => ({ data, totalCount: data.length })),
  pageParams: pages.map((_, index) => index + 1),
});

describe("notification query cache helpers", () => {
  it("prepends unread notifications to both cached list variants", () => {
    const queryClient = createQueryClient();
    const existing = createNotification("existing");
    const incoming = createNotification("incoming");

    queryClient.setQueryData(
      queryKeys.notifications.list({ unreadOnly: false }),
      createInfiniteData([[existing]]),
    );
    queryClient.setQueryData(
      queryKeys.notifications.list({ unreadOnly: true }),
      createInfiniteData([[existing]]),
    );
    queryClient.setQueryData(queryKeys.notifications.unreadCount(), 1);

    prependCachedNotification(queryClient, incoming);

    const allData = queryClient.getQueryData<NotificationsInfinite>(
      queryKeys.notifications.list({ unreadOnly: false }),
    );
    const unreadData = queryClient.getQueryData<NotificationsInfinite>(
      queryKeys.notifications.list({ unreadOnly: true }),
    );

    expect(allData?.pages[0].data.map((item) => item.id)).toEqual([
      "incoming",
      "existing",
    ]);
    expect(unreadData?.pages[0].data.map((item) => item.id)).toEqual([
      "incoming",
      "existing",
    ]);
    expect(
      queryClient.getQueryData(queryKeys.notifications.unreadCount()),
    ).toBe(2);
  });

  it("does not duplicate notifications already present in later pages", () => {
    const queryClient = createQueryClient();
    const first = createNotification("first");
    const duplicate = createNotification("duplicate");

    queryClient.setQueryData(
      queryKeys.notifications.list({ unreadOnly: false }),
      createInfiniteData([[first], [duplicate]]),
    );

    prependCachedNotification(queryClient, duplicate);

    const allData = queryClient.getQueryData<NotificationsInfinite>(
      queryKeys.notifications.list({ unreadOnly: false }),
    );

    expect(allData?.pages[0].data.map((item) => item.id)).toEqual(["first"]);
    expect(allData?.pages[1].data.map((item) => item.id)).toEqual([
      "duplicate",
    ]);
  });

  it("keeps read notifications out of the unread list and count", () => {
    const queryClient = createQueryClient();
    const read = createNotification("read", "2026-05-16T00:01:00Z");

    queryClient.setQueryData(
      queryKeys.notifications.list({ unreadOnly: false }),
      createInfiniteData([[]]),
    );
    queryClient.setQueryData(
      queryKeys.notifications.list({ unreadOnly: true }),
      createInfiniteData([[]]),
    );
    queryClient.setQueryData(queryKeys.notifications.unreadCount(), 4);

    prependCachedNotification(queryClient, read);

    const allData = queryClient.getQueryData<NotificationsInfinite>(
      queryKeys.notifications.list({ unreadOnly: false }),
    );
    const unreadData = queryClient.getQueryData<NotificationsInfinite>(
      queryKeys.notifications.list({ unreadOnly: true }),
    );

    expect(allData?.pages[0].data.map((item) => item.id)).toEqual(["read"]);
    expect(unreadData?.pages[0].data).toEqual([]);
    expect(
      queryClient.getQueryData(queryKeys.notifications.unreadCount()),
    ).toBe(4);
  });

  it("clears all notification query caches", () => {
    const queryClient = createQueryClient();

    queryClient.setQueryData(
      queryKeys.notifications.list({ unreadOnly: false }),
      createInfiniteData([[createNotification("existing")]]),
    );
    queryClient.setQueryData(queryKeys.notifications.unreadCount(), 5);

    clearNotificationCaches(queryClient);

    expect(
      queryClient.getQueryData(
        queryKeys.notifications.list({ unreadOnly: false }),
      ),
    ).toBeUndefined();
    expect(
      queryClient.getQueryData(queryKeys.notifications.unreadCount()),
    ).toBeUndefined();
  });
});
