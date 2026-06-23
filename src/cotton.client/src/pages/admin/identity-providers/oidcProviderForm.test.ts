import { describe, expect, it } from "vitest";
import {
  buildOidcCallbackUrl,
  resolveOidcCallbackBaseUrl,
} from "./oidcProviderForm";

describe("oidcProviderForm", () => {
  it("uses the browser origin when the public base URL is not configured", () => {
    expect(
      resolveOidcCallbackBaseUrl("http://localhost", "https://cotton.example/"),
    ).toBe("https://cotton.example");
  });

  it("builds the stable provider callback URL", () => {
    expect(buildOidcCallbackUrl("https://cotton.example")).toBe(
      "https://cotton.example/api/v1/auth/oidc/callback",
    );
  });
});
