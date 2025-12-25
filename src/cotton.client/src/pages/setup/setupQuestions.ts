export type SetupSingleOption<T> = {
  key: string;
  labelKey: string;
  descriptionKey?: string;
  value: T;
};

export type SetupMultiOption = {
  key: string;
  labelKey: string;
};

export type SetupStepDefinition =
  | {
      key: "multiuser" | "telemetry";
      type: "single";
      titleKey: string;
      subtitleKey: string;
      linkUrl?: string;
      linkAriaKey?: string;
      options: SetupSingleOption<boolean>[];
    }
  | {
      key: "usage";
      type: "multi";
      titleKey: string;
      subtitleKey: string;
      linkUrl?: string;
      linkAriaKey?: string;
      options: SetupMultiOption[];
    };

export const setupStepDefinitions: SetupStepDefinition[] = [
  {
    key: "multiuser",
    type: "single",
    titleKey: "questions.multiuser.title",
    subtitleKey: "questions.multiuser.subtitle",
    linkUrl: "https://github.com/bvdcode/cotton",
    linkAriaKey: "questions.multiuser.linkAria",
    options: [
      {
        key: "family",
        labelKey: "questions.multiuser.options.family",
        descriptionKey: "questions.multiuser.descriptions.family",
        value: true,
      },
      {
        key: "many",
        labelKey: "questions.multiuser.options.many",
        descriptionKey: "questions.multiuser.descriptions.many",
        value: false,
      },
      {
        key: "unknown",
        labelKey: "questions.multiuser.options.unknown",
        descriptionKey: "questions.multiuser.descriptions.unknown",
        value: false,
      },
    ],
  },
  {
    key: "usage",
    type: "multi",
    titleKey: "questions.usage.title",
    subtitleKey: "questions.usage.subtitle",
    options: [
      { key: "photos", labelKey: "questions.usage.options.photos" },
      { key: "documents", labelKey: "questions.usage.options.documents" },
      { key: "media", labelKey: "questions.usage.options.media" },
      { key: "other", labelKey: "questions.usage.options.other" },
    ],
  },
  {
    key: "telemetry",
    type: "single",
    titleKey: "questions.telemetry.title",
    subtitleKey: "questions.telemetry.subtitle",
    options: [
      {
        key: "deny",
        labelKey: "questions.telemetry.options.deny",
        value: false,
      },
      {
        key: "allow",
        labelKey: "questions.telemetry.options.allow",
        value: true,
      },
    ],
  },
];
