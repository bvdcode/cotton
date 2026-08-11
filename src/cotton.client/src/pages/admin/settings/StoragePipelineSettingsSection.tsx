import { Box, Stack, TextField, ToggleButton, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { StoragePipelineSettings } from "@shared/api/settingsApi";
import { SettingsSaveButton } from "./SettingsSaveButton";
import { SettingsSection } from "./SettingsSection";
import { SettingsToggleButtonGroup } from "./SettingsToggleButtonGroup";
import type { SaveStatus } from "./useAutoSavedSetting";

const bytesPerMiB = 1024 ** 2;

const formatChunkSize = (bytes: number): string => {
  const mib = bytes / bytesPerMiB;
  return `${Number(mib.toFixed(2)).toString()} MiB`;
};

interface StoragePipelineSettingsSectionProps {
  chunkSizeBytes: number;
  compressionLevelChanged: boolean;
  compressionLevelInput: string;
  disabled: boolean;
  onChunkSizeChange: (value: number | null) => void;
  onCipherChunkSizeChange: (value: number | null) => void;
  onCompressionLevelChange: (value: string) => void;
  onCompressionLevelSave: () => void;
  onEncryptionThreadsChange: (value: number | null) => void;
  pipelineStatus: SaveStatus;
  settings: StoragePipelineSettings;
  sectionStatus: SaveStatus;
  supportedChunkSizeBytes: number[];
}

export const StoragePipelineSettingsSection = ({
  chunkSizeBytes,
  compressionLevelChanged,
  compressionLevelInput,
  disabled,
  onChunkSizeChange,
  onCipherChunkSizeChange,
  onCompressionLevelChange,
  onCompressionLevelSave,
  onEncryptionThreadsChange,
  pipelineStatus,
  settings,
  sectionStatus,
  supportedChunkSizeBytes,
}: StoragePipelineSettingsSectionProps) => {
  const { t } = useTranslation("admin");

  return (
    <SettingsSection
      title={t("storageSettings.pipeline.title")}
      description={t("storageSettings.pipeline.description")}
      status={sectionStatus}
    >
      <Stack spacing={2}>
        <Box>
          <Typography variant="subtitle2" gutterBottom>
            {t("storageSettings.chunkSize.title")}
          </Typography>
          <SettingsToggleButtonGroup
            value={chunkSizeBytes}
            onChange={onChunkSizeChange}
            disabled={disabled}
            ariaLabel={t("storageSettings.chunkSize.ariaLabel")}
          >
            {supportedChunkSizeBytes.map((option) => (
              <ToggleButton key={option} value={option}>
                {formatChunkSize(option)}
              </ToggleButton>
            ))}
          </SettingsToggleButtonGroup>
        </Box>

        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          alignItems={{ xs: "stretch", sm: "flex-start" }}
        >
          <TextField
            label={t("storageSettings.pipeline.fields.compressionLevel")}
            value={compressionLevelInput}
            onChange={(event) => onCompressionLevelChange(event.target.value)}
            disabled={disabled}
            error={pipelineStatus === "error"}
            helperText={t("storageSettings.pipeline.compressionHelp", {
              min: settings.minCompressionLevel,
              max: settings.maxCompressionLevel,
            })}
            type="number"
            inputProps={{
              min: settings.minCompressionLevel,
              max: settings.maxCompressionLevel,
              step: 1,
            }}
            fullWidth
          />
          <SettingsSaveButton
            changed={compressionLevelChanged}
            disabled={disabled}
            label={t("settings.actions.save")}
            onSave={onCompressionLevelSave}
            saving={pipelineStatus === "saving"}
          />
        </Stack>

        <Box>
          <Typography variant="subtitle2" gutterBottom>
            {t("storageSettings.pipeline.fields.cipherChunkSize")}
          </Typography>
          <SettingsToggleButtonGroup
            value={settings.cipherChunkSizeBytes}
            onChange={onCipherChunkSizeChange}
            disabled={disabled}
            ariaLabel={t("storageSettings.pipeline.fields.cipherChunkSize")}
          >
            {settings.supportedCipherChunkSizeBytes.map((option) => (
              <ToggleButton key={option} value={option}>
                {formatChunkSize(option)}
              </ToggleButton>
            ))}
          </SettingsToggleButtonGroup>
        </Box>

        <Box>
          <Typography variant="subtitle2" gutterBottom>
            {t("storageSettings.pipeline.fields.encryptionThreads")}
          </Typography>
          <SettingsToggleButtonGroup
            value={settings.encryptionThreads}
            onChange={onEncryptionThreadsChange}
            disabled={disabled}
            ariaLabel={t("storageSettings.pipeline.fields.encryptionThreads")}
          >
            {settings.supportedEncryptionThreads.map((option) => (
              <ToggleButton key={option} value={option}>
                {option.toString()}
              </ToggleButton>
            ))}
          </SettingsToggleButtonGroup>
        </Box>
      </Stack>
    </SettingsSection>
  );
};
