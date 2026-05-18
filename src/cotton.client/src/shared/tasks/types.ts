export type AppTaskKind =
  | "upload"
  | "encrypt"
  | "decrypt"
  | "convert"
  | "system";

export type AppTaskStatus =
  | "queued"
  | "running"
  | "finalizing"
  | "completed"
  | "failed";

export interface AppTask {
  id: string;
  kind: AppTaskKind;
  label: string;
  scopeLabel: string;
  bytesTotal: number;
  bytesCompleted: number;
  progress01: number;
  status: AppTaskStatus;
  speedBytesPerSec?: number | null;
  error?: string;
  errorKey?: string;
  errorParams?: Record<string, string | number>;
  completedAt?: number;
}

export interface AppTaskOverallStats {
  bytesTotal: number;
  bytesCompleted: number;
  progress01: number;
  speedBytesPerSec: number;
}

export interface AppTaskSnapshot {
  open: boolean;
  tasks: AppTask[];
  overall: AppTaskOverallStats;
}

export interface CreateAppTaskOptions {
  kind: AppTaskKind;
  label: string;
  scopeLabel?: string;
  bytesTotal?: number;
}

export interface UpdateAppTaskOptions {
  status?: AppTaskStatus;
  bytesTotal?: number;
  bytesCompleted?: number;
  progress01?: number;
  speedBytesPerSec?: number | null;
  label?: string;
  scopeLabel?: string;
}

export interface AppTaskHandle {
  readonly id: string;
  update: (options: UpdateAppTaskOptions) => void;
  complete: () => void;
  fail: (error?: { message?: string; key?: string; params?: Record<string, string | number> }) => void;
}
