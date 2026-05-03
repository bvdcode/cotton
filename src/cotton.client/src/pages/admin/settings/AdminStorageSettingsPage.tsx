import {
  Alert,
  Box,
  FormControl,
  InputLabel,
  LinearProgress,
  MenuItem,
  Paper,
  Select,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "react-toastify";
import {
  settingsApi,
  type S3Config,
  type StorageType,
} from "../../../shared/api/settingsApi";
import { AdminSettingSaveIconButton } from "./AdminSettingSaveIconButton";
import { AdminSettingSavingOverlay } from "./AdminSettingSavingOverlay";

type SavingKey = "storageType" | "s3Config";

const saveIconSx = { mt: 1, flexShrink: 0 };

export const AdminStorageSettingsPage = () => {
  const { t } = useTranslation("admin");
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [savingKey, setSavingKey] = useState<SavingKey | null>(null);
  const [storageType, setStorageType] = useState<StorageType>("Local");
  const [s3Config, setS3Config] = useState<S3Config>({
    endpoint: "",
    region: "",
    bucket: "",
    accessKey: "",
    secretKey: "",
  });

  const isBusy = loading || savingKey !== null;
  const isS3Storage = storageType === "S3";
  const isSaving = (key: SavingKey): boolean => savingKey === key;

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

  const saveS3Config = async () => {
    if (isBusy) return;

    setSavingKey("s3Config");
    try {
      await settingsApi.setS3Config(s3Config);
      toast.success(t("storageSettings.state.s3Saved"), {
        toastId: "admin-storage-settings:s3-config:saved",
      });
    } catch {
      toast.error(t("storageSettings.errors.s3SaveFailed"), {
        toastId: "admin-storage-settings:s3-config:save-failed",
      });
    } finally {
      setSavingKey(null);
    }
  };

  const activateStorage = async (nextType: StorageType) => {
    if (isBusy) return;

    setSavingKey("storageType");
    try {
      if (nextType === "S3") {
        await settingsApi.setS3Config(s3Config);
      }

      await settingsApi.setStorageType(nextType);
      setStorageType(nextType);
      toast.success(t("storageSettings.state.storageSaved"), {
        toastId: "admin-storage-settings:storage-type:saved",
      });
    } catch {
      toast.error(t("storageSettings.errors.storageSaveFailed"), {
        toastId: "admin-storage-settings:storage-type:save-failed",
      });
    } finally {
      setSavingKey(null);
    }
  };

  return (
    <Stack spacing={2}>
      <Paper>
        <Stack p={2} spacing={2}>
          <Stack spacing={0.5}>
            <Typography variant="h6" fontWeight={700}>
              {t("storageSettings.title")}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {t("storageSettings.description")}
            </Typography>
          </Stack>

          <LinearProgress
            sx={{
              opacity: isBusy ? 1 : 0,
              transition: "opacity 120ms ease",
            }}
          />

          {loadError && <Alert severity="error">{loadError}</Alert>}

          <Stack direction="row" spacing={1} alignItems="flex-start">
            <Box flex={1} minWidth={0}>
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
            </Box>
            <AdminSettingSavingOverlay saving={isSaving("storageType")}>
              <AdminSettingSaveIconButton
                label={
                  storageType === "S3"
                    ? t("storageSettings.actions.useS3")
                    : t("storageSettings.actions.useLocal")
                }
                onClick={() => void activateStorage(storageType)}
                disabled={isBusy}
                sx={saveIconSx}
              />
            </AdminSettingSavingOverlay>
          </Stack>

          {isS3Storage && (
            <Stack spacing={2}>
              <Typography variant="subtitle1" fontWeight={700}>
                {t("storageSettings.s3.title")}
              </Typography>
              <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
                <Box flex={1} minWidth={0}>
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
                </Box>
                <Box flex={1} minWidth={0}>
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
                </Box>
              </Stack>
              <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
                <Box flex={1} minWidth={0}>
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
                </Box>
                <Box flex={1} minWidth={0}>
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
                </Box>
              </Stack>
              <Stack direction="row" spacing={1} alignItems="flex-start">
                <Box flex={1} minWidth={0}>
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
                <AdminSettingSavingOverlay saving={isSaving("s3Config")}>
                  <AdminSettingSaveIconButton
                    label={t("storageSettings.actions.saveS3")}
                    onClick={() => void saveS3Config()}
                    disabled={isBusy}
                    sx={saveIconSx}
                  />
                </AdminSettingSavingOverlay>
              </Stack>
            </Stack>
          )}
        </Stack>
      </Paper>
    </Stack>
  );
};
