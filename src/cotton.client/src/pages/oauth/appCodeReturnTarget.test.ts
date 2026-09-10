import { describe, expect, it } from "vitest";
import { resolveAppCodeReturnUri } from "./appCodeReturnTarget";

describe("app-code return target", () => {
  it("resolves the supported mobile return target", () => {
    expect(resolveAppCodeReturnUri("mobile")).toBe(
      "cotton://authorization-complete",
    );
  });

  it.each([null, "", "https://attacker.example", "unsupported"])(
    "rejects unsupported return target %s",
    (returnTarget) => {
      expect(resolveAppCodeReturnUri(returnTarget)).toBeNull();
    },
  );
});
