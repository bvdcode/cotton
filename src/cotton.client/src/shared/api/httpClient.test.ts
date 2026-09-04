import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
  type Mock,
} from "vitest";
import type { AxiosError } from "axios";
import { z } from "zod";

vi.mock("@shared/ui/notifications", () => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
}));

vi.mock("../i18n/translateError", () => ({
  translateError: (namespace: string, key: string) => `${namespace}:${key}`,
}));

const refreshEnabledMock = vi.fn<() => boolean>();
const logoutLocalMock = vi.fn<() => void>();

vi.mock("../store/authStore", () => ({
  getRefreshEnabled: () => refreshEnabledMock(),
  useAuthStore: {
    getState: () => ({
      user: null,
      refreshEnabled: refreshEnabledMock(),
      logoutLocal: logoutLocalMock,
    }),
  },
}));

const {
  extractApiErrorMessage,
  getApiErrorMessage,
  getValidated,
  hasApiErrorToastBeenDispatched,
  httpClient,
  parseValidated,
  showApiErrorToast,
} = await import("./httpClient");

const { toast } = await import("@shared/ui/notifications");
const toastErrorMock = toast.error as unknown as Mock;

const buildAxiosError = (
  status: number,
  data: unknown,
  url = "/test",
): AxiosError =>
  ({
    config: { url },
    response: { status, data },
    isAxiosError: true,
    message: "Request failed",
    name: "AxiosError",
    toJSON: () => ({}),
  }) as AxiosError;

beforeEach(() => {
  refreshEnabledMock.mockReturnValue(true);
  toastErrorMock.mockClear();
  logoutLocalMock.mockClear();
  vi.spyOn(console, "error").mockImplementation(() => undefined);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("extractApiErrorMessage", () => {
  it("unwraps a plain string body", () => {
    expect(extractApiErrorMessage("boom")).toBe("boom");
  });

  it("trims and rejects whitespace-only bodies", () => {
    expect(extractApiErrorMessage("   ")).toBeNull();
  });

  it("prefers detail over message over validation errors over title", () => {
    expect(
      extractApiErrorMessage({
        detail: "detail",
        message: "message",
        errors: { Field: ["required"] },
        title: "title",
      }),
    ).toBe("detail");
    expect(
      extractApiErrorMessage({
        message: "message",
        errors: { Field: ["required"] },
        title: "title",
      }),
    ).toBe("message");
    expect(
      extractApiErrorMessage({
        errors: { Field: ["required"] },
        title: "title",
      }),
    ).toBe("required");
    expect(extractApiErrorMessage({ title: "title" })).toBe("title");
  });

  it("descends into nested validation errors", () => {
    expect(
      extractApiErrorMessage({
        errors: {
          Name: ["Name is required"],
          Nested: { Field: ["Nested field is invalid"] },
        },
      }),
    ).toBe("Name is required");
  });

  it("returns null for unrecognized shapes", () => {
    expect(extractApiErrorMessage(null)).toBeNull();
    expect(extractApiErrorMessage(42)).toBeNull();
    expect(extractApiErrorMessage({ unrelated: "field" })).toBeNull();
  });
});

describe("getApiErrorMessage", () => {
  it("returns null for non-Axios errors", () => {
    expect(getApiErrorMessage(new Error("plain"))).toBeNull();
    expect(getApiErrorMessage("string-error")).toBeNull();
  });

  it("extracts the message from an Axios error body", () => {
    expect(getApiErrorMessage(buildAxiosError(400, { detail: "bad" }))).toBe(
      "bad",
    );
  });
});

describe("showApiErrorToast", () => {
  it("uses the fallback when the error is not an Axios error", () => {
    showApiErrorToast(new Error("oops"), "fallback message", "id");

    expect(toastErrorMock).toHaveBeenCalledWith("fallback message", {
      toastId: "id",
    });
  });

  it("uses the server-provided message when present", () => {
    showApiErrorToast(
      buildAxiosError(400, { detail: "from server" }),
      "fallback message",
      "id",
    );

    expect(toastErrorMock).toHaveBeenCalledWith(
      "from server",
      expect.objectContaining({
        toastId: expect.stringContaining("from server"),
      }),
    );
  });

  it("dispatches the toast only once per Axios error instance", () => {
    const error = buildAxiosError(400, { detail: "once" });

    showApiErrorToast(error, "fallback", "id");
    showApiErrorToast(error, "fallback", "id");

    expect(toastErrorMock).toHaveBeenCalledTimes(1);
    expect(hasApiErrorToastBeenDispatched(error)).toBe(true);
  });

  it("falls back to the caller's message when no server message is present", () => {
    showApiErrorToast(buildAxiosError(500, {}), "fallback", "id");

    expect(toastErrorMock).toHaveBeenCalledWith("fallback", { toastId: "id" });
  });
});

describe("parseValidated", () => {
  const schema = z.object({ id: z.string(), value: z.number() });

  it("returns parsed data on success", () => {
    expect(parseValidated("/probe", { id: "a", value: 1 }, schema)).toEqual({
      id: "a",
      value: 1,
    });
    expect(toastErrorMock).not.toHaveBeenCalled();
  });

  it("toasts and rethrows on a schema mismatch", () => {
    expect(() =>
      parseValidated("/probe", { id: 1, value: "bad" }, schema),
    ).toThrow(z.ZodError);

    expect(toastErrorMock).toHaveBeenCalledWith(
      "common:errors.schemaValidationFailed",
      expect.objectContaining({
        toastId: expect.stringContaining("/probe"),
      }),
    );
  });
});

describe("getValidated", () => {
  const schema = z.object({ ok: z.boolean() });

  it("issues a GET and validates the body", async () => {
    const get = vi
      .spyOn(httpClient, "get")
      .mockResolvedValue({ data: { ok: true } });

    await expect(getValidated("/probe", schema)).resolves.toEqual({ ok: true });
    expect(get).toHaveBeenCalledWith("/probe", undefined);
  });

  it("forwards axios config to the request", async () => {
    const get = vi
      .spyOn(httpClient, "get")
      .mockResolvedValue({ data: { ok: false } });

    await getValidated("/probe", schema, { params: { q: 1 } });

    expect(get).toHaveBeenCalledWith("/probe", { params: { q: 1 } });
  });

  it("rejects with ZodError when the body does not match", async () => {
    vi.spyOn(httpClient, "get").mockResolvedValue({ data: { ok: "yes" } });

    await expect(getValidated("/probe", schema)).rejects.toBeInstanceOf(
      z.ZodError,
    );
    expect(toastErrorMock).toHaveBeenCalledTimes(1);
  });
});
