import { describe, expect, it } from "vitest";
import {
  buildOidcProviderCallbackUrl,
  resolveOidcCallbackBaseUrl,
  resolveOidcProviderCallbackSlug,
  slugifyOidcProviderName,
} from "./oidcProviderForm";

describe("oidcProviderForm", () => {
  it("matches the server provider slug generation for common names", () => {
    expect(slugifyOidcProviderName(" Google Workspace ")).toBe(
      "google-workspace",
    );
    expect(slugifyOidcProviderName("123 Login")).toBe("oidc-123-login");
    expect(slugifyOidcProviderName("Acme___OIDC")).toBe("acme___oidc");
  });

  it("prefers an existing provider slug for callback URLs", () => {
    expect(
      resolveOidcProviderCallbackSlug({
        name: "Renamed provider",
        slug: "google",
      }),
    ).toBe("google");
  });

  it("uses the browser origin when the public base URL is not configured", () => {
    expect(
      resolveOidcCallbackBaseUrl(
        "http://localhost",
        "https://cotton.example/",
      ),
    ).toBe("https://cotton.example");
  });

  it("builds the provider callback URL", () => {
    expect(
      buildOidcProviderCallbackUrl("google-workspace", "https://cotton.example"),
    ).toBe(
      "https://cotton.example/api/v1/auth/oidc/callback/google-workspace",
    );
  });
});
