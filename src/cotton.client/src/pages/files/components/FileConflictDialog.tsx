import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { ConflictAction } from "../utils/uploadConflicts";

interface FileConflictDialogProps {
  open: boolean;
  newName: string;
  canOverwrite: boolean;
  onResolve: (resolution: ConflictAction) => void;
  onExited: () => void;
}

export const FileConflictDialog = ({
  open,
  newName,
  canOverwrite,
  onResolve,
  onExited,
}: FileConflictDialogProps) => {
  const { t } = useTranslation(["files", "common"]);
  const renameLabel = t("conflicts.rename", { ns: "files", newName });

  return (
    <Dialog
      open={open}
      onClose={() => onResolve(ConflictAction.Cancel)}
      TransitionProps={{ onExited }}
      fullWidth
      maxWidth="sm"
    >
      <DialogTitle>{t("conflicts.title", { ns: "files" })}</DialogTitle>
      <DialogContent>
        <DialogContentText sx={{ overflowWrap: "anywhere" }}>
          {t(
            canOverwrite
              ? "conflicts.overwriteDescription"
              : "conflicts.description",
            { ns: "files", newName },
          )}
        </DialogContentText>
      </DialogContent>
      <DialogActions
        disableSpacing
        sx={{
          alignItems: "stretch",
          flexDirection: { xs: "column", md: "row" },
          gap: 1,
          px: { xs: 2, sm: 3 },
          pb: { xs: 2, sm: 3 },
          pt: 1,
        }}
      >
        <Box
          sx={{
            display: "grid",
            flexShrink: 0,
            gap: 1,
            gridTemplateColumns: "repeat(3, minmax(0, 1fr))",
            width: { xs: "100%", md: "auto" },
            "& > button": {
              minWidth: 0,
              px: 1,
              whiteSpace: { xs: "normal", md: "nowrap" },
            },
          }}
        >
          <Button onClick={() => onResolve(ConflictAction.Cancel)}>
            {t("common:actions.cancel")}
          </Button>
          <Button onClick={() => onResolve(ConflictAction.Skip)}>
            {t("conflicts.skip", { ns: "files" })}
          </Button>
          <Button onClick={() => onResolve(ConflictAction.SkipAll)}>
            {t("conflicts.skipAll", { ns: "files" })}
          </Button>
        </Box>
        <Box
          sx={{
            display: "grid",
            flex: { md: 1 },
            gap: 1,
            gridTemplateColumns: canOverwrite
              ? "minmax(0, 1fr) auto"
              : "minmax(0, 1fr)",
            minWidth: 0,
            width: { xs: "100%", md: "auto" },
          }}
        >
          <Button
            fullWidth
            onClick={() => onResolve(ConflictAction.Rename)}
            sx={{ minWidth: 0, overflow: "hidden" }}
            title={renameLabel}
            variant={canOverwrite ? "outlined" : "contained"}
          >
            <Box
              component="span"
              sx={{
                minWidth: 0,
                overflow: "hidden",
                textOverflow: "ellipsis",
                whiteSpace: "nowrap",
              }}
            >
              {renameLabel}
            </Box>
          </Button>
          {canOverwrite && (
            <Button
              onClick={() => onResolve(ConflictAction.Overwrite)}
              variant="contained"
            >
              {t("conflicts.overwrite", { ns: "files" })}
            </Button>
          )}
        </Box>
      </DialogActions>
    </Dialog>
  );
};
