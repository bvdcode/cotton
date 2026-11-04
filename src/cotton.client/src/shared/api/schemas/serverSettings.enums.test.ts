import { describe, expect, it } from "vitest";
import {
  computionModeResponseSchema,
  emailModeResponseSchema,
  geoIpLookupModeResponseSchema,
  serverUsageListSchema,
  storageSpaceModeResponseSchema,
  storageTypeResponseSchema,
} from "./serverSettings";

const contracts = [
  { schema: storageTypeResponseSchema, field: "storageType", value: "S3" },
  { schema: emailModeResponseSchema, field: "emailMode", value: "Custom" },
  {
    schema: computionModeResponseSchema,
    field: "computionMode",
    value: "Remote",
  },
  {
    schema: storageSpaceModeResponseSchema,
    field: "storageSpaceMode",
    value: "Limited",
  },
  {
    schema: geoIpLookupModeResponseSchema,
    field: "geoIpLookupMode",
    value: "MaxMindLocal",
  },
] as const;

describe.each(contracts)("$field contract", ({ schema, field, value }) => {
  it("accepts the server value without normalization", () => {
    expect(schema.parse({ [field]: value })).toBe(value);
  });

  it.each(["unsupported", 0, -1, 99, null, undefined, false, {}, []])(
    "rejects invalid enum input %j",
    (invalid) => {
      expect(schema.safeParse({ [field]: invalid }).success).toBe(false);
    },
  );

  it("rejects missing fields and case changes", () => {
    expect(schema.safeParse({}).success).toBe(false);
    expect(schema.safeParse({ [field]: value.toLowerCase() }).success).toBe(
      false,
    );
  });
});

describe("server usage contract", () => {
  it("preserves the server selection", () => {
    expect(
      serverUsageListSchema.parse({ serverUsage: ["Photos", "Media"] }),
    ).toEqual(["Photos", "Media"]);
  });

  it.each([
    {},
    { serverUsage: null },
    { serverUsage: ["unsupported"] },
    { serverUsage: [0] },
  ])("rejects malformed usage %j", (response) => {
    expect(serverUsageListSchema.safeParse(response).success).toBe(false);
  });
});
