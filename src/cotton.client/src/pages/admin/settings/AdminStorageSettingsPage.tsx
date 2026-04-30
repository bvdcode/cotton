import {
  Alert,
  Button,
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
import {
  settingsApi,
  type S3Config,
  type StorageType,
} from "../../../shared/api/settingsApi";

type LoadState =
  | { kind: "loading" }
  | { kind: "idle" }
  | { kind: "saving" }
  | { kind: "error"; message: string }
  | { kind: "success"; message: string };

export const AdminStorageSettingsPage = () => {
  const { t } = useTranslation("admin");
  const [loadState, setLoadState] = useState<LoadState>({ kind: "loading" });
  const [storageType, setStorageType] = useState<StorageType>("Local");
  const [s3Config, setS3Config] = useState<S3Config>({
    endpoint: "",
    region: "",
    bucket: "",
    accessKey: "",
    secretKey: "",
  });

  const isBusy = loadState.kind === "loading" || loadState.kind === "saving";

  useEffect(() => {
    let active = true;

    const load = async () => {
      try {
        const [nextStorageType, nextS3Config] = await Promise.all([
          settingsApi.getStorageType(),
          settingsApi.getS3Config(),
        ]);

        if (!active) return;

        setStorageType(nextStorageType);
        setS3Config(nextS3Config);
        setLoadState({ kind: "idle" });
      } catch {
        if (!active) return;
        setLoadState({
          kind: "error",
          message: t("storageSettings.errors.loadFailed"),
        });
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
    setLoadState({ kind: "saving" });
    try {
      await settingsApi.setS3Config(s3Config);
      setLoadState({
        kind: "success",
        message: t("storageSettings.state.s3Saved"),
      });
    } catch {
      setLoadState({
        kind: "error",
        message: t("storageSettings.errors.s3SaveFailed"),
      });
    }
  };

  const activateStorage = async (nextType: StorageType) => {
    setLoadState({ kind: "saving" });
    try {
      if (nextType === "S3") {
        await settingsApi.setS3Config(s3Config);
      }

      await settingsApi.setStorageType(nextType);
      setStorageType(nextType);
      setLoadState({
        kind: "success",
        message: t("storageSettings.state.storageSaved"),
      });
    } catch {
      setLoadState({
        kind: "error",
        message: t("storageSettings.errors.storageSaveFailed"),
      });
    }
  };

  return (
    <Stack spacing={2}>
      <Paper sx={{ overflow: "hidden" }}>
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

          {loadState.kind === "error" && (
            <Alert severity="error">{loadState.message}</Alert>
          )}
          {loadState.kind === "success" && (
            <Alert severity="success">{loadState.message}</Alert>
          )}

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
              <MenuItem value="S3">{t("storageSettings.storageType.S3")}</MenuItem>
            </Select>
          </FormControl>

          <Stack spacing={2}>
            <Typography variant="subtitle1" fontWeight={700}>
              {t("storageSettings.s3.title")}
            </Typography>
            <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
              <TextField
                label={t("storageSettings.s3.fields.endpoint")}
                value={s3Config.endpoint}
                onChange={(event) =>
                  updateS3Config("endpoint", event.target.value)
                }
                disabled={isBusy}
                fullWidth
              />
              <TextField
                label={t("storageSettings.s3.fields.region")}
                value={s3Config.region}
                onChange={(event) =>
                  updateS3Config("region", event.target.value)
                }
                disabled={isBusy}
                fullWidth
              />
            </Stack>
            <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
              <TextField
                label={t("storageSettings.s3.fields.bucket")}
                value={s3Config.bucket}
                onChange={(event) =>
                  updateS3Config("bucket", event.target.value)
                }
                disabled={isBusy}
                fullWidth
              />
              <TextField
                label={t("storageSettings.s3.fields.accessKey")}
                value={s3Config.accessKey}
                onChange={(event) =>
                  updateS3Config("accessKey", event.target.value)
                }
                disabled={isBusy}
                fullWidth
              />
            </Stack>
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
          </Stack>

          <Stack
            direction={{ xs: "column", sm: "row" }}
            spacing={1}
            useFlexGap
            sx={{ flexWrap: "wrap" }}
          >
            <Button
              variant="outlined"
              onClick={saveS3Config}
              disabled={isBusy}
            >
              {t("storageSettings.actions.saveS3")}
            </Button>
            <Button
              variant={storageType === "Local" ? "contained" : "outlined"}
              onClick={() => void activateStorage("Local")}
              disabled={isBusy}
            >
              {t("storageSettings.actions.useLocal")}
            </Button>
            <Button
              variant={storageType === "S3" ? "contained" : "outlined"}
              onClick={() => void activateStorage("S3")}
              disabled={isBusy}
            >
              {t("storageSettings.actions.useS3")}
            </Button>
          </Stack>
        </Stack>
      </Paper>
    </Stack>
  );
};
