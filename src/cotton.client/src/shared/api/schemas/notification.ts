import { z } from "zod";

export const notificationSchema = z.object({
  id: z.string(),
  createdAt: z.string(),
  updatedAt: z.string(),
  userId: z.string(),
  title: z.string(),
  content: z.string().nullable(),
  readAt: z.string().nullable(),
  metadata: z.record(z.string(), z.string()).nullable().optional(),
});

export type NotificationDto = z.infer<typeof notificationSchema>;

export const isNotificationDto = (value: unknown): value is NotificationDto =>
  notificationSchema.safeParse(value).success;

export const notificationListResponseSchema = z
  .union([
    z.array(notificationSchema),
    z.object({ data: z.array(notificationSchema) }),
    z.object({ notifications: z.array(notificationSchema) }),
    z.object({ items: z.array(notificationSchema) }),
    z.object({ results: z.array(notificationSchema) }),
  ])
  .transform((value): NotificationDto[] => {
    if (Array.isArray(value)) return value;
    if ("data" in value) return value.data;
    if ("notifications" in value) return value.notifications;
    if ("items" in value) return value.items;
    return value.results;
  });

export const unreadCountResponseSchema = z.object({
  unreadCount: z.number(),
});
