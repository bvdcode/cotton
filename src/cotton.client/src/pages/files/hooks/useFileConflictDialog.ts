import { useRef, useState, useCallback } from "react";
import { ConflictAction } from "../utils/uploadConflicts";

interface ConflictDialogState {
  open: boolean;
  newName: string;
}

export const useFileConflictDialog = () => {
  const [dialogState, setDialogState] = useState<ConflictDialogState>({
    open: false,
    newName: "",
  });

  const resolveRef = useRef<((action: ConflictAction) => void) | null>(null);
  const pendingActionRef = useRef<ConflictAction | null>(null);

  const showConflictDialog = useCallback(
    (newName: string): Promise<ConflictAction> => {
      return new Promise<ConflictAction>((resolve) => {
        resolveRef.current = resolve;
        setDialogState({ open: true, newName });
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
    setDialogState({ open: false, newName: "" });

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
