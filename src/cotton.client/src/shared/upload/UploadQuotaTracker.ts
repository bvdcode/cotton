import { queryClient } from "../api/queries/queryClient";
import { queryKeys } from "../api/queries/queryKeys";
import {
  storageQuotaApi,
  type UserStorageQuotaDto,
} from "../api/storageQuotaApi";
import { formatBytes } from "../utils/formatBytes";
import type { UploadTaskInternal } from "./UploadManager";

const QUOTA_SNAPSHOT_TTL_MS = 30 * 60 * 1000;

export class UploadQuotaTracker {
  private snapshot: UserStorageQuotaDto | null = null;
  private loadedAt = 0;
  private refreshInFlight: Promise<void> | null = null;
  private refreshRequiredForCurrentBatch = false;
  private pendingBytes = 0;
  private readonly onRefreshFinished: () => void;

  constructor(onRefreshFinished: () => void) {
    this.onRefreshFinished = onRefreshFinished;
  }

  beginBatch(): void {
    this.syncFromQueryCache();
    this.refreshRequiredForCurrentBatch = this.isExpired();
  }

  shouldRefresh(): boolean {
    if (!this.refreshRequiredForCurrentBatch) {
      return false;
    }

    this.syncFromQueryCache();
    if (!this.isExpired()) {
      this.refreshRequiredForCurrentBatch = false;
      return false;
    }

    return true;
  }

  refresh(): void {
    if (this.refreshInFlight) {
      return;
    }

    this.refreshInFlight = storageQuotaApi
      .getCurrent()
      .then((quota) => {
        this.setSnapshot(quota);
      })
      .catch(() => {
        this.snapshot = null;
        this.loadedAt = Date.now();
      })
      .finally(() => {
        this.refreshRequiredForCurrentBatch = false;
        this.refreshInFlight = null;
        this.onRefreshFinished();
      });
  }

  tryReserve(task: UploadTaskInternal): boolean {
    if (task._replaceNodeFileId) {
      return true;
    }

    const quota = this.snapshot;
    if (!quota?.quotaBytes || quota.availableBytes === null) {
      return true;
    }

    const availableBytes = Math.max(
      0,
      quota.availableBytes - this.pendingBytes,
    );
    if (task.bytesTotal > availableBytes) {
      task.status = "failed";
      task.completedAt = Date.now();
      task.errorKey = "storageQuotaExceeded";
      task.errorParams = { available: formatBytes(availableBytes) };
      return false;
    }

    task._quotaReservationBytes = task.bytesTotal;
    this.pendingBytes += task._quotaReservationBytes;
    return true;
  }

  release(task: UploadTaskInternal, committed: boolean): void {
    const reservationBytes = task._quotaReservationBytes ?? 0;
    if (reservationBytes <= 0) {
      return;
    }

    task._quotaReservationBytes = undefined;
    this.pendingBytes = Math.max(0, this.pendingBytes - reservationBytes);
    if (committed && this.snapshot) {
      this.setSnapshot({
        ...this.snapshot,
        usedBytes: this.snapshot.usedBytes + reservationBytes,
        availableBytes:
          this.snapshot.availableBytes === null
            ? null
            : Math.max(0, this.snapshot.availableBytes - reservationBytes),
      });
    }
  }

  private isExpired(): boolean {
    return (
      this.loadedAt === 0 || Date.now() - this.loadedAt >= QUOTA_SNAPSHOT_TTL_MS
    );
  }

  private syncFromQueryCache(): void {
    const queryState = queryClient.getQueryState<UserStorageQuotaDto>(
      queryKeys.storageQuota.current(),
    );
    if (!queryState?.data || queryState.dataUpdatedAt <= this.loadedAt) {
      return;
    }

    this.snapshot = queryState.data;
    this.loadedAt = queryState.dataUpdatedAt;
  }

  private setSnapshot(quota: UserStorageQuotaDto): void {
    this.snapshot = quota;
    this.loadedAt = Date.now();
    queryClient.setQueryData(queryKeys.storageQuota.current(), quota);
  }
}
