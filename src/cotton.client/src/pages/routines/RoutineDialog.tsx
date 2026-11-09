import { ExpandMore, FolderOpen } from "@mui/icons-material";
import {
  Alert,
  Button,
  Collapse,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  InputAdornment,
  MenuItem,
  Stack,
  TextField,
  useMediaQuery,
} from "@mui/material";
import { useMutation } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  routinesApi,
  routineDraftSchema,
  RoutineScheduleKind,
  RoutineCalendarFrequency,
  calendarSchema,
  type Routine,
} from "@shared/api/routinesApi";
import type { NodeDto } from "@shared/api/layoutsApi";
import { useDecryptedNode } from "@shared/crypto/useDecryptedNodes";
import { FolderPickerDialog } from "@shared/ui/FolderPickerDialog";
import { defaultRoutine, intervalOptions, routineToDraft } from "./routineForm";
import { RoutineScheduleFields } from "./RoutineScheduleFields";
import { RoutineCleanupFields } from "./RoutineCleanupFields";

interface Props {
  routine: Routine | null;
  timezone: string;
  onClose: () => void;
  onSaved: () => Promise<void>;
}
export const RoutineDialog = ({
  routine,
  timezone,
  onClose,
  onSaved,
}: Props) => {
  const { t } = useTranslation("routines");
  const mobile = useMediaQuery((theme) => theme.breakpoints.down("sm"));
  const nameRef = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(true);
  const [draft, setDraft] = useState(() =>
    routine ? routineToDraft(routine) : defaultRoutine(),
  );
  const [advanced, setAdvanced] = useState(false);
  const [picking, setPicking] = useState(false);
  const [folder, setFolder] = useState<NodeDto | null>(
    routine?.targetNode ?? null,
  );
  const displayFolder = useDecryptedNode(folder);
  const save = useMutation({
    mutationFn: () => routinesApi.save(routine?.id ?? null, draft),
    onSuccess: async () => {
      await onSaved();
      setOpen(false);
    },
  });
  const valid =
    routineDraftSchema.safeParse(draft).success &&
    (draft.scheduleKind !== RoutineScheduleKind.Calendar ||
      calendarSchema.safeParse(draft.calendar).success);
  const close = () => {
    if (!save.isPending) {
      setOpen(false);
    }
  };
  return (
    <>
      <Dialog
        open={open}
        onClose={close}
        maxWidth="sm"
        fullWidth
        fullScreen={mobile}
        slotProps={{
          paper: { elevation: 8 },
          transition: {
            onExited: onClose,
            onEntered: () => nameRef.current?.select(),
          },
        }}
      >
        <DialogTitle>{routine ? t("editTitle") : t("createTitle")}</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2.5}>
            {save.isError && <Alert severity="error">{t("saveError")}</Alert>}
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                fullWidth
                label={t("name")}
                value={draft.name}
                autoFocus
                inputRef={nameRef}
                onChange={(event) =>
                  setDraft({ ...draft, name: event.target.value })
                }
                slotProps={{ htmlInput: { maxLength: 200 } }}
              />
              <TextField fullWidth select label={t("type")} value={draft.type}>
                <MenuItem value={0}>{t("deleteFiles")}</MenuItem>
              </TextField>
            </Stack>
            <TextField
              fullWidth
              label={t("workingFolder")}
              value={displayFolder?.name ?? ""}
              placeholder={t("chooseFolder")}
              onClick={() => setPicking(true)}
              slotProps={{
                input: {
                  readOnly: true,
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton
                        aria-label={t("chooseFolder")}
                        onClick={() => setPicking(true)}
                      >
                        <FolderOpen />
                      </IconButton>
                    </InputAdornment>
                  ),
                },
              }}
            />
            {draft.scheduleKind === RoutineScheduleKind.Interval && (
              <TextField
                select
                label={t("frequency")}
                value={draft.intervalHours}
                onChange={(event) =>
                  setDraft({
                    ...draft,
                    intervalHours: Number(event.target.value),
                  })
                }
              >
                {intervalOptions.map((hours) => (
                  <MenuItem key={hours} value={hours}>
                    {t(`intervals.${hours}`)}
                  </MenuItem>
                ))}
              </TextField>
            )}
            {draft.scheduleKind === RoutineScheduleKind.Calendar &&
              draft.calendar && (
                <TextField
                  select
                  label={t("frequency")}
                  value={draft.calendar.frequency}
                  onChange={(event) => {
                    if (draft.calendar) {
                      setDraft({
                        ...draft,
                        calendar: {
                          ...draft.calendar,
                          frequency: calendarSchema.shape.frequency.parse(
                            Number(event.target.value),
                          ),
                        },
                      });
                    }
                  }}
                >
                  {[
                    RoutineCalendarFrequency.Daily,
                    RoutineCalendarFrequency.Weekly,
                    RoutineCalendarFrequency.Monthly,
                    RoutineCalendarFrequency.Yearly,
                  ].map((frequency) => (
                    <MenuItem key={frequency} value={frequency}>
                      {t(`calendarFrequency.${frequency}`)}
                    </MenuItem>
                  ))}
                </TextField>
              )}
            <RoutineCleanupFields draft={draft} setDraft={setDraft} />
            <Button
              size="small"
              color="inherit"
              endIcon={
                <ExpandMore
                  sx={{
                    transform: advanced ? "rotate(180deg)" : "none",
                    transition: (theme) =>
                      theme.transitions.create("transform"),
                  }}
                />
              }
              onClick={() => setAdvanced(!advanced)}
              aria-expanded={advanced}
              sx={{ alignSelf: "flex-start" }}
            >
              {advanced ? t("lessSettings") : t("moreSettings")}
            </Button>
          </Stack>
          <Collapse in={advanced}>
            <Stack sx={{ pt: 2 }}>
              <RoutineScheduleFields
                value={draft}
                onChange={setDraft}
                timezone={timezone}
              />
            </Stack>
          </Collapse>
        </DialogContent>
        <DialogActions>
          <Button color="inherit" onClick={close} disabled={save.isPending}>
            {t("cancel")}
          </Button>
          <Button
            variant="contained"
            disabled={!valid}
            loading={save.isPending}
            onClick={() => save.mutate()}
          >
            {t(
              !routine && draft.enabled && !draft.startAtLocal
                ? "createAndRun"
                : "save",
            )}
          </Button>
        </DialogActions>
      </Dialog>
      {picking && (
        <FolderPickerDialog
          initialNodeId={draft.targetNodeId || undefined}
          onClose={() => setPicking(false)}
          onSelect={(selected) => {
            setFolder(selected);
            setDraft({ ...draft, targetNodeId: selected.id });
          }}
        />
      )}
    </>
  );
};
