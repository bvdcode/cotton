import { describe, expect, it } from "vitest";
import { base64ToBytes, bytesToBase64 } from "./base64";

describe("base64 helpers", () => {
  it("round-trips empty input", () => {
    expect(bytesToBase64(new Uint8Array())).toBe("");
    expect(Array.from(base64ToBytes(""))).toEqual([]);
  });

  it("matches a known RFC 4648 sample", () => {
    const text = "Many hands make light work.";
    const bytes = new TextEncoder().encode(text);

    expect(bytesToBase64(bytes)).toBe("TWFueSBoYW5kcyBtYWtlIGxpZ2h0IHdvcmsu");
    expect(new TextDecoder().decode(base64ToBytes(bytesToBase64(bytes)))).toBe(
      text,
    );
  });

  it("round-trips arbitrary bytes", () => {
    const bytes = new Uint8Array(256);
    crypto.getRandomValues(bytes);

    expect(Array.from(base64ToBytes(bytesToBase64(bytes)))).toEqual(
      Array.from(bytes),
    );
  });
});
