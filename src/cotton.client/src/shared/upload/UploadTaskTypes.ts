import type { Guid } from "../api/layoutsApi";
import type { NodeFileManifestDto } from "../api/nodesApi";
import type { SharedUploadContext } from "./types";

export type UploadTaskStatus =
  "queued" | "uploading" | "finalizing" | "completed" | "failed";

export interface UploadTask {
  id: string;
  nodeId: Guid;
  nodeLabel: string;
  fileName: string;
  bytesTotal: number;
  bytesUploaded: number;
  progress01: number;
  status: UploadTaskStatus;
  error?: string;
  errorKey?: string;
  errorParams?: Record<string, string | number>;
  uploadSpeedBytesPerSec?: number;
  completedAt?: number;
}

export interface UploadTaskInternal extends UploadTask {
  _file?: File;
  _sharedUpload?: SharedUploadContext;
  _encrypt: boolean;
  _replaceNodeFileId?: Guid | null;
  _onFileUploaded?: (file: NodeFileManifestDto) => void;
  _startedAt?: number;
  _sawProgress?: boolean;
  _laneProbeConsumed?: boolean;
  _laneProbeTimeout?: ReturnType<typeof setTimeout>;
  _bytesProcessedForSpeed?: number;
  _quotaReservationBytes?: number;
}

export interface EnqueueOptions {
  sharedUpload?: SharedUploadContext;
  encrypt?: boolean;
  onFileUploaded?: (file: NodeFileManifestDto) => void;
}

export interface UploadFilePickerContext {
  onFilesSelected?: (files: FileList | File[]) => void;
  nodeId: Guid;
  nodeLabel: string;
  multiple?: boolean;
  accept?: string;
}
