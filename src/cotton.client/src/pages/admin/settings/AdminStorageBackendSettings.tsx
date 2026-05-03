import SaveIcon from "@mui/icons-material/Save";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
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
import { useEffect, useState, type ReactNode } from "react";
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
import { AdminSettingSavingOverlay } from "./AdminSettingSavingOverlay";

type AdminStorageBackendSettingsProps = {
  showHeader?: boolean;
  onSaved?: () => void;
  sx?: SxProps<Theme>;
  storageTypeRightSlot?: ReactNode;
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
  storageTypeRightSlot,
}: AdminStorageBackendSettingsProps) => {
  const { t } = useTranslation("admin");
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [storageTypeSaving, setStorageTypeSaving] = useState(false);
  const [s3Saving, setS3Saving] = useState(false);
  const [storageType, setStorageType] = useState<StorageType>("Local");
  const [s3Config, setS3Config] = useState<S3Config>(emptyS3Config);

  const isBusy = loading || storageTypeSaving || s3Saving;
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

  const handleStorageTypeChange = async (next: StorageType) => {
    if (next === storageType || isBusy) return;

    if (next === "S3") {
      setStorageType(next);
      return;
    }

    const previous = storageType;
    setStorageType(next);
    setStorageTypeSaving(true);
    try {
      await settingsApi.setStorageType(next);
      toast.success(t("storageSettings.state.storageSaved"), {
        toastId: "admin-storage-settings:storage-type:saved",
      });
      onSaved?.();
    } catch (error) {
      setStorageType(previous);
      if (!isAxiosError(error) || !hasApiErrorToastBeenDispatched(error)) {
        toast.error(t("storageSettings.errors.storageSaveFailed"), {
          toastId: "admin-storage-settings:storage-type:save-failed",
        });
      }
    } finally {
      setStorageTypeSaving(false);
    }
  };

  const saveS3Config = async () => {
    if (isBusy) return;

    setS3Saving(true);
    try {
      await settingsApi.setS3Config(s3Config);
      await settingsApi.setStorageType("S3");
      toast.success(t("storageSettings.state.s3Saved"), {
        toastId: "admin-storage-settings:s3-config:saved",
      });
      onSaved?.();
    } catch (error) {
      if (!isAxiosError(error) || !hasApiErrorToastBeenDispatched(error)) {
        toast.error(t("storageSettings.errors.s3SaveFailed"), {
          toastId: "admin-storage-settings:s3-config:save-failed",
        });
      }
    } finally {
      setS3Saving(false);
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

      <Box
        sx={{
          width: "100%",
          display: "grid",
          gap: 2,
          alignItems: "center",
          gridTemplateColumns: {
            xs: "1fr",
            md: storageTypeRightSlot
              ? "repeat(2, minmax(0, 1fr))"
              : showHeader
                ? "minmax(0, 760px)"
                : "minmax(0, calc((100% - 16px) / 2))",
          },
        }}
      >
        <AdminSettingSavingOverlay saving={loading || storageTypeSaving}>
          <FormControl fullWidth>
            <InputLabel id="admin-storage-type-label">
              {t("storageSettings.fields.storageType")}
            </InputLabel>
            <Select
              labelId="admin-storage-type-label"
              label={t("storageSettings.fields.storageType")}
              value={storageType}
              onChange={(event) =>
                void handleStorageTypeChange(event.target.value as StorageType)
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
        {storageTypeRightSlot ? (
          <Box
            sx={{
              minWidth: 0,
              display: "flex",
              justifyContent: { xs: "flex-start", md: "flex-end" },
            }}
          >
            {storageTypeRightSlot}
          </Box>
        ) : null}
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
            <Box
              sx={{
                display: "flex",
                alignItems: "stretch",
                width: "100%",
              }}
            >
              <Button
                variant="contained"
                onClick={() => void saveS3Config()}
                disabled={isBusy}
                fullWidth
                startIcon={
                  s3Saving ? (
                    <CircularProgress size={16} color="inherit" />
                  ) : (
                    <SaveIcon />
                  )
                }
                sx={{ height: 56 }}
              >
                {t("settings.actions.save")}
              </Button>
            </Box>
          </Box>
        </Stack>
      )}
    </Stack>
  );
};
