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
  onResolve: (resolution: ConflictAction) => void;
  onExited: () => void;
}

export const FileConflictDialog = ({
  open,
  newName,
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
          {t("conflicts.description", { ns: "files", newName })}
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
          variant="contained"
        >
          {t("common:actions.confirm")}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
