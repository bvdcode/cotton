import { settingsApi } from "../../shared/api/settingsApi";
import { setupStepDefinitions } from "./setupQuestions.tsx";
import type { JsonValue } from "../../shared/types/json";

export function convertAnswersToValues(
  answers: Record<string, JsonValue>,
): Record<string, JsonValue> {
  const converted: Record<string, JsonValue> = {};

  for (const [questionKey, answer] of Object.entries(answers)) {
    const stepDef = setupStepDefinitions.find((s) => s.key === questionKey);

    if (!stepDef) {
      // Keep as-is if not found (form fields, etc)
      converted[questionKey] = answer;
      continue;
    }

    if (questionKey === "trustedMode") {
      converted[questionKey] = answer;
    } else if (stepDef.type === "single" && typeof answer === "string") {
      // Find the option and get its value
      const options =
        "getOptions" in stepDef && stepDef.getOptions
          ? stepDef.getOptions()
          : stepDef.options;
      const option = options.find((opt) => opt.key === answer);
      const value = option?.value;
      if (
        value === null ||
        typeof value === "string" ||
        typeof value === "number" ||
        typeof value === "boolean"
      ) {
        converted[questionKey] = value;
      } else {
        converted[questionKey] = answer;
      }
    } else {
      // Keep as-is for multi, form types
      converted[questionKey] = answer;
    }
  }

  return converted;
}

export const hasWizardAnswer = (answer: JsonValue | undefined): boolean => {
  if (answer === undefined || answer === null) {
    return false;
  }

  if (typeof answer === "string") {
    return answer.trim().length > 0;
  }

  if (typeof answer === "number") {
    return true;
  }

  if (typeof answer === "boolean") {
    return answer;
  }

  if (Array.isArray(answer)) {
    return answer.length > 0;
  }

  return Object.values(answer).some((value) => hasWizardAnswer(value));
};

const toUsageAnswerKeys = (
  usage: Awaited<ReturnType<typeof settingsApi.getServerUsage>>,
): string[] => usage.map((value) => value.toLowerCase());

const toGeoIpLookupAnswerKey = (
  mode: Awaited<ReturnType<typeof settingsApi.getGeoIpLookupMode>>,
): string => {
  if (mode === "CottonCloud") return "cottonCloud";
  if (mode === "MaxMindLocal") return "local";
  if (mode === "CustomHttp") return "custom";
  return "disabled";
};

const toEmailAnswerKey = (
  mode: Awaited<ReturnType<typeof settingsApi.getEmailMode>>,
): string => {
  if (mode === "Cloud") return "cloud";
  if (mode === "Custom") return "custom";
  return "none";
};

export const loadSetupStepPrefill = async (
  stepKey: string,
): Promise<JsonValue | undefined> => {
  if (stepKey === "usage") {
    const usage = toUsageAnswerKeys(await settingsApi.getServerUsage());
    return usage.length > 0 ? usage : undefined;
  }

  if (stepKey === "telemetry") {
    return (await settingsApi.getTelemetry()) ? "allow" : "deny";
  }

  if (stepKey === "geoIpLookupMode") {
    return toGeoIpLookupAnswerKey(await settingsApi.getGeoIpLookupMode());
  }

  if (stepKey === "customGeoIpLookupUrl") {
    const url = (await settingsApi.getCustomGeoIpLookupUrl()).trim();
    return url ? { url } : undefined;
  }

  if (stepKey === "s3Config") {
    const config = await settingsApi.getS3Config();
    const answer: Record<string, JsonValue> = {
      endpoint: config.endpoint,
      region: config.region,
      bucket: config.bucket,
      accessKey: config.accessKey,
      secretKey: config.secretKey,
    };
    return hasWizardAnswer(answer) ? answer : undefined;
  }

  if (stepKey === "emailConfig") {
    const config = await settingsApi.getEmailConfig();
    const answer: Record<string, JsonValue> = {
      smtpServer: config.smtpServer,
      port: config.port,
      username: config.username,
      password: config.password,
      fromAddress: config.fromAddress,
      useSSL: config.useSSL,
    };
    return hasWizardAnswer(answer) ? answer : undefined;
  }

  if (stepKey === "email") {
    return toEmailAnswerKey(await settingsApi.getEmailMode());
  }

  if (stepKey === "remoteComputationRunnerUrl") {
    const url = (await settingsApi.getRemoteComputationRunnerUrl()).trim();
    return url ? { url } : undefined;
  }

  return undefined;
};
