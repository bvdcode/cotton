import { z } from "zod";
import { GUID_PATTERN } from "../../utils/guid";

const makeEnumSchema = <T extends string>(values: readonly T[], fallback: T) =>
  z.unknown().transform((value): T => {
    if (typeof value === "number" && Number.isInteger(value)) {
      return values[value] ?? fallback;
    }

    if (typeof value !== "string") {
      return fallback;
    }

    const match = values.find(
      (entry) => entry.toLowerCase() === value.toLowerCase(),
    );
    return match ?? fallback;
  });

const nullableStringSchema = z.string().nullable().optional();
const configStringSchema = nullableStringSchema.transform((value) => value ?? "");

export const storageTypeValues = ["Local", "S3"] as const;
export const emailModeValues = ["None", "Cloud", "Custom"] as const;
export const computionModeValues = ["Local", "Cloud", "Remote"] as const;
export const storageSpaceModeValues = [
  "Optimal",
  "Limited",
  "Unlimited",
] as const;
export const geoIpLookupModeValues = [
  "Disabled",
  "CottonCloud",
  "MaxMindLocal",
  "CustomHttp",
] as const;
export const serverUsageValues = [
  "Other",
  "Photos",
  "Documents",
  "Media",
] as const;

export type StorageType = (typeof storageTypeValues)[number];
export type EmailMode = (typeof emailModeValues)[number];
export type ComputionMode = (typeof computionModeValues)[number];
export type StorageSpaceMode = (typeof storageSpaceModeValues)[number];
export type GeoIpLookupMode = (typeof geoIpLookupModeValues)[number];
export type ServerUsage = (typeof serverUsageValues)[number];

export const storageTypeSchema = makeEnumSchema(storageTypeValues, "Local");
export const emailModeSchema = makeEnumSchema(emailModeValues, "None");
export const computionModeSchema = makeEnumSchema(computionModeValues, "Local");
export const storageSpaceModeSchema = makeEnumSchema(
  storageSpaceModeValues,
  "Optimal",
);
export const geoIpLookupModeSchema = makeEnumSchema(
  geoIpLookupModeValues,
  "Disabled",
);
export const serverUsageSchema = makeEnumSchema(serverUsageValues, "Other");

export const publicServerInfoSchema = z.object({
  canCreateInitialAdmin: z.boolean(),
  instanceIdHash: z.string(),
  product: z.string(),
});
export type PublicServerInfo = z.infer<typeof publicServerInfoSchema>;

export const setupStatusSchema = z.object({
  isServerInitialized: z.boolean(),
});

const defaultSupportedMaxChunkSizeBytes = [
  4 * 1024 * 1024,
  8 * 1024 * 1024,
  16 * 1024 * 1024,
];

const chunkSizeObjectSchema = z.object({
  maxChunkSizeBytes: z.number().finite(),
  supportedMaxChunkSizeBytes: z.array(z.number().finite()).optional(),
});

export const chunkSizeSettingsResponseSchema = z
  .union([z.number().finite(), chunkSizeObjectSchema])
  .transform((value) => {
    const maxChunkSizeBytes =
      typeof value === "number" ? value : value.maxChunkSizeBytes;
    const supportedMaxChunkSizeBytes =
      typeof value === "number"
        ? defaultSupportedMaxChunkSizeBytes
        : value.supportedMaxChunkSizeBytes;
    const normalizedSupportedMaxChunkSizeBytes = Array.from(
      new Set(
        (supportedMaxChunkSizeBytes ?? defaultSupportedMaxChunkSizeBytes)
          .filter((entry) => Number.isFinite(entry) && entry > 0),
      ),
    ).sort((left, right) => left - right);

    return {
      maxChunkSizeBytes,
      supportedMaxChunkSizeBytes:
        normalizedSupportedMaxChunkSizeBytes.length > 0
          ? normalizedSupportedMaxChunkSizeBytes
          : defaultSupportedMaxChunkSizeBytes,
    };
  });
export type ChunkSizeSettings = z.infer<typeof chunkSizeSettingsResponseSchema>;

export const chunkSizeResponseSchema = chunkSizeSettingsResponseSchema
  .transform((value): number => value.maxChunkSizeBytes);

const selectSupportedHashAlgorithm = (value: {
  supportedHashAlgorithms?: string[];
  supportedHashAlgorithm?: string | null;
}): string | null => {
  const fromList = value.supportedHashAlgorithms?.find(
    (entry) => entry.trim().length > 0,
  );
  if (fromList) {
    return fromList.trim();
  }

  const fromSingular = value.supportedHashAlgorithm?.trim();
  return fromSingular && fromSingular.length > 0 ? fromSingular : null;
};

export const serverSettingsResponseSchema = z
  .object({
    version: nullableStringSchema,
    maxChunkSizeBytes: z.number().finite(),
    supportedHashAlgorithms: z.array(z.string()).optional(),
    supportedHashAlgorithm: nullableStringSchema,
  })
  .superRefine((value, context) => {
    if (!selectSupportedHashAlgorithm(value)) {
      context.addIssue({
        code: "custom",
        message:
          "settings response must contain at least one supported hash algorithm",
        path: ["supportedHashAlgorithms"],
      });
    }
  })
  .transform((value) => ({
    version:
      typeof value.version === "string" && value.version.trim().length > 0
        ? value.version.trim()
        : null,
    maxChunkSizeBytes: value.maxChunkSizeBytes,
    supportedHashAlgorithm: selectSupportedHashAlgorithm(value) ?? "",
  }));
export type ServerSettings = z.infer<typeof serverSettingsResponseSchema>;

export const telemetrySettingSchema = z
  .object({ telemetryEnabled: z.boolean() })
  .transform((value) => value.telemetryEnabled);

export const allowCrossUserDeduplicationSchema = z
  .object({ allowCrossUserDeduplication: z.boolean() })
  .transform((value) => value.allowCrossUserDeduplication);

export const allowGlobalIndexingSchema = z
  .object({ allowGlobalIndexing: z.boolean() })
  .transform((value) => value.allowGlobalIndexing);

export const serverUsageListSchema = z
  .object({ serverUsage: z.array(z.unknown()).optional() })
  .transform((value): ServerUsage[] => {
    const raw = value.serverUsage;
    if (!Array.isArray(raw) || raw.length === 0) {
      return ["Other"];
    }

    return raw.map((entry) => serverUsageSchema.parse(entry));
  });

export const timezoneSchema = z
  .object({ timezone: nullableStringSchema })
  .transform((value) => value.timezone ?? "UTC");

export const publicBaseUrlSchema = z
  .object({ publicBaseUrl: nullableStringSchema })
  .transform((value) => value.publicBaseUrl ?? "");

export const storageSpaceModeResponseSchema = z
  .object({ storageSpaceMode: z.unknown().optional() })
  .transform((value) => storageSpaceModeSchema.parse(value.storageSpaceMode));

export const defaultUserStorageQuotaBytesSchema = z
  .object({
    defaultUserStorageQuotaBytes: z.number().finite().nonnegative().nullable().optional(),
  })
  .transform((value) => value.defaultUserStorageQuotaBytes ?? null);

export const defaultUserTemplateNodeIdSchema = z
  .object({
    defaultUserTemplateNodeId: z.string().regex(GUID_PATTERN).nullable().optional(),
  })
  .transform((value) => value.defaultUserTemplateNodeId ?? null);

export const computionModeResponseSchema = z
  .object({ computionMode: z.unknown().optional() })
  .transform((value) => computionModeSchema.parse(value.computionMode));

export const storageTypeResponseSchema = z
  .object({ storageType: z.unknown().optional() })
  .transform((value) => storageTypeSchema.parse(value.storageType));

export const emailModeResponseSchema = z
  .object({ emailMode: z.unknown().optional() })
  .transform((value) => emailModeSchema.parse(value.emailMode));

export const geoIpLookupModeResponseSchema = z
  .object({ geoIpLookupMode: z.unknown().optional() })
  .transform((value) => geoIpLookupModeSchema.parse(value.geoIpLookupMode));

export const customGeoIpLookupUrlSchema = z
  .object({ customGeoIpLookupUrl: nullableStringSchema })
  .transform((value) => value.customGeoIpLookupUrl ?? "");

export const s3ConfigSchema = z.object({
  accessKey: configStringSchema,
  secretKey: configStringSchema,
  endpoint: configStringSchema,
  region: configStringSchema,
  bucket: configStringSchema,
});
export type S3Config = z.infer<typeof s3ConfigSchema>;

export const emailConfigSchema = z.object({
  username: configStringSchema,
  password: configStringSchema,
  smtpServer: configStringSchema,
  port: configStringSchema,
  fromAddress: configStringSchema,
  useSSL: z.boolean().optional().default(false),
});
export type EmailConfig = z.infer<typeof emailConfigSchema>;
