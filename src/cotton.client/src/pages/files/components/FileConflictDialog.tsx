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
import {
  ConflictAction,
  type NameConflictPrompt,
} from "../../../shared/types/nameConflict";

interface FileConflictDialogProps extends NameConflictPrompt {
  open: boolean;
  onResolve: (resolution: ConflictAction) => void;
  onExited: () => void;
}

export const FileConflictDialog = ({
  open,
  newName,
  canOverwrite,
  canMerge,
  canApplyToAll,
  onResolve,
  onExited,
}: FileConflictDialogProps) => {
  const { t } = useTranslation(["files", "common"]);
  const renameLabel = t("conflicts.rename", { ns: "files", newName });
  const renameNameOffset = renameLabel.indexOf(newName);
  const renameCharacters = Array.from(newName);
  const renameMiddle = Math.ceil(renameCharacters.length / 2);
  const hasPrimaryAction = canOverwrite || canMerge;
  const primaryActionCount =
    Number(canMerge) +
    Number(canOverwrite) +
    Number(canOverwrite && canApplyToAll);
  const descriptionKey = canMerge
    ? "conflicts.mergeDescription"
    : canOverwrite
      ? "conflicts.overwriteDescription"
      : "conflicts.description";

  return (
    <Dialog
      open={open}
      onClose={() => onResolve(ConflictAction.Cancel)}
      fullWidth
      maxWidth="sm"
      slotProps={{
        transition: { onExited },
      }}
    >
      <DialogTitle>{t("conflicts.title", { ns: "files" })}</DialogTitle>
      <DialogContent>
        <DialogContentText
          sx={{
            color: "text.primary",
            overflowWrap: "anywhere",
          }}
        >
          {t(descriptionKey, { ns: "files", newName })}
        </DialogContentText>
      </DialogContent>
      <DialogActions
        disableSpacing
        sx={{
          alignItems: "stretch",
          flexDirection: "column",
          gap: 1,
          px: { xs: 2, sm: 3 },
          pb: { xs: 2, sm: 3 },
          pt: 1,
        }}
      >
        <Box
          sx={{
            display: "flex",
            flexWrap: "wrap",
            gap: 1,

            "& > button": {
              flexGrow: 1,
              minWidth: "max-content",
              px: 1,
              whiteSpace: "normal",
            },
          }}
        >
          <Button
            color="inherit"
            onClick={() => onResolve(ConflictAction.Cancel)}
          >
            {t("common:actions.cancel")}
          </Button>
          <Button
            color="inherit"
            onClick={() => onResolve(ConflictAction.Skip)}
          >
            {t("conflicts.skip", { ns: "files" })}
          </Button>
          {canApplyToAll && (
            <Button
              color="inherit"
              onClick={() => onResolve(ConflictAction.SkipAll)}
            >
              {t("conflicts.skipAll", { ns: "files" })}
            </Button>
          )}
        </Box>
        <Box
          sx={{
            display: "grid",
            gap: 1,
            gridTemplateColumns: hasPrimaryAction
              ? {
                  xs: `repeat(${primaryActionCount}, minmax(0, 1fr))`,
                  sm: `minmax(0, 1fr) repeat(${primaryActionCount}, auto)`,
                }
              : "minmax(0, 1fr)",
            minWidth: 0,
          }}
        >
          <Button
            aria-label={renameLabel}
            color={hasPrimaryAction ? "inherit" : "primary"}
            fullWidth
            onClick={() => onResolve(ConflictAction.Rename)}
            sx={{
              gridColumn: { xs: "1 / -1", sm: "auto" },
              minWidth: 0,
              overflow: "hidden",
            }}
            title={renameLabel}
            variant={hasPrimaryAction ? "outlined" : "contained"}
          >
            <Box
              component="span"
              sx={{
                display: "inline-flex",
                minWidth: 0,
                whiteSpace: "pre",
              }}
            >
              <Box
                component="span"
                sx={{
                  flexShrink: 0,
                }}
              >
                {renameLabel.slice(0, renameNameOffset)}
              </Box>
              <Box
                component="span"
                sx={{
                  display: "inline-flex",
                  minWidth: 0,
                  textTransform: "none",
                }}
              >
                <Box
                  component="span"
                  sx={{
                    minWidth: 0,
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                  }}
                >
                  {renameCharacters.slice(0, renameMiddle).join("")}
                </Box>
                <Box
                  component="span"
                  sx={{
                    display: "flex",
                    justifyContent: "flex-end",
                    minWidth: 0,
                    overflow: "hidden",
                  }}
                >
                  <Box
                    component="span"
                    sx={{
                      flexShrink: 0,
                    }}
                  >
                    {renameCharacters.slice(renameMiddle).join("")}
                  </Box>
                </Box>
              </Box>
              <Box
                component="span"
                sx={{
                  flexShrink: 0,
                }}
              >
                {renameLabel.slice(renameNameOffset + newName.length)}
              </Box>
            </Box>
          </Button>
          {canMerge && (
            <Button
              variant="contained"
              onClick={() => onResolve(ConflictAction.Merge)}
            >
              {t("conflicts.merge", { ns: "files" })}
            </Button>
          )}
          {canOverwrite && (
            <>
              <Button
                color="inherit"
                onClick={() => onResolve(ConflictAction.Overwrite)}
                variant={canApplyToAll ? "outlined" : "contained"}
              >
                {t("conflicts.overwrite", { ns: "files" })}
              </Button>
              {canApplyToAll && (
                <Button
                  onClick={() => onResolve(ConflictAction.OverwriteAll)}
                  variant="contained"
                >
                  {t("conflicts.overwriteAll", { ns: "files" })}
                </Button>
              )}
            </>
          )}
        </Box>
      </DialogActions>
    </Dialog>
  );
};
