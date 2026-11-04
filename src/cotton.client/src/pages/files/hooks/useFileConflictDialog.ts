import { useRef, useState, useCallback } from "react";
import {
  ConflictAction,
  type NameConflictPrompt,
} from "../../../shared/types/nameConflict";

interface ConflictDialogState extends NameConflictPrompt {
  open: boolean;
}

const closedState: ConflictDialogState = {
  open: false,
  newName: "",
  canOverwrite: false,
  canMerge: false,
  canApplyToAll: false,
};

export const useFileConflictDialog = () => {
  const [dialogState, setDialogState] = useState(closedState);

  const resolveRef = useRef<((action: ConflictAction) => void) | null>(null);
  const pendingActionRef = useRef<ConflictAction | null>(null);

  const showConflictDialog = useCallback(
    (prompt: NameConflictPrompt): Promise<ConflictAction> => {
      return new Promise<ConflictAction>((resolve) => {
        resolveRef.current = resolve;
        setDialogState({
          ...prompt,
          open: true,
        });
      });
    },
    [],
  );

  const handleResolve = useCallback((action: ConflictAction) => {
    pendingActionRef.current = action;
    setDialogState((prev) => ({ ...prev, open: false }));
  }, []);

  const handleExited = useCallback(() => {
    const pendingResolve = resolveRef.current;
    const pendingAction = pendingActionRef.current;

    resolveRef.current = null;
    pendingActionRef.current = null;
    setDialogState(closedState);

    if (pendingResolve && pendingAction) {
      pendingResolve(pendingAction);
    }
  }, []);

  return {
    dialogState,
    showConflictDialog,
    handleResolve,
    handleExited,
  };
};
