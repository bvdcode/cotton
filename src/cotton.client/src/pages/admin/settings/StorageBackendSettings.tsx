import SaveIcon from "@mui/icons-material/Save";
import {
  Box,
  Button,
  CircularProgress,
  MenuItem,
  Stack,
  TextField,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { S3Config, StorageType } from "@shared/api/settingsApi";
import { SettingsSection } from "./SettingsSection";
import type { SaveStatus } from "./useAutoSavedSetting";

interface StorageBackendSettingsProps {
  onS3Change: (config: S3Config) => void;
  onSaveS3: () => void;
  onStorageTypeChange: (storageType: StorageType) => void;
  s3Config: S3Config;
  s3Disabled: boolean;
  s3Saving: boolean;
  s3Status: SaveStatus;
  storageType: StorageType;
  storageTypeDisabled: boolean;
  storageTypeStatus: SaveStatus;
}

export const StorageBackendSettings = ({
  onS3Change,
  onSaveS3,
  onStorageTypeChange,
  s3Config,
  s3Disabled,
  s3Saving,
  s3Status,
  storageType,
  storageTypeDisabled,
  storageTypeStatus,
}: StorageBackendSettingsProps) => {
  const { t } = useTranslation("admin");

  const updateS3Config = <K extends keyof S3Config>(
    key: K,
    value: S3Config[K],
  ) => {
    onS3Change({ ...s3Config, [key]: value });
  };

  return (
    <>
      <SettingsSection
        title={t("storageSettings.fields.storageType")}
        status={storageTypeStatus}
      >
        <TextField
          select
          value={storageType}
          onChange={(event) =>
            onStorageTypeChange(event.target.value as StorageType)
          }
          disabled={storageTypeDisabled}
          fullWidth
        >
          <MenuItem value="Local">
            {t("storageSettings.storageType.Local")}
          </MenuItem>
          <MenuItem value="S3">
            {t("storageSettings.storageType.S3")}
          </MenuItem>
        </TextField>
      </SettingsSection>

      {storageType === "S3" && (
        <SettingsSection
          title={t("storageSettings.s3.title")}
          status={s3Status}
        >
          <Stack spacing={2}>
            <Box
              sx={{
                display: "grid",
                gap: 2,
                gridTemplateColumns: {
                  xs: "1fr",
                  md: "1fr 1fr",
                },
              }}
            >
              <TextField
                label={t("storageSettings.s3.fields.endpoint")}
                value={s3Config.endpoint}
                onChange={(event) =>
                  updateS3Config("endpoint", event.target.value)
                }
                disabled={s3Disabled}
                fullWidth
              />
              <TextField
                label={t("storageSettings.s3.fields.region")}
                value={s3Config.region}
                onChange={(event) =>
                  updateS3Config("region", event.target.value)
                }
                disabled={s3Disabled}
                fullWidth
              />
              <TextField
                label={t("storageSettings.s3.fields.bucket")}
                value={s3Config.bucket}
                onChange={(event) =>
                  updateS3Config("bucket", event.target.value)
                }
                disabled={s3Disabled}
                fullWidth
              />
              <TextField
                label={t("storageSettings.s3.fields.accessKey")}
                value={s3Config.accessKey}
                onChange={(event) =>
                  updateS3Config("accessKey", event.target.value)
                }
                disabled={s3Disabled}
                fullWidth
              />
              <TextField
                label={t("storageSettings.s3.fields.secretKey")}
                value={s3Config.secretKey}
                onChange={(event) =>
                  updateS3Config("secretKey", event.target.value)
                }
                disabled={s3Disabled}
                type="password"
                fullWidth
              />
            </Box>

            <Box>
              <Button
                variant="contained"
                onClick={onSaveS3}
                disabled={s3Disabled || s3Saving}
                startIcon={
                  s3Saving ? (
                    <CircularProgress size={16} color="inherit" />
                  ) : (
                    <SaveIcon />
                  )
                }
              >
                {t("settings.actions.save")}
              </Button>
            </Box>
          </Stack>
        </SettingsSection>
      )}
    </>
  );
};
