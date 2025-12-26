import { type ReactNode } from "react";
import { t } from "i18next";
import PhotoLibraryIcon from "@mui/icons-material/PhotoLibrary";
import DescriptionIcon from "@mui/icons-material/Description";
import MovieIcon from "@mui/icons-material/Movie";
import CodeIcon from "@mui/icons-material/Code";
import { Cloud, Folder } from "@mui/icons-material";

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

export type SetupTextFieldOption = {
  key: string;
  label: () => string;
  placeholder?: () => string;
  type?: "text" | "password" | "url";
};

export type SetupStepDefinition =
  | {
      key: string;
      type: "single";
      title: () => string;
      subtitle: () => string;
      linkUrl?: string;
      linkAria?: () => string;
      options: SetupSingleOption<unknown>[];
      showIf?: (answers: Record<string, unknown>) => boolean;
    }
  | {
      key: string;
      type: "multi";
      title: () => string;
      subtitle: () => string;
      linkUrl?: string;
      linkAria?: () => string;
      options: SetupMultiOption[];
      showIf?: (answers: Record<string, unknown>) => boolean;
    }
  | {
      key: string;
      type: "form";
      title: () => string;
      subtitle: () => string;
      fields: SetupTextFieldOption[];
      showIf?: (answers: Record<string, unknown>) => boolean;
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
    key: "storage",
    type: "single",
    title: () => t("setup:questions.storage.title"),
    subtitle: () => t("setup:questions.storage.subtitle"),
    options: [
      {
        key: "local",
        label: () => t("setup:questions.storage.options.local"),
        description: () => t("setup:questions.storage.descriptions.local"),
        value: "local",
        icon: <Folder />,
      },
      {
        key: "s3",
        label: () => t("setup:questions.storage.options.s3"),
        description: () => t("setup:questions.storage.descriptions.s3"),
        value: "s3",
        icon: <Cloud />,
      },
    ],
  },
  {
    key: "s3Config",
    type: "form",
    title: () => t("setup:questions.s3Config.title"),
    subtitle: () => t("setup:questions.s3Config.subtitle"),
    showIf: (answers) => answers.storage === "s3",
    fields: [
      {
        key: "endpoint",
        label: () => t("setup:questions.s3Config.fields.endpoint"),
        placeholder: () => t("setup:questions.s3Config.placeholders.endpoint"),
        type: "url",
      },
      {
        key: "region",
        label: () => t("setup:questions.s3Config.fields.region"),
        placeholder: () => t("setup:questions.s3Config.placeholders.region"),
        type: "text",
      },
      {
        key: "bucket",
        label: () => t("setup:questions.s3Config.fields.bucket"),
        placeholder: () => t("setup:questions.s3Config.placeholders.bucket"),
        type: "text",
      },
      {
        key: "accessKey",
        label: () => t("setup:questions.s3Config.fields.accessKey"),
        placeholder: () => t("setup:questions.s3Config.placeholders.accessKey"),
        type: "text",
      },
      {
        key: "secretKey",
        label: () => t("setup:questions.s3Config.fields.secretKey"),
        placeholder: () => t("setup:questions.s3Config.placeholders.secretKey"),
        type: "password",
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
