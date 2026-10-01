export interface UseBreadcrumb {
  id: string;
  name: string;
}

export type DropPreparationPhase = "idle" | "scanning" | "preparing";

export type DropPreparationStep =
  "idle" | "scanning" | "mapping" | "folders" | "conflicts" | "enqueue";

export interface DropPreparationState {
  active: boolean;
  phase: DropPreparationPhase;
  step: DropPreparationStep;
  filesFound: number;
  processed: number;
}

export interface SkippedItemsDialogState {
  open: boolean;
  total: number;
  items: string[];
  truncated: boolean;
}

export const emptySkippedItemsDialog: SkippedItemsDialogState = {
  open: false,
  total: 0,
  items: [],
  truncated: false,
};
