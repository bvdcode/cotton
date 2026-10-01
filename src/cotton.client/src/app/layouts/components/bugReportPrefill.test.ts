import { describe, expect, it } from "vitest";
import { reportClientError } from "../../../shared/utils/clientDiagnostics";
import { buildBugReportUrl } from "./bugReportPrefill";

describe("bug report prefill", () => {
  it("includes recorded application errors without exposing the current host", () => {
    const marker = `diagnostic-${crypto.randomUUID()}`;
    reportClientError(marker, new Error("failed"));

    const url = buildBugReportUrl({
      currentUrl: "https://private.example/files/test",
    });
    const body = new URL(url).searchParams.get("body");

    expect(body).toContain(marker);
    expect(body).toContain("Error: failed");
    expect(body).toContain("https://<redacted-host>/files/test");
    expect(body).not.toContain("private.example");
  });
});
