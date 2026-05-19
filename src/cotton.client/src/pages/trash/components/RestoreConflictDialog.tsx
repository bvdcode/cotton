import React, { useState } from "react";
import {
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  FormControlLabel,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { RestoreConflictKind } from "../../../shared/api/nodesApi";
import type { PromptDecision } from "../hooks/useTrashRestoreActions";

type Prompt =
  | { kind: "confirm"; restorePath: string }
  | { kind: "parentMissing"; missingPath: string }
  | { kind: "conflict"; conflictKind: RestoreConflictKind; conflictName: string };

type RestoreConflictDialogProps = {
  open: boolean;
  itemName: string;
  prompt: Prompt | null;
  showApplyToAll: boolean;
  onAnswer: (decision: PromptDecision) => void;
};

export const RestoreConflictDialog: React.FC<RestoreConflictDialogProps> = ({
  open,
  itemName,
  prompt,
  showApplyToAll,
  onAnswer,
}) => {
  if (!prompt) {
    return null;
  }

  const promptKey = prompt.kind === "confirm"
    ? `${itemName}:confirm:${prompt.restorePath}`
    : prompt.kind === "parentMissing"
      ? `${itemName}:parent:${prompt.missingPath}`
      : `${itemName}:conflict:${prompt.conflictKind}:${prompt.conflictName}`;

  return (
    <RestoreConflictDialogContent
      key={promptKey}
      open={open}
      itemName={itemName}
      prompt={prompt}
      showApplyToAll={showApplyToAll}
      onAnswer={onAnswer}
    />
  );
};

const RestoreConflictDialogContent: React.FC<
  Omit<RestoreConflictDialogProps, "prompt"> & { prompt: Prompt }
> = ({
  open,
  itemName,
  prompt,
  showApplyToAll,
  onAnswer,
}) => {
  const { t } = useTranslation(["trash", "common"]);
  const [applyToAll, setApplyToAll] = useState(false);

  const isConfirm = prompt.kind === "confirm";
  const isParentMissing = prompt.kind === "parentMissing";
  const title = isConfirm
    ? t("restore.confirm.title")
    : isParentMissing
      ? t("restore.parentMissing.title")
      : t("restore.conflict.title");
  const description = isConfirm
    ? t("restore.confirm.description", {
        name: itemName,
        path: prompt.restorePath || t("restore.rootFolder"),
      })
    : isParentMissing
      ? t("restore.parentMissing.description", {
          name: itemName,
          path: prompt.missingPath || t("restore.rootFolder"),
        })
      : t("restore.conflict.description", {
          name: itemName,
          conflictName: prompt.conflictName,
          kind:
            prompt.conflictKind === "Folder"
              ? t("restore.conflict.folderKind")
              : t("restore.conflict.fileKind"),
        });
  const confirmLabel = isConfirm
    ? t("restore.confirm.confirm")
    : isParentMissing
      ? t("restore.parentMissing.confirm")
      : t("restore.conflict.confirm");

  return (
    <Dialog open={open} maxWidth="sm" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <DialogContentText>{description}</DialogContentText>
        {showApplyToAll && (
          <FormControlLabel
            sx={{ mt: 1 }}
            control={
              <Checkbox
                checked={applyToAll}
                onChange={(event) => setApplyToAll(event.target.checked)}
              />
            }
            label={t("restore.applyToAll")}
          />
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={() => onAnswer({ action: "skip", applyToAll })}>
          {t("restore.skip")}
        </Button>
        <Button
          variant="contained"
          color={isParentMissing || isConfirm ? "primary" : "error"}
          onClick={() => onAnswer({ action: "apply", applyToAll })}
        >
          {confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
