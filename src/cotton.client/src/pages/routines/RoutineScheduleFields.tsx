import {
  FormControlLabel,
  MenuItem,
  Stack,
  Switch,
  TextField,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import {
  RoutineCalendarFrequency,
  RoutineScheduleKind,
  type RoutineDraft,
} from "@shared/api/routinesApi";

interface Props {
  value: RoutineDraft;
  onChange: (draft: RoutineDraft) => void;
  timezone?: string;
}
export const RoutineScheduleFields = ({ value, onChange, timezone }: Props) => {
  const { t, i18n } = useTranslation("routines");
  const calendar = value.calendar;
  const setCalendar = (
    patch: Partial<NonNullable<RoutineDraft["calendar"]>>,
  ) => {
    if (calendar) {
      onChange({ ...value, calendar: { ...calendar, ...patch } });
    }
  };
  return (
    <Stack spacing={2}>
      <FormControlLabel
        control={
          <Switch
            checked={value.enabled}
            onChange={(_, enabled) => onChange({ ...value, enabled })}
          />
        }
        label={t("enabled")}
      />
      <TextField
        type="datetime-local"
        label={t("startAt")}
        value={value.startAtLocal ?? ""}
        onChange={(event) =>
          onChange({ ...value, startAtLocal: event.target.value || null })
        }
        slotProps={{ inputLabel: { shrink: true } }}
        helperText={t("startHint", { timezone })}
      />
      <FormControlLabel
        control={
          <Switch
            checked={value.scheduleKind === RoutineScheduleKind.Calendar}
            onChange={(_, checked) =>
              onChange({
                ...value,
                scheduleKind: checked
                  ? RoutineScheduleKind.Calendar
                  : RoutineScheduleKind.Interval,
              })
            }
          />
        }
        label={t("calendarMode")}
      />
      {value.scheduleKind === RoutineScheduleKind.Calendar && calendar && (
        <Stack spacing={2}>
          <TextField
            type="time"
            label={t("time")}
            value={`${String(calendar.hour).padStart(2, "0")}:${String(calendar.minute).padStart(2, "0")}`}
            onChange={(event) => {
              const [hour, minute] = event.target.value.split(":").map(Number);
              setCalendar({ hour, minute });
            }}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          {calendar.frequency === RoutineCalendarFrequency.Weekly && (
            <TextField
              select
              label={t("weekday")}
              value={calendar.dayOfWeek}
              onChange={(event) =>
                setCalendar({ dayOfWeek: Number(event.target.value) })
              }
            >
              {Array.from({ length: 7 }, (_, day) => (
                <MenuItem key={day} value={day}>
                  {new Intl.DateTimeFormat(i18n.language, {
                    weekday: "long",
                    timeZone: "UTC",
                  }).format(new Date(Date.UTC(2026, 10, 1 + day)))}
                </MenuItem>
              ))}
            </TextField>
          )}
          {(calendar.frequency === RoutineCalendarFrequency.Monthly ||
            calendar.frequency === RoutineCalendarFrequency.Yearly) && (
            <TextField
              type="number"
              label={t("dayOfMonth")}
              value={calendar.dayOfMonth}
              onChange={(event) =>
                setCalendar({ dayOfMonth: Number(event.target.value) })
              }
              slotProps={{ htmlInput: { min: 1, max: 31 } }}
            />
          )}
          {calendar.frequency === RoutineCalendarFrequency.Yearly && (
            <TextField
              select
              label={t("month")}
              value={calendar.month}
              onChange={(event) =>
                setCalendar({ month: Number(event.target.value) })
              }
            >
              {Array.from({ length: 12 }, (_, index) => (
                <MenuItem key={index} value={index + 1}>
                  {new Intl.DateTimeFormat(i18n.language, {
                    month: "long",
                    timeZone: "UTC",
                  }).format(new Date(Date.UTC(2026, index, 1)))}
                </MenuItem>
              ))}
            </TextField>
          )}
          <Typography variant="caption" color="text.secondary">
            {t("calendarHint", { timezone })}
          </Typography>
        </Stack>
      )}
    </Stack>
  );
};
