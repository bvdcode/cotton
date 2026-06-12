import { describe, expect, it } from "vitest";

import { formatAppCodeOrigin, isLoopbackOrigin } from "./appCodeOrigin";

describe("app-code origin display", () => {
  it.each(["127.0.0.1", "127.42.0.8", "::1", "::ffff:127.0.0.1", "localhost"])(
    "formats loopback origin %s as a local device",
    (origin) => {
      expect(isLoopbackOrigin(origin)).toBe(true);
      expect(formatAppCodeOrigin(origin, "this device")).toBe("this device");
    },
  );

  it.each(["10.0.0.5", "192.168.1.20", "203.0.113.10", "app.cottoncloud.dev"])(
    "keeps non-loopback origin %s visible",
    (origin) => {
      expect(isLoopbackOrigin(origin)).toBe(false);
      expect(formatAppCodeOrigin(origin, "this device")).toBe(origin);
    },
  );
});
