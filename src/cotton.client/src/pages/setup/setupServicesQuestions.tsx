import { t } from "i18next";
import {
  AttachEmail,
  Block,
  CloudDone,
  CloudSync,
  Computer,
  Memory,
  PsychologyAlt,
  Save,
  SdStorage,
} from "@mui/icons-material";
import type { SetupStepDefinition } from "./setupModels";

export const setupServicesQuestions: SetupStepDefinition[] = [
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
        requires: "telemetry:allow",
      },
      {
        key: "custom",
        label: () => t("setup:questions.email.options.custom"),
        description: () => t("setup:questions.email.descriptions.custom"),
        value: "custom",
        icon: <AttachEmail />,
      },
      {
        key: "none",
        label: () => t("setup:questions.email.options.none"),
        description: () => t("setup:questions.email.descriptions.none"),
        value: "none",
        icon: <Block />,
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
    key: "computionMode",
    type: "single",
    title: () => t("setup:questions.ai.title"),
    subtitle: () => t("setup:questions.ai.subtitle"),
    options: [
      {
        key: "cloud",
        label: () => t("setup:questions.ai.options.cloud"),
        description: () => t("setup:questions.ai.descriptions.cloud"),
        value: "cloud",
        icon: <CloudSync />,
        requires: "telemetry:allow",
      },
      {
        key: "local",
        label: () => t("setup:questions.ai.options.local"),
        description: () => t("setup:questions.ai.descriptions.local"),
        value: "local",
        icon: <Computer />,
      },
      {
        key: "remote",
        label: () => t("setup:questions.ai.options.runner"),
        description: () => t("setup:questions.ai.descriptions.runner"),
        value: "remote",
        icon: <Memory />,
      },
    ],
  },
  {
    key: "remoteComputationRunnerUrl",
    type: "form",
    requires: "computionMode:remote",
    title: () => t("setup:questions.remoteComputationRunnerUrl.title"),
    subtitle: () => t("setup:questions.remoteComputationRunnerUrl.subtitle"),
    fields: [
      {
        key: "url",
        label: () => t("setup:questions.remoteComputationRunnerUrl.fields.url"),
        placeholder: () =>
          t("setup:questions.remoteComputationRunnerUrl.placeholders.url"),
        type: "url",
      },
    ],
  },
  {
    key: "timezone",
    type: "single",
    title: () => t("setup:questions.timezone.title"),
    subtitle: () => t("setup:questions.timezone.subtitle"),
    renderAs: "autocomplete",
    getOptions: () => {
      const timezones = Intl.supportedValuesOf("timeZone");
      return timezones.map((tz) => ({
        key: tz,
        label: () => tz,
        value: tz,
      }));
    },
    getDefaultValue: () => Intl.DateTimeFormat().resolvedOptions().timeZone,
    options: [],
  },
  {
    key: "storageSpace",
    type: "single",
    title: () => t("setup:questions.storageSpace.title"),
    subtitle: () => t("setup:questions.storageSpace.subtitle"),
    options: [
      {
        key: "unlimited",
        label: () => t("setup:questions.storageSpace.options.all"),
        description: () => t("setup:questions.storageSpace.descriptions.all"),
        value: "unlimited",
        icon: <SdStorage />,
      },
      {
        key: "optimal",
        label: () => t("setup:questions.storageSpace.options.optimal"),
        description: () =>
          t("setup:questions.storageSpace.descriptions.unknown"),
        value: "optimal",
        icon: <PsychologyAlt />,
      },
      {
        key: "limited",
        label: () => t("setup:questions.storageSpace.options.economical"),
        description: () =>
          t("setup:questions.storageSpace.descriptions.economical"),
        value: "limited",
        icon: <Save />,
      },
    ],
  },
];
