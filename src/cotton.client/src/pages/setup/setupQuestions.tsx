import { type ReactNode } from "react";
import { t } from "i18next";
import PhotoLibraryIcon from "@mui/icons-material/PhotoLibrary";
import DescriptionIcon from "@mui/icons-material/Description";
import MovieIcon from "@mui/icons-material/Movie";
import CodeIcon from "@mui/icons-material/Code";

export type SetupSingleOption<T> = {
  key: string;
  label: () => string;
  description?: () => string;
  value: T;
  icon?: ReactNode;
};

export type SetupMultiOption = {
  key: string;
  label: () => string;
  icon?: ReactNode;
};

export type SetupStepDefinition =
  | {
      key: "multiuser" | "telemetry";
      type: "single";
      title: () => string;
      subtitle: () => string;
      linkUrl?: string;
      linkAria?: () => string;
      options: SetupSingleOption<boolean>[];
    }
  | {
      key: "usage";
      type: "multi";
      title: () => string;
      subtitle: () => string;
      linkUrl?: string;
      linkAria?: () => string;
      options: SetupMultiOption[];
    };

export const setupStepDefinitions: SetupStepDefinition[] = [
  {
    key: "multiuser",
    type: "single",
    title: () => t("setup:questions.multiuser.title"),
    subtitle: () => t("setup:questions.multiuser.subtitle"),
    linkUrl: "https://github.com/bvdcode/cotton",
    linkAria: () => t("setup:questions.multiuser.linkAria"),
    options: [
      {
        key: "family",
        label: () => t("setup:questions.multiuser.options.family"),
        description: () => t("setup:questions.multiuser.descriptions.family"),
        value: true,
      },
      {
        key: "many",
        label: () => t("setup:questions.multiuser.options.many"),
        description: () => t("setup:questions.multiuser.descriptions.many"),
        value: false,
      },
      {
        key: "unknown",
        label: () => t("setup:questions.multiuser.options.unknown"),
        description: () => t("setup:questions.multiuser.descriptions.unknown"),
        value: false,
      },
    ],
  },
  {
    key: "usage",
    type: "multi",
    title: () => t("setup:questions.usage.title"),
    subtitle: () => t("setup:questions.usage.subtitle"),
    options: [
      {
        key: "photos",
        label: () => t("setup:questions.usage.options.photos"),
        icon: <PhotoLibraryIcon />,
      },
      {
        key: "documents",
        label: () => t("setup:questions.usage.options.documents"),
        icon: <DescriptionIcon />,
      },
      {
        key: "media",
        label: () => t("setup:questions.usage.options.media"),
        icon: <MovieIcon />,
      },
      {
        key: "other",
        label: () => t("setup:questions.usage.options.other"),
        icon: <CodeIcon />,
      },
    ],
  },
  {
    key: "telemetry",
    type: "single",
    title: () => t("setup:questions.telemetry.title"),
    subtitle: () => t("setup:questions.telemetry.subtitle"),
    options: [
      {
        key: "deny",
        label: () => t("setup:questions.telemetry.options.deny"),
        value: false,
      },
      {
        key: "allow",
        label: () => t("setup:questions.telemetry.options.allow"),
        value: true,
      },
    ],
  },
];
