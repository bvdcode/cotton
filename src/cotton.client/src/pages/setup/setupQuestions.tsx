import { type ReactNode } from "react";
import { t } from "i18next";
import PhotoLibraryIcon from "@mui/icons-material/PhotoLibrary";
import DescriptionIcon from "@mui/icons-material/Description";
import MovieIcon from "@mui/icons-material/Movie";
import CodeIcon from "@mui/icons-material/Code";
import {
  AssistWalker,
  AttachEmail,
  AutoFixHigh,
  Cloud,
  CloudDone,
  CloudSync,
  Computer,
  Diversity1,
  Diversity3,
  Folder,
  Memory,
  PsychologyAlt,
} from "@mui/icons-material";

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
  description?: () => string;
  icon?: ReactNode;
};

export type SetupTextFieldOption = {
  key: string;
  label: () => string;
  placeholder?: () => string;
  type?: "text" | "password" | "url" | "boolean";
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
      requires?: string;
    }
  | {
      key: string;
      type: "multi";
      title: () => string;
      subtitle: () => string;
      linkUrl?: string;
      linkAria?: () => string;
      options: SetupMultiOption[];
      requires?: string;
    }
  | {
      key: string;
      type: "form";
      title: () => string;
      subtitle: () => string;
      fields: SetupTextFieldOption[];
      requires?: string;
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
        icon: <Diversity1 />,
      },
      {
        key: "many",
        label: () => t("setup:questions.multiuser.options.many"),
        description: () => t("setup:questions.multiuser.descriptions.many"),
        value: false,
        icon: <Diversity3 />,
      },
      {
        key: "unknown",
        label: () => t("setup:questions.multiuser.options.unknown"),
        description: () => t("setup:questions.multiuser.descriptions.unknown"),
        value: false,
        icon: <PsychologyAlt />,
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
        description: () => t("setup:questions.usage.descriptions.photos"),
      },
      {
        key: "documents",
        label: () => t("setup:questions.usage.options.documents"),
        icon: <DescriptionIcon />,
        description: () => t("setup:questions.usage.descriptions.documents"),
      },
      {
        key: "media",
        label: () => t("setup:questions.usage.options.media"),
        icon: <MovieIcon />,
        description: () => t("setup:questions.usage.descriptions.media"),
      },
      {
        key: "other",
        label: () => t("setup:questions.usage.options.other"),
        icon: <CodeIcon />,
        description: () => t("setup:questions.usage.descriptions.other"),
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
    requires: "storage:s3",
    title: () => t("setup:questions.s3Config.title"),
    subtitle: () => t("setup:questions.s3Config.subtitle"),
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
        description: () => t("setup:questions.telemetry.descriptions.deny"),
        value: false,
        icon: <AssistWalker />,
      },
      {
        key: "allow",
        label: () => t("setup:questions.telemetry.options.allow"),
        description: () => t("setup:questions.telemetry.descriptions.allow"),
        value: true,
        icon: <AutoFixHigh />,
      },
    ],
  },
  {
    key: "email",
    type: "single",
    title: () => t("setup:questions.email.title"),
    subtitle: () => t("setup:questions.email.subtitle"),
    options: [
      {
        key: "cloud",
        label: () => t("setup:questions.email.options.cloud"),
        description: () => t("setup:questions.email.descriptions.cloud"),
        value: "cloud",
        icon: <CloudDone />,
      },
      {
        key: "custom",
        label: () => t("setup:questions.email.options.custom"),
        description: () => t("setup:questions.email.descriptions.custom"),
        value: "custom",
        icon: <AttachEmail />,
      },
    ],
  },
  {
    key: "emailConfig",
    type: "form",
    requires: "email:custom",
    title: () => t("setup:questions.emailConfig.title"),
    subtitle: () => t("setup:questions.emailConfig.subtitle"),
    fields: [
      {
        key: "smtpServer",
        label: () => t("setup:questions.emailConfig.fields.smtpServer"),
        placeholder: () =>
          t("setup:questions.emailConfig.placeholders.smtpServer"),
        type: "text",
      },
      {
        key: "port",
        label: () => t("setup:questions.emailConfig.fields.port"),
        placeholder: () => t("setup:questions.emailConfig.placeholders.port"),
        type: "text",
      },
      {
        key: "username",
        label: () => t("setup:questions.emailConfig.fields.username"),
        placeholder: () =>
          t("setup:questions.emailConfig.placeholders.username"),
        type: "text",
      },
      {
        key: "password",
        label: () => t("setup:questions.emailConfig.fields.password"),
        placeholder: () =>
          t("setup:questions.emailConfig.placeholders.password"),
        type: "password",
      },
      {
        key: "fromAddress",
        label: () => t("setup:questions.emailConfig.fields.fromAddress"),
        placeholder: () =>
          t("setup:questions.emailConfig.placeholders.fromAddress"),
        type: "text",
      },
      {
        key: "useSSL",
        label: () => t("setup:questions.emailConfig.fields.useSSL"),
        placeholder: () => t("setup:questions.emailConfig.placeholders.useSSL"),
        type: "boolean",
      },
    ],
  },
  {
    key: "ai",
    type: "single",
    title: () => t("setup:questions.ai.title"),
    subtitle: () => t("setup:questions.ai.subtitle"),
    options: [
      {
        key: "local",
        label: () => t("setup:questions.ai.options.local"),
        description: () => t("setup:questions.ai.descriptions.local"),
        value: "local",
        icon: <Computer />,
      },
      {
        key: "runner",
        label: () => t("setup:questions.ai.options.runner"),
        description: () => t("setup:questions.ai.descriptions.runner"),
        value: "runner",
        icon: <Memory />,
      },
      {
        key: "cloud",
        label: () => t("setup:questions.ai.options.cloud"),
        description: () => t("setup:questions.ai.descriptions.cloud"),
        value: "cloud",
        icon: <CloudSync />,
      },
    ],
  },
];
