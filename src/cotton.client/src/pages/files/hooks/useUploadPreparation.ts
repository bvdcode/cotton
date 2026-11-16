import { useCallback, useRef, useState } from "react";
import type { DropPreparationState } from "./fileUploadTypes";

const idle: DropPreparationState = {
  active: false,
  phase: "idle",
  step: "idle",
  filesFound: 0,
  processed: 0,
};

export const useUploadPreparation = () => {
  const [dropPreparation, setDropPreparation] = useState(idle);
  const tail = useRef<Promise<void> | null>(null);
  const run = useCallback((prepare: () => Promise<void>) => {
    const execute = async () => {
      try {
        await prepare();
      } finally {
        setDropPreparation(idle);
      }
    };
    const operation = tail.current ? tail.current.then(execute) : execute();
    const settled = operation.then(
      () => {},
      () => {},
    );
    tail.current = settled;
    void settled.then(() => {
      if (tail.current === settled) {
        tail.current = null;
      }
    });
    return operation;
  }, []);

  return { dropPreparation, setDropPreparation, run };
};
