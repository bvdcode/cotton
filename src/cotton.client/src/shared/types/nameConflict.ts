export const ConflictAction = {
  Overwrite: "overwrite",
  OverwriteAll: "overwriteAll",
  Rename: "rename",
  Merge: "merge",
  Skip: "skip",
  SkipAll: "skipAll",
  Cancel: "cancel",
} as const;

export type ConflictAction =
  (typeof ConflictAction)[keyof typeof ConflictAction];

export interface NameConflictPrompt {
  newName: string;
  canOverwrite: boolean;
  canMerge: boolean;
  canApplyToAll: boolean;
}
