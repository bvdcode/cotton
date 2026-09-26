import { t } from "i18next";
import PhotoLibraryIcon from "@mui/icons-material/PhotoLibrary";
import DescriptionIcon from "@mui/icons-material/Description";
import MovieIcon from "@mui/icons-material/Movie";
import {
  AutoFixHigh,
  Block,
  Cloud,
  Computer,
  Diversity1,
  Diversity3,
  Folder,
  FolderCopy,
  Http,
  PrivacyTip,
  PsychologyAlt,
  TravelExplore,
} from "@mui/icons-material";
import { TelemetryHelpButton } from "../../shared/ui/TelemetryHelpButton";
import type { SetupStepDefinition } from "./setupModels";

export const setupBasicsQuestions: SetupStepDefinition[] = [
  {
    key: "trustedMode",
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
        icon: <FolderCopy />,
        description: () => t("setup:questions.usage.descriptions.other"),
      },
    ],
  },
  {
    key: "telemetry",
    type: "single",
    title: () => t("setup:questions.telemetry.title"),
    subtitle: () => t("setup:questions.telemetry.subtitle"),
    extraHeader: () => <TelemetryHelpButton />,
    options: [
      {
        key: "allow",
        label: () => t("setup:questions.telemetry.options.allow"),
        description: () => t("setup:questions.telemetry.descriptions.allow"),
        value: true,
        icon: <AutoFixHigh />,
      },
      {
        key: "deny",
        label: () => t("setup:questions.telemetry.options.deny"),
        description: () => t("setup:questions.telemetry.descriptions.deny"),
        value: false,
        icon: <PrivacyTip />,
        disabledIfAny: [
          "geoIpLookupMode:cottonCloud",
          "email:cloud",
          "computionMode:cloud",
        ],
      },
    ],
  },
  {
    key: "geoIpLookupMode",
    type: "single",
    title: () => t("setup:questions.geoIpLookup.title"),
    subtitle: () => t("setup:questions.geoIpLookup.subtitle"),
    options: [
      {
        key: "disabled",
        label: () => t("setup:questions.geoIpLookup.options.disabled"),
        description: () =>
          t("setup:questions.geoIpLookup.descriptions.disabled"),
        value: "disabled",
        icon: <Block />,
      },
      {
        key: "cottonCloud",
        label: () => t("setup:questions.geoIpLookup.options.cottonCloud"),
        description: () =>
          t("setup:questions.geoIpLookup.descriptions.cottonCloud"),
        value: "cottonCloud",
        icon: <TravelExplore />,
        requires: "telemetry:allow",
      },
      {
        key: "local",
        label: () => t("setup:questions.geoIpLookup.options.local"),
        description: () => t("setup:questions.geoIpLookup.descriptions.local"),
        value: "maxMindLocal",
        icon: <Computer />,
      },
      {
        key: "custom",
        label: () => t("setup:questions.geoIpLookup.options.custom"),
        description: () => t("setup:questions.geoIpLookup.descriptions.custom"),
        value: "customHttp",
        icon: <Http />,
      },
    ],
  },
  {
    key: "customGeoIpLookupUrl",
    type: "form",
    requires: "geoIpLookupMode:custom",
    title: () => t("setup:questions.customGeoIpLookupUrl.title"),
    subtitle: () => t("setup:questions.customGeoIpLookupUrl.subtitle"),
    fields: [
      {
        key: "url",
        label: () => t("setup:questions.customGeoIpLookupUrl.fields.url"),
        placeholder: () =>
          t("setup:questions.customGeoIpLookupUrl.placeholders.url"),
        type: "url",
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
];
