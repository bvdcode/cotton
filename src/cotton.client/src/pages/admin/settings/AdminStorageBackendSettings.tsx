import {
  Alert,
  Box,
  FormControl,
  InputLabel,
  LinearProgress,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
  type SxProps,
  type Theme,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "react-toastify";
import {
  settingsApi,
  type S3Config,
  type StorageType,
} from "../../../shared/api/settingsApi";
import {
  hasApiErrorToastBeenDispatched,
  isAxiosError,
} from "../../../shared/api/httpClient";
import { AdminSettingSaveField } from "./AdminSettingSaveField";
import { AdminSettingSavingOverlay } from "./AdminSettingSavingOverlay";

type AdminStorageBackendSettingsProps = {
  showHeader?: boolean;
  onSaved?: () => void;
  sx?: SxProps<Theme>;
};

const emptyS3Config: S3Config = {
  endpoint: "",
  region: "",
  bucket: "",
  accessKey: "",
  secretKey: "",
};

export const AdminStorageBackendSettings = ({
  showHeader = true,
  onSaved,
  sx,
}: AdminStorageBackendSettingsProps) => {
  const { t } = useTranslation("admin");
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [storageType, setStorageType] = useState<StorageType>("Local");
  const [s3Config, setS3Config] = useState<S3Config>(emptyS3Config);

  const isBusy = loading || saving;
  const isS3Storage = storageType === "S3";

  useEffect(() => {
    let active = true;

    const load = async () => {
      setLoading(true);
      setLoadError(null);

      try {
        const [nextStorageType, nextS3Config] = await Promise.all([
          settingsApi.getStorageType(),
          settingsApi.getS3Config(),
        ]);

        if (!active) return;

        setStorageType(nextStorageType);
        setS3Config(nextS3Config);
      } catch {
        if (!active) return;
        setLoadError(t("storageSettings.errors.loadFailed"));
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    };

    void load();

    return () => {
      active = false;
    };
  }, [t]);

  const updateS3Config = (key: keyof S3Config, value: string) => {
    setS3Config((current) => ({ ...current, [key]: value }));
  };

  const saveStorageSettings = async () => {
    if (isBusy) return;

    setSaving(true);
    try {
      if (storageType === "S3") {
        await settingsApi.setS3Config(s3Config);
      }

      await settingsApi.setStorageType(storageType);
      toast.success(t("storageSettings.state.storageSaved"), {
        toastId: "admin-storage-settings:storage-type:saved",
      });
      onSaved?.();
    } catch (error) {
      if (!isAxiosError(error) || !hasApiErrorToastBeenDispatched(error)) {
        toast.error(t("storageSettings.errors.storageSaveFailed"), {
          toastId: "admin-storage-settings:storage-type:save-failed",
        });
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <Stack spacing={2} sx={sx}>
      {showHeader && (
        <Stack spacing={0.5}>
          <Typography variant="h6" fontWeight={700}>
            {t("storageSettings.title")}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {t("storageSettings.description")}
          </Typography>
        </Stack>
      )}

      <Box minHeight={4}>
        <LinearProgress
          sx={{
            opacity: loading ? 1 : 0,
            transition: "opacity 120ms ease",
          }}
        />
      </Box>

      {loadError && <Alert severity="error">{loadError}</Alert>}

      <Box sx={{ maxWidth: showHeader ? 760 : 520, width: "100%" }}>
        <AdminSettingSaveField
          label={t("settings.actions.save")}
          onSave={() => void saveStorageSettings()}
          disabled={isBusy}
          saving={saving}
        >
          <AdminSettingSavingOverlay saving={loading}>
            <FormControl fullWidth>
              <InputLabel id="admin-storage-type-label">
                {t("storageSettings.fields.storageType")}
              </InputLabel>
              <Select
                labelId="admin-storage-type-label"
                label={t("storageSettings.fields.storageType")}
                value={storageType}
                onChange={(event) =>
                  setStorageType(event.target.value as StorageType)
                }
                disabled={isBusy}
              >
                <MenuItem value="Local">
                  {t("storageSettings.storageType.Local")}
                </MenuItem>
                <MenuItem value="S3">
                  {t("storageSettings.storageType.S3")}
                </MenuItem>
              </Select>
            </FormControl>
          </AdminSettingSavingOverlay>
        </AdminSettingSaveField>
      </Box>

      {isS3Storage && (
        <Stack spacing={2}>
          {showHeader && (
            <Typography variant="subtitle1" fontWeight={700}>
              {t("storageSettings.s3.title")}
            </Typography>
          )}
          <Box
            sx={{
              display: "grid",
              gap: 2,
              gridTemplateColumns: {
                xs: "minmax(0, 1fr)",
                md: "repeat(2, minmax(0, 1fr))",
              },
            }}
          >
            <AdminSettingSavingOverlay saving={loading}>
              <TextField
                label={t("storageSettings.s3.fields.endpoint")}
                value={s3Config.endpoint}
                onChange={(event) =>
                  updateS3Config("endpoint", event.target.value)
                }
                disabled={isBusy}
                fullWidth
              />
            </AdminSettingSavingOverlay>
            <AdminSettingSavingOverlay saving={loading}>
              <TextField
                label={t("storageSettings.s3.fields.region")}
                value={s3Config.region}
                onChange={(event) =>
                  updateS3Config("region", event.target.value)
                }
                disabled={isBusy}
                fullWidth
              />
            </AdminSettingSavingOverlay>
            <AdminSettingSavingOverlay saving={loading}>
              <TextField
                label={t("storageSettings.s3.fields.bucket")}
                value={s3Config.bucket}
                onChange={(event) =>
                  updateS3Config("bucket", event.target.value)
                }
                disabled={isBusy}
                fullWidth
              />
            </AdminSettingSavingOverlay>
            <AdminSettingSavingOverlay saving={loading}>
              <TextField
                label={t("storageSettings.s3.fields.accessKey")}
                value={s3Config.accessKey}
                onChange={(event) =>
                  updateS3Config("accessKey", event.target.value)
                }
                disabled={isBusy}
                fullWidth
              />
            </AdminSettingSavingOverlay>
            <AdminSettingSavingOverlay saving={loading}>
              <TextField
                label={t("storageSettings.s3.fields.secretKey")}
                value={s3Config.secretKey}
                onChange={(event) =>
                  updateS3Config("secretKey", event.target.value)
                }
                disabled={isBusy}
                type="password"
                fullWidth
              />
            </AdminSettingSavingOverlay>
          </Box>
        </Stack>
      )}
    </Stack>
  );
};
