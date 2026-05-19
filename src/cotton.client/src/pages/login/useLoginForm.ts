import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
} from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useSearchParams } from "react-router-dom";
import { toast } from "@shared/ui/notifications";
import { useAuth } from "@features/auth";
import { authApi } from "@shared/api/authApi";
import {
  getApiErrorMessage,
  hasApiErrorToastBeenDispatched,
  isAxiosError,
} from "@shared/api/httpClient";
import { getUsernameError } from "@shared/validation/username";
import {
  isEmail,
  normalizeTwoFactorCode,
  tryGetTwoFactorHint,
  type ToastSeverity,
} from "./loginUtils";
import { getOrCreateDemoCredentials } from "./demoCredentials";

interface UseLoginFormResult {
  username: string;
  setUsername: (value: string) => void;
  password: string;
  setPassword: (value: string) => void;
  twoFactorCode: string;
  setTwoFactorCode: (value: string) => void;
  trustDevice: boolean;
  toggleTrustDevice: () => void;
  isUsernameBlurred: boolean;
  markUsernameBlurred: () => void;
  requiresTwoFactor: boolean;
  loading: boolean;
  forgotPasswordSending: boolean;
  usernameHasError: boolean;
  handleSubmit: (e: FormEvent) => Promise<void>;
  handleForgotPassword: () => Promise<void>;
}

export const useLoginForm = (): UseLoginFormResult => {
  const navigate = useNavigate();
  const { t } = useTranslation("login");
  const { setAuthenticated } = useAuth();
  const [searchParams] = useSearchParams();

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [isUsernameBlurred, setIsUsernameBlurred] = useState(false);
  const [trustDevice, setTrustDevice] = useState(false);
  const [requiresTwoFactor, setRequiresTwoFactor] = useState(false);
  const [twoFactorCode, setTwoFactorCode] = useState("");
  const [loading, setLoading] = useState(false);
  const [forgotPasswordSending, setForgotPasswordSending] = useState(false);
  const autoSubmitTriggeredRef = useRef(false);
  const demoFillRef = useRef(false);
  const demoSubmitRef = useRef(false);

  const showToast = useCallback((message: string, severity: ToastSeverity) => {
    const toastId = `login:${severity}:${message}`;

    if (severity === "success") {
      toast.success(message, { toastId });
      return;
    }

    toast.error(message, { toastId });
  }, []);

  const submitLogin = useCallback(async () => {
    setLoading(true);

    try {
      const trimmedUsername = username.trim();
      const usernameFormatError =
        trimmedUsername.length > 0 && !isEmail(trimmedUsername)
          ? getUsernameError(trimmedUsername)
          : null;

      if (usernameFormatError) {
        showToast(usernameFormatError, "error");
        return;
      }

      if (
        requiresTwoFactor &&
        normalizeTwoFactorCode(twoFactorCode).length < 6
      ) {
        showToast(t("twoFactor.required"), "error");
        return;
      }

      await authApi.login({
        username: trimmedUsername,
        password,
        twoFactorCode: requiresTwoFactor
          ? normalizeTwoFactorCode(twoFactorCode)
          : undefined,
        trustDevice,
      });

      const user = await authApi.me();
      setAuthenticated(true, user);
      navigate("/");
    } catch (e) {
      if (isAxiosError(e)) {
        const status = e.response?.status;
        const serverMessage = getApiErrorMessage(e) ?? undefined;
        const hint = tryGetTwoFactorHint({ status, serverMessage });

        if (hint === "required") {
          setRequiresTwoFactor(true);
          setTwoFactorCode("");
          return;
        }

        if (hint === "invalid") {
          setRequiresTwoFactor(true);
          showToast(t("twoFactor.invalid"), "error");
          return;
        }

        if (hint === "locked") {
          setRequiresTwoFactor(true);
          showToast(t("twoFactor.locked"), "error");
          return;
        }

        if (hasApiErrorToastBeenDispatched(e)) {
          return;
        }
      }

      showToast(t("errorMessage"), "error");
    } finally {
      setLoading(false);
    }
  }, [
    requiresTwoFactor,
    twoFactorCode,
    username,
    password,
    trustDevice,
    t,
    showToast,
    setAuthenticated,
    navigate,
  ]);

  const handleSubmit = useCallback(
    async (e: FormEvent) => {
      e.preventDefault();
      await submitLogin();
    },
    [submitLogin],
  );

  const handleForgotPassword = useCallback(async () => {
    const trimmed = username.trim();
    if (!trimmed || !isEmail(trimmed)) {
      showToast(t("forgotPassword.enterEmail"), "error");
      return;
    }

    setForgotPasswordSending(true);
    try {
      await authApi.forgotPassword(trimmed);
      showToast(t("forgotPassword.sent"), "success");
    } catch {
      showToast(t("forgotPassword.sent"), "success");
    } finally {
      setForgotPasswordSending(false);
    }
  }, [username, t, showToast]);

  useEffect(() => {
    if (demoFillRef.current) return;
    if (searchParams.get("demo") !== "true") return;
    demoFillRef.current = true;

    const credentials = getOrCreateDemoCredentials(window.localStorage);
    setUsername(credentials.username);
    setPassword(credentials.password);
  }, [searchParams]);

  useEffect(() => {
    if (!demoFillRef.current || demoSubmitRef.current) return;
    if (username.trim().length === 0 || password.length === 0) return;
    demoSubmitRef.current = true;
    void submitLogin();
  }, [username, password, submitLogin]);

  useEffect(() => {
    if (!requiresTwoFactor) {
      autoSubmitTriggeredRef.current = false;
      return;
    }

    const cleanCode = normalizeTwoFactorCode(twoFactorCode);
    if (cleanCode.length === 6 && !loading && !autoSubmitTriggeredRef.current) {
      autoSubmitTriggeredRef.current = true;

      const timer = setTimeout(() => {
        void submitLogin();
      }, 100);

      return () => clearTimeout(timer);
    }

    if (cleanCode.length < 6) {
      autoSubmitTriggeredRef.current = false;
    }
  }, [twoFactorCode, requiresTwoFactor, loading, submitLogin]);

  const usernameErrorText = (() => {
    if (requiresTwoFactor) {
      return undefined;
    }

    const trimmedUsername = username.trim();
    if (trimmedUsername.length === 0 || isEmail(trimmedUsername)) {
      return undefined;
    }

    return getUsernameError(trimmedUsername) ?? undefined;
  })();

  const usernameHasError = isUsernameBlurred && Boolean(usernameErrorText);

  const toggleTrustDevice = useCallback(() => {
    setTrustDevice((value) => !value);
  }, []);

  const markUsernameBlurred = useCallback(() => {
    setIsUsernameBlurred(true);
  }, []);

  return {
    username,
    setUsername,
    password,
    setPassword,
    twoFactorCode,
    setTwoFactorCode,
    trustDevice,
    toggleTrustDevice,
    isUsernameBlurred,
    markUsernameBlurred,
    requiresTwoFactor,
    loading,
    forgotPasswordSending,
    usernameHasError,
    handleSubmit,
    handleForgotPassword,
  };
};
