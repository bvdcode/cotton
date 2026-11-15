import { Check, ContentCopy } from "@mui/icons-material";
import {
  Alert,
  Box,
  Button,
  Collapse,
  FormControlLabel,
  Switch,
  Dialog,
  DialogActions,
  DialogContent,
  IconButton,
  InputAdornment,
  MenuItem,
  Stack,
  TextField,
  Tooltip,
  Typography,
  useMediaQuery,
} from "@mui/material";
import { useId, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useShareLinkForm } from "@shared/hooks/useShareLinkForm";
import {
  shareLinkDurationUnits,
  type ShareLinkTarget,
} from "@shared/utils/shareLinkOptions";

import { ShareLinkDialogTitle } from "./ShareLinkDialogTitle";

interface ShareLinkDialogProps {
  target: ShareLinkTarget;
  onClose: () => void;
}

export const ShareLinkDialog = ({ target, onClose }: ShareLinkDialogProps) => {
  const { t } = useTranslation(["files", "common"]);
  const titleId = useId();
  const reducedMotion = useMediaQuery("(prefers-reduced-motion: reduce)");
  const linkInputRef = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(true);
  const form = useShareLinkForm(target);
  const closeDialog = () => {
    if (!form.pending) {
      setOpen(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={closeDialog}
      transitionDuration={reducedMotion ? 0 : undefined}
      onClick={(event) => event.stopPropagation()}
      onKeyDown={(event) => {
        if (event.key !== "Escape") {
          event.stopPropagation();
        }
      }}
      fullWidth
      maxWidth="xs"
      aria-labelledby={titleId}
      slotProps={{
        transition: { onExited: onClose },
        paper: {
          component: "form",
          sx: {
            "& .MuiInputLabel-root.Mui-focused:not(.Mui-error)": {
              color: "text.primary",
            },
            "& .MuiOutlinedInput-root.Mui-focused:not(.Mui-error) .MuiOutlinedInput-notchedOutline":
              {
                borderColor: "text.primary",
              },
          },
          onSubmit: (event: React.FormEvent<HTMLDivElement>) => {
            event.preventDefault();
            void form.createLink();
          },
        },
      }}
    >
      <ShareLinkDialogTitle id={titleId} name={target.name} />
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {target.kind === "folder" && (
            <Stack>
              <FormControlLabel
                label={t("share.options.allowUpload")}
                control={
                  <Switch
                    checked={form.allowUpload}
                    disabled={form.locked}
                    onChange={(_, checked) => {
                      form.setAllowUpload(checked);
                      if (!checked) {
                        form.setHideFolderContent(false);
                      }
                    }}
                  />
                }
              />
              <FormControlLabel
                label={t("share.options.hideFolderContent")}
                control={
                  <Switch
                    checked={form.hideFolderContent}
                    disabled={form.locked || !form.allowUpload}
                    onChange={(_, checked) =>
                      form.setHideFolderContent(checked)
                    }
                  />
                }
              />
            </Stack>
          )}
          {form.error && (
            <Collapse in appear timeout={reducedMotion ? 0 : "auto"}>
              <Alert severity="error">{form.error}</Alert>
            </Collapse>
          )}
          <TextField
            label={t("share.options.name")}
            value={form.name}
            onChange={(event) => {
              form.setName(event.target.value);
              form.setError("");
            }}
            helperText={
              form.validName
                ? t("share.options.nameHint")
                : t("share.options.invalidName")
            }
            error={!form.validName}
            disabled={form.locked}
            autoComplete="off"
            autoFocus
            fullWidth
            slotProps={{
              input: {
                startAdornment: (
                  <InputAdornment position="start">/s/</InputAdornment>
                ),
              },
            }}
          />
          <Stack direction="row" spacing={1}>
            {form.unit !== "never" && (
              <TextField
                label={t("share.options.duration")}
                type="number"
                value={form.duration}
                onChange={(event) => form.setDuration(event.target.value)}
                error={form.minutes === undefined}
                disabled={form.locked}
                fullWidth
                slotProps={{
                  htmlInput: {
                    step: "any",
                  },
                }}
              />
            )}
            <TextField
              select
              label={t("share.options.unit")}
              value={form.unit}
              disabled={form.locked}
              fullWidth
              onChange={(event) => {
                const selected = shareLinkDurationUnits.find(
                  (entry) => entry.key === event.target.value,
                );
                if (selected) {
                  form.setUnit(selected.key);
                  if (form.duration.length === 0) {
                    form.setDuration("1");
                  }
                }
              }}
            >
              {shareLinkDurationUnits.map((entry) => (
                <MenuItem key={entry.key} value={entry.key}>
                  {t(`share.options.units.${entry.key}`)}
                </MenuItem>
              ))}
            </TextField>
          </Stack>
          {form.minutes === undefined && (
            <Typography variant="caption" color="error">
              {t("share.options.invalidDuration")}
            </Typography>
          )}
          {form.link && (
            <Collapse
              in
              appear
              timeout={reducedMotion ? 0 : "auto"}
              onEntered={() => linkInputRef.current?.focus()}
            >
              <Box sx={{ pt: 1 }}>
                <TextField
                  label={t("share.options.link")}
                  value={form.link}
                  inputRef={linkInputRef}
                  fullWidth
                  slotProps={{
                    input: {
                      readOnly: true,
                      endAdornment: (
                        <InputAdornment position="end">
                          <Tooltip title={form.copyLabel}>
                            <IconButton
                              aria-label={form.copyLabel}
                              onClick={() => void form.copyLink()}
                              edge="end"
                            >
                              {form.copied ? <Check /> : <ContentCopy />}
                            </IconButton>
                          </Tooltip>
                        </InputAdornment>
                      ),
                    },
                  }}
                />
              </Box>
            </Collapse>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button color="inherit" onClick={closeDialog} disabled={form.pending}>
          {form.link ? t("common:actions.close") : t("common:actions.cancel")}
        </Button>
        {!form.link && (
          <Button
            type="submit"
            variant="contained"
            loading={form.pending}
            disabled={!form.validName || form.minutes === undefined}
          >
            {t("share.options.create")}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
};
