import { describe, expect, it } from "vitest";
import { getPgvectorDockerImage } from "./smartSearchSetup";

describe("pgvector Docker setup", () => {
  it.each([
    ["postgres:17-bookworm", 17, "", "pgvector/pgvector:pg17-bookworm"],
    [" image: postgres:18.3-trixie ", 18, "", "pgvector/pgvector:pg18-trixie"],
    [
      "docker.io/library/postgres:16",
      16,
      "bookworm",
      "pgvector/pgvector:pg16-bookworm",
    ],
  ])(
    "preserves the server major version and Debian variant for %s",
    (image, version, variant, expected) => {
      expect(getPgvectorDockerImage(image, version, variant)).toEqual({
        kind: "ready",
        image: expected,
      });
    },
  );

  it.each([
    ["", 18, "empty"],
    ["postgres:18", 18, "chooseVariant"],
    ["postgres:17-bookworm", 18, "mismatch"],
    ["postgres:18-alpine", 18, "unsupported"],
    ["postgres:19-trixie", 19, "versionUnsupported"],
    ["postgres:latest", 18, "invalid"],
    ["custom/postgres:18", 18, "invalid"],
    ["postgres:18; touch file", 18, "invalid"],
  ])(
    "does not propose an unsafe image replacement for %s",
    (image, version, kind) => {
      expect(getPgvectorDockerImage(image, version, "")).toEqual({ kind });
    },
  );
});
