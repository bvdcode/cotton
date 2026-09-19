import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import {
  AxiosError,
  AxiosHeaders,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from "axios";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useLoginForm } from "./useLoginForm";

const mocks = vi.hoisted(() => ({
  login: vi.fn(),
  setAuthenticated: vi.fn(),
  toastError: vi.fn(),
}));

vi.mock("@features/auth", () => ({
  useAuth: () => ({ setAuthenticated: mocks.setAuthenticated }),
}));

vi.mock("@shared/api/authApi", () => ({
  authApi: {
    login: mocks.login,
  },
}));

vi.mock("@shared/ui/notifications", () => ({
  toast: {
    error: mocks.toastError,
    success: vi.fn(),
  },
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

const LoginFormProbe = () => {
  const form = useLoginForm();

  return (
    <form onSubmit={form.handleSubmit}>
      <input
        aria-label="username"
        value={form.username}
        onChange={(event) => form.setUsername(event.target.value)}
      />
      <input
        aria-label="password"
        value={form.password}
        onChange={(event) => form.setPassword(event.target.value)}
      />
      <button type="submit">submit</button>
    </form>
  );
};

const buildLoginError = (detail: string): AxiosError => {
  const config: InternalAxiosRequestConfig = {
    headers: new AxiosHeaders(),
    url: "auth/login",
  };
  const response: AxiosResponse = {
    config,
    data: {
      title: "Unauthorized",
      status: 401,
      detail,
      instance: "/api/v1/auth/login",
      code: "unauthorized",
      traceId: "trace-id",
    },
    headers: {},
    status: 401,
    statusText: "Unauthorized",
  };

  return new AxiosError(
    "Request failed with status code 401",
    "ERR_BAD_REQUEST",
    config,
    undefined,
    response,
  );
};

describe("useLoginForm", () => {
  beforeEach(() => {
    mocks.login.mockReset();
    mocks.setAuthenticated.mockReset();
    mocks.toastError.mockReset();
  });

  it("shows the localized message when login is rejected", async () => {
    mocks.login.mockRejectedValue(
      buildLoginError("Invalid username or password"),
    );
    render(
      <MemoryRouter>
        <LoginFormProbe />
      </MemoryRouter>,
    );

    fireEvent.change(screen.getByLabelText("username"), {
      target: { value: "alice@example.com" },
    });
    fireEvent.change(screen.getByLabelText("password"), {
      target: { value: "wrong-password" },
    });
    fireEvent.click(screen.getByRole("button", { name: "submit" }));

    await waitFor(() => {
      expect(mocks.toastError).toHaveBeenCalledWith("errorMessage", {
        toastId: "login:error:errorMessage",
      });
    });
    expect(mocks.toastError).not.toHaveBeenCalledWith(
      "Invalid username or password",
      expect.anything(),
    );
    expect(mocks.setAuthenticated).not.toHaveBeenCalled();
  });
});
