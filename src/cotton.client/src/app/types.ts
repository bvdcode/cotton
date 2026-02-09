import type { JSX } from "react";

export type RouteConfig = {
  path: string;
  displayName?: string;
  translationKey?: string;
  element: JSX.Element;
  protected?: boolean;
  icon?: JSX.Element;
};
