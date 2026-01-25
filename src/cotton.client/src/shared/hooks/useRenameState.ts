import { useState } from "react";

/**
 * Shared state and handlers for renaming folders/files
 */
export interface RenameState {
  renamingId: string | null;
  renamingName: string;
  originalName: string;
}

export interface RenameHandlers {
  startRename: (id: string, currentName: string) => void;
  confirmRename: (
    performRename: (id: string, newName: string) => Promise<boolean | void>,
  ) => Promise<void>;
  cancelRename: () => void;
  setRenamingName: (name: string) => void;
}

/**
 * Hook for managing rename operations state
 */
export const useRenameState = (): [RenameState, RenameHandlers] => {
  const [renamingId, setRenamingId] = useState<string | null>(null);
  const [renamingName, setRenamingName] = useState("");
  const [originalName, setOriginalName] = useState("");

  const startRename = (id: string, currentName: string) => {
    setRenamingId(id);
    setRenamingName(currentName);
    setOriginalName(currentName);
  };

  const confirmRename = async (
    performRename: (id: string, newName: string) => Promise<boolean | void>,
  ) => {
    if (!renamingId || renamingName.trim().length === 0) {
      setRenamingId(null);
      setRenamingName("");
      setOriginalName("");
      return;
    }

    const newName = renamingName.trim();

    // No changes - just close rename mode
    if (newName === originalName) {
      setRenamingId(null);
      setRenamingName("");
      setOriginalName("");
      return;
    }

    const result = await performRename(renamingId, newName);

    // If result is undefined or true, consider it success
    if (result === undefined || result === true) {
      setRenamingId(null);
      setRenamingName("");
      setOriginalName("");
    }
  };

  const cancelRename = () => {
    setRenamingId(null);
    setRenamingName("");
    setOriginalName("");
  };

  return [
    { renamingId, renamingName, originalName },
    { startRename, confirmRename, cancelRename, setRenamingName },
  ];
};
