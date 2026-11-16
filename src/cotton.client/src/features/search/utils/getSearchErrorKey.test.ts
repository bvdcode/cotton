import { AxiosError } from "axios";
import { describe, expect, it } from "vitest";
import { createHttpResponse } from "../../../test/httpFixtures";
import { getSearchErrorKey } from "./getSearchErrorKey";

describe("search configuration errors", () => {
  it.each(["NotConfigured", "UnsupportedMode", "InvalidUrl"])(
    "recognizes %s only for smart search",
    (code) => {
      const error = new AxiosError(
        "Configuration required",
        undefined,
        undefined,
        undefined,
        {
          ...createHttpResponse({ code }),
          status: 422,
        },
      );

      expect(getSearchErrorKey(error, true)).toBe("smartSearch.notConfigured");
      expect(getSearchErrorKey(error, false)).toBe("error");
    },
  );

  it.each(["Unreachable", "Timeout", "AccessDenied", "InvalidResponse"])(
    "keeps %s distinct from missing configuration",
    (code) => {
      const error = new AxiosError(
        "Service unavailable",
        undefined,
        undefined,
        undefined,
        {
          ...createHttpResponse({ code }),
          status: 422,
        },
      );

      expect(getSearchErrorKey(error, true)).toBe("smartSearch.error");
    },
  );

  it("does not infer missing configuration from an unrecognized error", () => {
    expect(getSearchErrorKey(new Error("Network error"), true)).toBe(
      "smartSearch.error",
    );
    expect(getSearchErrorKey({ code: "NotConfigured" }, true)).toBe(
      "smartSearch.error",
    );
  });
});
