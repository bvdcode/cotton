import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
  type InfiniteData,
  type QueryClient,
} from "@tanstack/react-query";
import { notificationsApi, type NotificationDto } from "../notificationsApi";
import { queryKeys } from "./queryKeys";

const PAGE_SIZE = 20;

type NotificationsPage = { data: NotificationDto[]; totalCount: number };
type NotificationsInfinite = InfiniteData<NotificationsPage, number>;

export const useNotificationsQuery = (options: {
  unreadOnly: boolean;
  enabled?: boolean;
}) =>
  useInfiniteQuery<
    NotificationsPage,
    Error,
    NotificationsInfinite,
    ReturnType<typeof queryKeys.notifications.list>,
    number
  >({
    queryKey: queryKeys.notifications.list({
      unreadOnly: options.unreadOnly,
    }),
    queryFn: ({ pageParam }) =>
      notificationsApi.list(pageParam, PAGE_SIZE, options.unreadOnly),
    initialPageParam: 1,
    getNextPageParam: (lastPage, allPages) =>
      lastPage.data.length >= PAGE_SIZE ? allPages.length + 1 : undefined,
    enabled: options.enabled ?? true,
  });

export const useUnreadCountQuery = (options: { enabled?: boolean } = {}) =>
  useQuery({
    queryKey: queryKeys.notifications.unreadCount(),
    queryFn: () => notificationsApi.getUnreadCount(),
    enabled: options.enabled ?? true,
  });

const invalidateUnreadCount = (queryClient: QueryClient): void => {
  void queryClient.invalidateQueries({
    queryKey: queryKeys.notifications.unreadCount(),
  });
};

const stampReadAt = (notification: NotificationDto): NotificationDto =>
  notification.readAt
    ? notification
    : { ...notification, readAt: new Date().toISOString() };

const updateNotificationInLists = (
  queryClient: QueryClient,
  predicate: (notification: NotificationDto) => boolean,
  updater: (notification: NotificationDto) => NotificationDto,
): void => {
  for (const unreadOnly of [false, true]) {
    queryClient.setQueryData<NotificationsInfinite>(
      queryKeys.notifications.list({ unreadOnly }),
      (old) => {
        if (!old) return old;

        return {
          ...old,
          pages: old.pages.map((page) => ({
            ...page,
            data: page.data.map((notification) =>
              predicate(notification) ? updater(notification) : notification,
            ),
          })),
        };
      },
    );
  }
};

export const useMarkAsReadMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => notificationsApi.markAsRead(id),
    onSuccess: (_, id) => {
      updateNotificationInLists(
        queryClient,
        (notification) => notification.id === id && !notification.readAt,
        stampReadAt,
      );
      invalidateUnreadCount(queryClient);
    },
  });
};

export const useMarkAllAsReadMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => notificationsApi.markAllAsRead(),
    onSuccess: () => {
      updateNotificationInLists(
        queryClient,
        (notification) => !notification.readAt,
        stampReadAt,
      );
      queryClient.setQueryData<number>(
        queryKeys.notifications.unreadCount(),
        () => 0,
      );
      invalidateUnreadCount(queryClient);
    },
  });
};

const includesNotification = (
  data: NotificationsInfinite,
  notificationId: string,
): boolean =>
  data.pages.some((page) =>
    page.data.some((notification) => notification.id === notificationId),
  );

export const prependCachedNotification = (
  queryClient: QueryClient,
  notification: NotificationDto,
): void => {
  const wasAlreadyCached = [false, true].some((unreadOnly) => {
    const data = queryClient.getQueryData<NotificationsInfinite>(
      queryKeys.notifications.list({ unreadOnly }),
    );
    return data ? includesNotification(data, notification.id) : false;
  });

  for (const unreadOnly of [false, true]) {
    if (unreadOnly && notification.readAt) continue;

    queryClient.setQueryData<NotificationsInfinite>(
      queryKeys.notifications.list({ unreadOnly }),
      (old) => {
        if (!old || old.pages.length === 0) return old;
        if (includesNotification(old, notification.id)) return old;

        const firstPage = old.pages[0];
        const updatedFirstPage: NotificationsPage = {
          data: [notification, ...firstPage.data],
          totalCount: firstPage.totalCount + 1,
        };

        return {
          ...old,
          pages: [updatedFirstPage, ...old.pages.slice(1)],
        };
      },
    );
  }

  if (!notification.readAt && !wasAlreadyCached) {
    invalidateUnreadCount(queryClient);
  }
};

export const clearNotificationCaches = (queryClient: QueryClient): void => {
  queryClient.removeQueries({ queryKey: queryKeys.notifications.all() });
};
