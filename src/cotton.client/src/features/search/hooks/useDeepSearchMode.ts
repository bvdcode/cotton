import { useCallback, useState } from "react";

export const useDeepSearchMode = (searchKey: string) => {
  const [mode, setMode] = useState({
    searchKey,
    deep: false,
    allowAutomatic: true,
  });

  if (mode.searchKey !== searchKey) {
    setMode({ searchKey, deep: false, allowAutomatic: true });
  }

  const toggleDeep = useCallback(() => {
    setMode((current) => ({
      searchKey,
      deep: !current.deep,
      allowAutomatic: false,
    }));
  }, [searchKey]);

  const enableDeep = useCallback(() => {
    setMode((current) => {
      if (current.searchKey !== searchKey || !current.allowAutomatic) {
        return current;
      }

      return { ...current, deep: true };
    });
  }, [searchKey]);

  return {
    deep: mode.searchKey === searchKey && mode.deep,
    toggleDeep,
    enableDeep,
  };
};
