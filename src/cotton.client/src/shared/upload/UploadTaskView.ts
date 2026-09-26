import type { AppTask, AppTaskStatus } from "../tasks/types";
import type { UploadTaskInternal, UploadTaskStatus } from "./UploadManager";

export const toAppTaskStatus = (status: UploadTaskStatus): AppTaskStatus =>
  status === "uploading" ? "running" : status;

export const toAppTask = (task: UploadTaskInternal): AppTask => ({
  id: task.id,
  kind: "upload",
  label: task.fileName,
  scopeLabel: task.nodeLabel,
  bytesTotal: task.bytesTotal,
  bytesCompleted: task.bytesUploaded,
  progress01: task.progress01,
  status: toAppTaskStatus(task.status),
  speedBytesPerSec: task.uploadSpeedBytesPerSec,
  error: task.error,
  errorKey: task.errorKey,
  errorParams: task.errorParams,
  completedAt: task.completedAt,
});
