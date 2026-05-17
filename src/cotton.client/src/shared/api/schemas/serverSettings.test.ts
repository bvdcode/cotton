import { describe, expect, it } from "vitest";
import {
  chunkSizeResponseSchema,
  emailConfigSchema,
  emailModeResponseSchema,
  publicBaseUrlSchema,
  serverSettingsResponseSchema,
  serverUsageListSchema,
  s3ConfigSchema,
  storageTypeResponseSchema,
  telemetrySettingSchema,
  timezoneSchema,
} from "./serverSettings";

describe("server settings schemas", () => {
  it("parses server settings with singular or plural hash algorithm fields", () => {
    expect(
      serverSettingsResponseSchema.parse({
        version: " 1.2.3 ",
        maxChunkSizeBytes: 1024,
        supportedHashAlgorithm: " SHA256 ",
      }),
    ).toEqual({
      version: "1.2.3",
      maxChunkSizeBytes: 1024,
      supportedHashAlgorithm: "SHA256",
    });

    expect(
      serverSettingsResponseSchema.parse({
        version: "",
        maxChunkSizeBytes: 2048,
        supportedHashAlgorithms: ["", "BLAKE3"],
      }),
    ).toEqual({
      version: null,
      maxChunkSizeBytes: 2048,
      supportedHashAlgorithm: "BLAKE3",
    });
  });

  it("rejects settings without a usable hash algorithm", () => {
    expect(() =>
      serverSettingsResponseSchema.parse({
        version: null,
        maxChunkSizeBytes: 1024,
        supportedHashAlgorithms: [" "],
      }),
    ).toThrow();
  });

  it("accepts chunk size response variants", () => {
    expect(chunkSizeResponseSchema.parse(512)).toBe(512);
    expect(chunkSizeResponseSchema.parse({ maxChunkSizeBytes: 1024 })).toBe(
      1024,
    );
  });

  it("normalizes enum responses from strings and numeric indexes", () => {
    expect(storageTypeResponseSchema.parse({ storageType: "s3" })).toBe("S3");
    expect(emailModeResponseSchema.parse({ emailMode: 2 })).toBe("Custom");
    expect(emailModeResponseSchema.parse({ emailMode: "unknown" })).toBe(
      "None",
    );
  });

  it("normalizes optional settings fields", () => {
    expect(timezoneSchema.parse({ timezone: null })).toBe("UTC");
    expect(publicBaseUrlSchema.parse({ publicBaseUrl: null })).toBe("");
    expect(serverUsageListSchema.parse({ serverUsage: [] })).toEqual([
      "Other",
    ]);
  });

  it("normalizes storage and email config payloads", () => {
    expect(
      s3ConfigSchema.parse({
        accessKey: "access",
        secretKey: null,
        endpoint: "https://s3.example",
        region: undefined,
        bucket: "bucket",
      }),
    ).toEqual({
      accessKey: "access",
      secretKey: "",
      endpoint: "https://s3.example",
      region: "",
      bucket: "bucket",
    });

    expect(
      emailConfigSchema.parse({
        username: "user",
        password: null,
        smtpServer: "smtp.example",
        port: "587",
        fromAddress: undefined,
      }),
    ).toEqual({
      username: "user",
      password: "",
      smtpServer: "smtp.example",
      port: "587",
      fromAddress: "",
      useSSL: false,
    });
  });

  it("validates boolean settings", () => {
    expect(telemetrySettingSchema.parse({ telemetryEnabled: true })).toBe(true);
    expect(() =>
      telemetrySettingSchema.parse({ telemetryEnabled: "true" }),
    ).toThrow();
  });
});
