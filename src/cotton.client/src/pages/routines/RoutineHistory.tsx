import { ExpandMore } from "@mui/icons-material";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  CircularProgress,
  Divider,
  Stack,
  Typography,
} from "@mui/material";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { routinesApi, type RoutineRun } from "@shared/api/routinesApi";
import { useAuthStore } from "@shared/store/authStore";

export const RoutineRunDetails = ({ run }: { run: RoutineRun }) => {
  const { t, i18n } = useTranslation("routines");
  return (
    <Stack spacing={0.5}>
      <Typography variant="body2">
        {new Intl.DateTimeFormat(i18n.language, {
          dateStyle: "medium",
          timeStyle: "short",
        }).format(new Date(run.createdAt))}
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {t(`statuses.${run.status}`)} ·{" "}
        {t("counts", {
          processed: run.processedCount,
          deleted: run.deletedCount,
        })}
      </Typography>
      {run.error && <Alert severity="error">{t(`errors.${run.error}`)}</Alert>}
    </Stack>
  );
};
export const RoutineHistory = ({
  id,
  latestRun,
}: {
  id: string;
  latestRun: RoutineRun | null;
}) => {
  const { t } = useTranslation("routines");
  const [expanded, setExpanded] = useState(false);
  const userId = useAuthStore((state) => state.user?.id);
  const runs = useQuery({
    queryKey: ["routineRuns", userId, id],
    queryFn: () => routinesApi.runs(id),
    enabled: expanded,
  });
  return (
    <Accordion
      disableGutters
      elevation={0}
      expanded={expanded}
      onChange={(_, value) => setExpanded(value)}
      sx={{ background: "transparent" }}
    >
      <AccordionSummary
        aria-label={t("history")}
        expandIcon={<ExpandMore />}
        sx={{
          px: 2,
          "& .MuiAccordionSummary-content": {
            gap: 1,
            flexWrap: "wrap",
            alignItems: "center",
          },
        }}
      >
        <Typography variant="body2" sx={{ flex: 1 }}>
          {t("history")}
        </Typography>
        {latestRun && (
          <Typography variant="caption" color="text.secondary">
            {t(`statuses.${latestRun.status}`)}
          </Typography>
        )}
      </AccordionSummary>
      <AccordionDetails sx={{ px: 2 }}>
        {runs.isPending && <CircularProgress size={24} />}
        {runs.isError && <Alert severity="error">{t("loadError")}</Alert>}
        {runs.data?.length === 0 && (
          <Typography variant="body2" color="text.secondary">
            {t("noRuns")}
          </Typography>
        )}
        <Stack spacing={2} divider={<Divider />}>
          {runs.data?.map((run) => (
            <RoutineRunDetails key={run.id} run={run} />
          ))}
        </Stack>
      </AccordionDetails>
    </Accordion>
  );
};
