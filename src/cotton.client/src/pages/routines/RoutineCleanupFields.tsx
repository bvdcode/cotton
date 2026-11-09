import {
  Alert,
  FormControlLabel,
  MenuItem,
  Stack,
  Switch,
  TextField,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { routineDraftSchema, type RoutineDraft } from "@shared/api/routinesApi";

export const RoutineCleanupFields = ({
  draft,
  setDraft,
}: {
  draft: RoutineDraft;
  setDraft: (draft: RoutineDraft) => void;
}) => {
  const { t } = useTranslation("routines");
  return (
    <Stack spacing={2.5}>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
        <TextField
          type="number"
          fullWidth
          label={t("olderThanDays")}
          value={draft.settings.olderThanDays}
          onChange={(event) =>
            setDraft({
              ...draft,
              settings: {
                ...draft.settings,
                olderThanDays: Number(event.target.value),
              },
            })
          }
          slotProps={{ htmlInput: { min: 1, max: 36500 } }}
        />
        <TextField
          select
          fullWidth
          label={t("dateField")}
          value={draft.settings.dateField}
          onChange={(event) =>
            setDraft({
              ...draft,
              settings: {
                ...draft.settings,
                dateField:
                  routineDraftSchema.shape.settings.shape.dateField.parse(
                    Number(event.target.value),
                  ),
              },
            })
          }
        >
          <MenuItem value={0}>{t("createdAt")}</MenuItem>
          <MenuItem value={1}>{t("updatedAt")}</MenuItem>
        </TextField>
      </Stack>
      <FormControlLabel
        control={
          <Switch
            checked={draft.settings.includeSubfolders}
            onChange={(_, checked) =>
              setDraft({
                ...draft,
                settings: {
                  ...draft.settings,
                  includeSubfolders: checked,
                },
              })
            }
          />
        }
        label={t("includeSubfolders")}
      />
      <TextField
        select
        label={t("deletionMode")}
        value={String(draft.settings.permanentlyDelete)}
        onChange={(event) =>
          setDraft({
            ...draft,
            settings: {
              ...draft.settings,
              permanentlyDelete: event.target.value === "true",
            },
          })
        }
      >
        <MenuItem value="false">{t("trash")}</MenuItem>
        <MenuItem value="true">{t("permanent")}</MenuItem>
      </TextField>
      {draft.settings.permanentlyDelete && (
        <Alert severity="warning">{t("permanentWarning")}</Alert>
      )}
    </Stack>
  );
};
