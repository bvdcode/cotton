import { Add, Refresh } from "@mui/icons-material";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useConfirm } from "material-ui-confirm";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { routinesApi, type Routine } from "@shared/api/routinesApi";
import { destructiveConfirmOptions } from "@shared/ui/confirmOptions";
import { usePageTitle } from "@shared/hooks/usePageTitle";
import { RoutineCard } from "./RoutineCard";
import { RoutineDialog } from "./RoutineDialog";
import { useRoutines } from "./useRoutines";

export const RoutinesPage = () => {
  const { t } = useTranslation("routines");
  usePageTitle(t("title"));
  const confirm = useConfirm();
  const routines = useRoutines();
  const options = useQuery({
    queryKey: ["routineOptions"],
    queryFn: routinesApi.options,
  });
  const [editor, setEditor] = useState<{ routine: Routine | null } | null>(
    null,
  );
  const run = useMutation({
    mutationFn: routinesApi.run,
    onSuccess: routines.refresh,
  });
  const remove = useMutation({
    mutationFn: routinesApi.delete,
    onSuccess: routines.refresh,
  });
  const deleteRoutine = async (routine: Routine) => {
    const { confirmed } = await confirm({
      ...destructiveConfirmOptions,
      title: t("deleteRoutine"),
      description: t("deleteConfirm", { name: routine.name }),
      confirmationText: t("deleteRoutine"),
      cancellationText: t("cancel"),
    });
    if (confirmed) {
      remove.mutate(routine.id);
    }
  };
  return (
    <Box
      sx={{
        width: "100%",
        display: "flex",
        justifyContent: "center",
        flex: 1,
        minHeight: 0,
        pt: { xs: 1, md: 3 },
        pb: { xs: 1, md: 2 },
      }}
    >
      <Paper
        sx={{
          width: "100%",
          maxWidth: 1200,
          minHeight: 0,
          display: "flex",
          flexDirection: "column",
          overflow: "hidden",
        }}
      >
        <Stack
          spacing={2}
          sx={{
            p: { xs: 2, sm: 3 },
            overflow: "auto",
            scrollbarWidth: "thin",
            flex: 1,
          }}
        >
          <Stack
            direction="row"
            spacing={1}
            useFlexGap
            sx={{ alignItems: "center" }}
          >
            <Typography
              component="h1"
              variant="h5"
              sx={{ flex: 1, fontWeight: 700 }}
            >
              {t("title")}
            </Typography>
            <Tooltip title={t("refresh")}>
              <span>
                <IconButton
                  aria-label={t("refresh")}
                  disabled={routines.isFetching}
                  onClick={() => {
                    void routines.refresh();
                  }}
                >
                  <Refresh />
                </IconButton>
              </span>
            </Tooltip>
            <Tooltip title={t("create")}>
              <span>
                <Button
                  variant="contained"
                  aria-label={t("create")}
                  disabled={!options.data}
                  onClick={() => setEditor({ routine: null })}
                  sx={{ minWidth: { xs: 40, sm: 64 }, gap: { sm: 1 } }}
                >
                  <Add />
                  <Box
                    component="span"
                    sx={{ display: { xs: "none", sm: "inline" } }}
                  >
                    {t("create")}
                  </Box>
                </Button>
              </span>
            </Tooltip>
          </Stack>
          {(routines.isError || options.isError) && (
            <Alert severity="error">{t("loadError")}</Alert>
          )}
          {(run.isError || remove.isError) && (
            <Alert severity="error">{t("actionError")}</Alert>
          )}
          {routines.isPending && (
            <Stack
              sx={{ flex: 1, alignItems: "center", justifyContent: "center" }}
            >
              <CircularProgress />
            </Stack>
          )}
          {routines.data?.length === 0 && (
            <Stack
              spacing={2}
              sx={{
                flex: 1,
                alignItems: "center",
                justifyContent: "center",
                textAlign: "center",
              }}
            >
              <Typography variant="h6">{t("empty")}</Typography>
            </Stack>
          )}
          <Stack spacing={2}>
            {routines.data?.map((routine) => (
              <RoutineCard
                key={routine.id}
                routine={routine}
                pending={
                  (run.isPending && run.variables === routine.id) ||
                  (remove.isPending && remove.variables === routine.id)
                }
                onRun={() => run.mutate(routine.id)}
                onEdit={() => setEditor({ routine })}
                onDelete={() => {
                  void deleteRoutine(routine);
                }}
              />
            ))}
          </Stack>
        </Stack>
      </Paper>
      {editor && options.data && (
        <RoutineDialog
          routine={editor.routine}
          timezone={options.data.timezone}
          onClose={() => setEditor(null)}
          onSaved={routines.refresh}
        />
      )}
    </Box>
  );
};
