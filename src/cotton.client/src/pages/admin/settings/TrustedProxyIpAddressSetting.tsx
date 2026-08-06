import AutoFixHighIcon from "@mui/icons-material/AutoFixHigh";
import PublicIcon from "@mui/icons-material/Public";
import SaveIcon from "@mui/icons-material/Save";
import {
  Alert,
  Button,
  CircularProgress,
  Stack,
  TextField,
} from "@mui/material";
import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "@shared/ui/notifications";
import {
  DIRECT_CONNECTION_IP_ADDRESS,
  settingsApi,
  type CloudflareProxyMetadata,
  type DetectedProxyService,
  type TrustedProxyVerificationResult,
} from "../../../shared/api/settingsApi";
import { showApiErrorToast } from "../../../shared/api/httpClient";
import { SAVED_STATUS_VISIBLE_MS } from "./adminSettingSaveStatus";
import { SettingsSection } from "./SettingsSection";
import type { SaveStatus } from "./useAutoSavedSetting";
import {
  formatCloudflareCountry,
  formatCloudflareDatacenter,
} from "./cloudflareLocation";

const proxyServiceNames: Record<
  Exclude<DetectedProxyService, "reverse-proxy">,
  string
> = {
  cloudflare: "Cloudflare",
  cloudfront: "Amazon CloudFront",
  "azure-front-door": "Azure Front Door",
  fastly: "Fastly",
  "fly-io": "Fly.io",
  vercel: "Vercel",
  "aws-alb": "AWS Application Load Balancer",
  traefik: "Traefik",
  envoy: "Envoy",
  nginx: "nginx",
  caddy: "Caddy",
  haproxy: "HAProxy",
  apache: "Apache HTTP Server",
};

export const TrustedProxyIpAddressSetting = () => {
  const { t, i18n } = useTranslation("admin");
  const [value, setValue] = useState("");
  const [status, setStatus] = useState<SaveStatus>("loading");
  const [detecting, setDetecting] = useState(false);
  const [lastResult, setLastResult] =
    useState<TrustedProxyVerificationResult | null>(null);
  const [detectedProxyServices, setDetectedProxyServices] = useState<
    DetectedProxyService[]
  >([]);
  const [cloudflare, setCloudflare] = useState<CloudflareProxyMetadata>(null);
  const flashTimerRef = useRef<number | null>(null);

  useEffect(() => {
    let active = true;
    settingsApi
      .getTrustedProxyIpAddress()
      .then((address) => {
        if (!active) return;
        setValue(address);
        setStatus("idle");
      })
      .catch((error) => {
        if (!active) return;
        setStatus("error");
        showApiErrorToast(
          error,
          t("settings.errors.loadFailed"),
          "admin-general:trusted-proxy:load-error",
        );
      });

    return () => {
      active = false;
      if (flashTimerRef.current !== null) {
        window.clearTimeout(flashTimerRef.current);
      }
    };
  }, [t]);

  const flashSaved = useCallback(() => {
    if (flashTimerRef.current !== null) {
      window.clearTimeout(flashTimerRef.current);
    }
    setStatus("saved");
    flashTimerRef.current = window.setTimeout(() => {
      setStatus((current) => (current === "saved" ? "idle" : current));
      flashTimerRef.current = null;
    }, SAVED_STATUS_VISIBLE_MS);
  }, []);

  const handleAutoDetect = useCallback(async () => {
    setDetecting(true);
    setLastResult(null);
    setDetectedProxyServices([]);
    setCloudflare(null);
    try {
      const observed = await settingsApi.getObservedProxyInfo();
      if (!observed.observedProxyIpAddress) {
        toast.error(t("settings.general.trustedProxy.unavailable"), {
          toastId: "admin-general:trusted-proxy:auto-unavailable",
        });
        return;
      }
      setValue(observed.suggestedTrustedProxy);
      setDetectedProxyServices(observed.detectedProxyServices);
      setCloudflare(observed.cloudflare);
      toast.success(
        t("settings.general.trustedProxy.detected", {
          address: observed.suggestedTrustedProxy,
        }),
        { toastId: "admin-general:trusted-proxy:auto-success" },
      );
    } catch (error) {
      showApiErrorToast(
        error,
        t("settings.errors.loadFailed"),
        "admin-general:trusted-proxy:auto-error",
      );
    } finally {
      setDetecting(false);
    }
  }, [t]);

  const handleVerifyAndSave = useCallback(async () => {
    setStatus("saving");
    setLastResult(null);
    setDetectedProxyServices([]);
    setCloudflare(null);
    try {
      const result = await settingsApi.verifyAndSaveTrustedProxyIpAddress(
        value.trim() || null,
      );
      setLastResult(result);
      setDetectedProxyServices(result.detectedProxyServices);
      setCloudflare(result.cloudflare);
      if (!result.saved) {
        setStatus("error");
        return;
      }

      setValue(result.trustedProxyIpAddress ?? "");
      flashSaved();
      toast.success(
        result.trustedProxyIpAddress
          ? t("settings.general.trustedProxy.saved")
          : t("settings.general.trustedProxy.disabled"),
        { toastId: "admin-general:trusted-proxy:save-success" },
      );
    } catch (error) {
      setStatus("error");
      showApiErrorToast(
        error,
        t("settings.errors.saveFailed"),
        "admin-general:trusted-proxy:save-error",
      );
    }
  }, [flashSaved, t, value]);

  const handleDirectConnection = useCallback(async () => {
    setStatus("saving");
    setLastResult(null);
    setDetectedProxyServices([]);
    setCloudflare(null);
    try {
      const result = await settingsApi.verifyAndSaveTrustedProxyIpAddress(
        DIRECT_CONNECTION_IP_ADDRESS,
      );
      setLastResult(result);
      setDetectedProxyServices(result.detectedProxyServices);
      setCloudflare(result.cloudflare);
      if (!result.saved) {
        setStatus("error");
        return;
      }

      setValue(DIRECT_CONNECTION_IP_ADDRESS);
      flashSaved();
      toast.success(t("settings.general.trustedProxy.directSaved"), {
        toastId: "admin-general:trusted-proxy:direct-success",
      });
    } catch (error) {
      setStatus("error");
      showApiErrorToast(
        error,
        t("settings.errors.saveFailed"),
        "admin-general:trusted-proxy:direct-error",
      );
    }
  }, [flashSaved, t]);

  const busy = status === "loading" || status === "saving" || detecting;
  const mismatch = lastResult?.matches === false ? lastResult : null;
  const directConnection = value === DIRECT_CONNECTION_IP_ADDRESS;
  const detectedPath = [
    "Cotton",
    ...detectedProxyServices
      .slice()
      .reverse()
      .map((service) =>
        service === "reverse-proxy"
          ? t("settings.general.trustedProxy.genericProxy")
          : proxyServiceNames[service],
      ),
    t("settings.general.trustedProxy.internet"),
  ].join(" → ");
  const cloudflareDetails = [
    formatCloudflareCountry(
      cloudflare?.visitorCountryCode ?? null,
      i18n.resolvedLanguage ?? "en",
    ),
    formatCloudflareDatacenter(cloudflare?.datacenterCode ?? null),
  ].filter((detail): detail is string => detail !== null);

  return (
    <SettingsSection
      title={t("settings.general.trustedProxy.title")}
      description={t("settings.general.trustedProxy.description")}
      status={status}
    >
      <Stack spacing={1.5}>
        <TextField
          value={directConnection ? "" : value}
          onChange={(event) => {
            setValue(event.target.value);
            setLastResult(null);
            setDetectedProxyServices([]);
            setCloudflare(null);
            if (status === "error") setStatus("idle");
          }}
          placeholder={t("settings.general.trustedProxy.placeholder")}
          helperText={
            directConnection
              ? t("settings.general.trustedProxy.directMode")
              : t("settings.general.trustedProxy.emptyHint")
          }
          disabled={busy}
          fullWidth
        />

        {detectedProxyServices.length > 0 && (
          <Alert severity="info">
            <Stack spacing={0.5}>
              <span>
                {t("settings.general.trustedProxy.detectedPath")}:{" "}
                <strong>{detectedPath}</strong>
              </span>
              {cloudflareDetails.length > 0 && (
                <span>
                  Cloudflare: <strong>{cloudflareDetails.join(" · ")}</strong>
                </span>
              )}
            </Stack>
          </Alert>
        )}

        {mismatch && (
          <Alert severity="warning">
            {t("settings.general.trustedProxy.mismatch", {
              entered: mismatch.trustedProxyIpAddress ?? "",
              observed: mismatch.observedProxyIpAddress,
            })}
          </Alert>
        )}

        <Stack
          direction="row"
          spacing={1}
          justifyContent="flex-end"
          useFlexGap
          flexWrap="wrap"
        >
          <Button
            variant={directConnection ? "contained" : "outlined"}
            onClick={() => void handleDirectConnection()}
            disabled={busy}
            startIcon={<PublicIcon />}
          >
            {t("settings.general.trustedProxy.direct")}
          </Button>
          <Button
            variant="outlined"
            onClick={() => void handleAutoDetect()}
            disabled={busy}
            startIcon={
              detecting ? (
                <CircularProgress size={16} color="inherit" />
              ) : (
                <AutoFixHighIcon />
              )
            }
          >
            {t("settings.general.trustedProxy.detect")}
          </Button>
          <Button
            variant="contained"
            onClick={() => void handleVerifyAndSave()}
            disabled={busy}
            startIcon={
              status === "saving" ? (
                <CircularProgress size={16} color="inherit" />
              ) : (
                <SaveIcon />
              )
            }
          >
            {t("settings.general.trustedProxy.verifyAndSave")}
          </Button>
        </Stack>
      </Stack>
    </SettingsSection>
  );
};
