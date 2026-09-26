import type {
  AppTask,
  AppTaskHandle,
  CreateAppTaskOptions,
  UpdateAppTaskOptions,
} from "../tasks/types";

export interface ExternalTaskInternal extends AppTask {
  _external: true;
}

const makeId = () => `${Date.now()}-${Math.random().toString(16).slice(2)}`;

export class UploadExternalTasks {
  readonly tasks: ExternalTaskInternal[] = [];
  private readonly emit: () => void;

  constructor(emit: () => void) {
    this.emit = emit;
  }

  getPublicTasks(): AppTask[] {
    return this.tasks.map((task) => ({
      id: task.id,
      kind: task.kind,
      label: task.label,
      scopeLabel: task.scopeLabel,
      bytesTotal: task.bytesTotal,
      bytesCompleted: task.bytesCompleted,
      progress01: task.progress01,
      status: task.status,
      speedBytesPerSec: task.speedBytesPerSec,
      error: task.error,
      errorKey: task.errorKey,
      errorParams: task.errorParams,
      completedAt: task.completedAt,
    }));
  }

  createTask(options: CreateAppTaskOptions): AppTaskHandle {
    const bytesTotal = Math.max(0, options.bytesTotal ?? 0);
    const task: ExternalTaskInternal = {
      _external: true,
      id: makeId(),
      kind: options.kind,
      label: options.label,
      scopeLabel: options.scopeLabel ?? "",
      bytesTotal,
      bytesCompleted: 0,
      progress01: bytesTotal > 0 ? 0 : 1,
      status: "queued",
    };

    this.tasks.unshift(task);

    return {
      id: task.id,
      update: (update) => this.updateTask(task.id, update),
      complete: () =>
        this.updateTask(task.id, {
          status: "completed",
          bytesCompleted: task.bytesTotal,
          progress01: 1,
        }),
      fail: (error) => {
        const current = this.tasks.find((item) => item.id === task.id);
        if (!current) return;

        current.status = "failed";
        current.completedAt = Date.now();
        current.error = error?.message;
        current.errorKey = error?.key;
        current.errorParams = error?.params;
        this.emit();
      },
    };
  }

  private updateTask(taskId: string, update: UpdateAppTaskOptions): void {
    const task = this.tasks.find((item) => item.id === taskId);
    if (!task) return;

    if (update.label !== undefined) task.label = update.label;
    if (update.scopeLabel !== undefined) task.scopeLabel = update.scopeLabel;
    if (update.bytesTotal !== undefined) {
      task.bytesTotal = Math.max(0, update.bytesTotal);
      task.bytesCompleted = Math.min(task.bytesCompleted, task.bytesTotal);
      task.progress01 =
        task.bytesTotal > 0 ? task.bytesCompleted / task.bytesTotal : 1;
    }
    if (update.status !== undefined) {
      task.status = update.status;
      if (update.status === "completed" || update.status === "failed") {
        task.completedAt = Date.now();
      }
    }
    if (update.bytesCompleted !== undefined) {
      task.bytesCompleted = Math.max(
        0,
        Math.min(task.bytesTotal, update.bytesCompleted),
      );
      task.progress01 =
        task.bytesTotal > 0 ? task.bytesCompleted / task.bytesTotal : 1;
    }
    if (update.progress01 !== undefined) {
      task.progress01 = Math.max(0, Math.min(1, update.progress01));
      if (task.bytesTotal > 0 && update.bytesCompleted === undefined) {
        task.bytesCompleted = Math.round(task.bytesTotal * task.progress01);
      }
    }
    if (update.speedBytesPerSec !== undefined) {
      task.speedBytesPerSec = update.speedBytesPerSec;
    }

    this.emit();
  }
}
