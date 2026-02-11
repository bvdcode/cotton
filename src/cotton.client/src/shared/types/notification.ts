import type { BaseDto } from "../api/types";

export interface NotificationDto extends BaseDto {
  userId: string;
  title: string;
  content: string | null;
  readAt: string | null;
}
