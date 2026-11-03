import { createConflictResolver } from "../../../shared/types/createConflictResolver";
import {
  ConflictAction,
  type NameConflictPrompt,
} from "../../../shared/types/nameConflict";

export function createUploadConflictResolver(
  skipAll: { current: boolean },
  showDialog: (prompt: NameConflictPrompt) => Promise<ConflictAction>,
) {
  return createConflictResolver(async (prompt) => {
    if (skipAll.current) {
      return ConflictAction.SkipAll;
    }
    const action = await showDialog(prompt);
    if (action === ConflictAction.SkipAll) {
      skipAll.current = true;
    }
    return action;
  });
}
