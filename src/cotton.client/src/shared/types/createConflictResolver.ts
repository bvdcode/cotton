import { ConflictAction, type NameConflictPrompt } from "./nameConflict";

export const createConflictResolver = (
  confirm: (prompt: NameConflictPrompt) => Promise<ConflictAction>,
): ((prompt: NameConflictPrompt) => Promise<ConflictAction>) => {
  let remainingAction: ConflictAction | null = null;

  return async (prompt) => {
    if (remainingAction === ConflictAction.SkipAll) {
      return ConflictAction.SkipAll;
    }
    if (
      remainingAction === ConflictAction.OverwriteAll &&
      prompt.canOverwrite
    ) {
      return ConflictAction.Overwrite;
    }

    const action = await confirm(prompt);
    switch (action) {
      case ConflictAction.OverwriteAll:
        remainingAction = action;
        return ConflictAction.Overwrite;
      case ConflictAction.SkipAll:
        remainingAction = action;
        return action;
      case ConflictAction.Cancel:
      case ConflictAction.Skip:
      case ConflictAction.Rename:
      case ConflictAction.Overwrite:
        return action;
    }
  };
};
