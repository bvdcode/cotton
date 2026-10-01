import type { useTranslation } from "react-i18next";
import type { MoveClipboardItem } from "../store/moveClipboardStore";
import type { MoveConflictResolver } from "../move/moveConflictResolution";
import type { ExistingFileEncryptionTaskFile } from "../tasks";

export interface MoveOutcome {
  succeeded: ReadonlyArray<MoveClipboardItem>;
  failed: ReadonlyArray<MoveClipboardItem>;
  notMoved: ReadonlyArray<MoveClipboardItem>;
  lastErrorMessage: string | null;
}

export interface MoveExecutionResult extends MoveOutcome {
  movedFilesToEncrypt: ReadonlyArray<MoveClipboardItem>;
  movedFilesToOfferDecrypt: ReadonlyArray<MoveClipboardItem>;
  sourceParents: ReadonlySet<string>;
}

export interface UseMoveOperationsOptions {
  confirmConflict: MoveConflictResolver;
}

export interface EncryptionCandidate {
  file: ExistingFileEncryptionTaskFile;
  targetNodeId: string;
}

export interface EncryptionCandidateScanResult {
  candidates: EncryptionCandidate[];
  incomplete: boolean;
}

export type MoveTranslation = ReturnType<
  typeof useTranslation<["files", "common", "tasks"]>
>["t"];

export interface UseMoveOperationsResult {
  cutItems: (items: ReadonlyArray<MoveClipboardItem>) => void;
  clearClipboard: () => void;
  /** Paste current clipboard contents into the target parent. */
  pasteInto: (targetParentId: string) => Promise<void>;
  /** Move arbitrary items (e.g. from drag-and-drop) into the target parent. */
  moveItems: (
    items: ReadonlyArray<MoveClipboardItem>,
    targetParentId: string,
  ) => Promise<void>;
}
