import { useEffect } from "react";
import { setPageTitle } from "../utils/pageTitle";

export const usePageTitle = (title?: string | null): void => {
  useEffect(() => {
    setPageTitle(title);

    return () => {
      setPageTitle();
    };
  }, [title]);
};
