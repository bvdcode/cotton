import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogContentText,
  DialogActions,
  Button,
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

  return (
    <Dialog
      open={open}
      onClose={() => onResolve(ConflictAction.Cancel)}
      TransitionProps={{ onExited }}
    >
      <DialogTitle>{t("conflicts.title", { ns: "files" })}</DialogTitle>
      <DialogContent>
        <DialogContentText>
          {t(
            canOverwrite
              ? "conflicts.overwriteDescription"
              : "conflicts.description",
            { ns: "files", newName },
          )}
        </DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={() => onResolve(ConflictAction.Cancel)}>
          {t("common:actions.cancel")}
        </Button>
        <Button onClick={() => onResolve(ConflictAction.Skip)}>
          {t("conflicts.skip", { ns: "files" })}
        </Button>
        <Button onClick={() => onResolve(ConflictAction.SkipAll)}>
          {t("conflicts.skipAll", { ns: "files" })}
        </Button>
        <Button
          onClick={() => onResolve(ConflictAction.Rename)}
          variant={canOverwrite ? "text" : "contained"}
        >
          {t("conflicts.rename", { ns: "files", newName })}
        </Button>
        {canOverwrite && (
          <Button
            onClick={() => onResolve(ConflictAction.Overwrite)}
            variant="contained"
          >
            {t("conflicts.overwrite", { ns: "files" })}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
};
