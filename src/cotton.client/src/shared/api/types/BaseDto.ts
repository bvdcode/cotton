/**
 * Base DTO interface matching backend BaseDto<TId>
 * All entities inherit id, createdAt, updatedAt from this base
 */
export interface BaseDto<TId = string> {
  /** Entity identifier */
  id: TId;
  /** Created at UTC (ISO string) */
  createdAt: string;
  /** Updated at UTC (ISO string) */
  updatedAt: string;
}
