import { useCallback, useState } from "react";

export const useDeepSearchMode = () => {
  const [mode, setMode] = useState({
    deep: false,
    allowAutomatic: true,
  });

  const toggleDeep = useCallback(() => {
    setMode((current) => ({
      deep: !current.deep,
      allowAutomatic: false,
    }));
  }, []);

  const enableDeep = useCallback(() => {
    setMode((current) => {
      if (!current.allowAutomatic) {
        return current;
      }

      return { ...current, deep: true };
    });
  }, []);

  return {
    deep: mode.deep,
    toggleDeep,
    enableDeep,
  };
};
