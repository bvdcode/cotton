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
import { filesApi } from "@shared/api/filesApi";
import { isAxiosError } from "@shared/api/httpClient";
import { layoutsApi } from "@shared/api/layoutsApi";
import { useCopyFeedback } from "@shared/hooks/useCopyFeedback";
import {
  selectShareLinkExpireAfterMinutes,
  useUserPreferencesStore,
} from "@shared/store/userPreferencesStore";
import { shareLinks } from "@shared/utils/shareLinks";
import {
  getShareLinkDuration,
  getShareLinkExpirationMinutes,
  isValidShareLinkName,
  shareLinkDurationUnits,
  type ShareLinkDurationUnit,
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
  const [initialDuration] = useState(() =>
    getShareLinkDuration(
      selectShareLinkExpireAfterMinutes(useUserPreferencesStore.getState()),
    ),
  );
  const [allowUpload, setAllowUpload] = useState(false);
  const [hideFolderContent, setHideFolderContent] = useState(false);
  const [name, setName] = useState("");
  const [duration, setDuration] = useState(initialDuration.value);
  const [unit, setUnit] = useState<ShareLinkDurationUnit>(initialDuration.unit);
  const [pending, setPending] = useState(false);
  const [link, setLink] = useState("");
  const [error, setError] = useState("");
  const [copied, markCopied] = useCopyFeedback();
  const minutes = getShareLinkExpirationMinutes(duration, unit);
  const validName = isValidShareLinkName(name);
  const locked = pending || link.length > 0;
  const closeDialog = () => {
    if (!pending) {
      setOpen(false);
    }
  };

  const createLink = async () => {
    if (minutes === undefined || !validName || locked) {
      return;
    }
    setPending(true);
    setError("");
    try {
      let result: string;
      switch (target.kind) {
        case "file":
          result = await filesApi.getDownloadLink(
            target.id,
            minutes,
            name.trim(),
          );
          break;
        case "folder":
          result = await layoutsApi.getNodeShareLink(
            target.id,
            minutes,
            name.trim(),
            allowUpload,
            hideFolderContent,
          );
          break;
      }
      const token = shareLinks.tryExtractTokenFromDownloadUrl(result);
      if (!token) {
        setError(t("share.errors.token"));
        return;
      }
      setLink(shareLinks.buildShareUrl(token));
    } catch (failure) {
      setError(
        isAxiosError(failure) && failure.response?.status === 409
          ? t("share.options.nameTaken")
          : t("share.errors.link"),
      );
    } finally {
      setPending(false);
    }
  };

  const copyLink = async () => {
    try {
      await navigator.clipboard.writeText(link);
      markCopied();
      setError("");
    } catch {
      setError(t("share.errors.copy"));
    }
  };
  const copyLabel = copied
    ? t("common:actions.copied")
    : t("common:actions.copy");

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
            void createLink();
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
                    checked={allowUpload}
                    disabled={locked}
                    onChange={(_, checked) => {
                      setAllowUpload(checked);
                      if (!checked) {
                        setHideFolderContent(false);
                      }
                    }}
                  />
                }
              />
              <FormControlLabel
                label={t("share.options.hideFolderContent")}
                control={
                  <Switch
                    checked={hideFolderContent}
                    disabled={locked || !allowUpload}
                    onChange={(_, checked) => setHideFolderContent(checked)}
                  />
                }
              />
            </Stack>
          )}
          {error && (
            <Collapse in appear timeout={reducedMotion ? 0 : "auto"}>
              <Alert severity="error">{error}</Alert>
            </Collapse>
          )}
          <TextField
            label={t("share.options.name")}
            value={name}
            onChange={(event) => {
              setName(event.target.value);
              setError("");
            }}
            helperText={
              validName
                ? t("share.options.nameHint")
                : t("share.options.invalidName")
            }
            error={!validName}
            disabled={locked}
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
            {unit !== "never" && (
              <TextField
                label={t("share.options.duration")}
                type="number"
                value={duration}
                onChange={(event) => setDuration(event.target.value)}
                error={minutes === undefined}
                disabled={locked}
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
              value={unit}
              disabled={locked}
              fullWidth
              onChange={(event) => {
                const selected = shareLinkDurationUnits.find(
                  (entry) => entry.key === event.target.value,
                );
                if (selected) {
                  setUnit(selected.key);
                  if (duration.length === 0) {
                    setDuration("1");
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
          {minutes === undefined && (
            <Typography variant="caption" color="error">
              {t("share.options.invalidDuration")}
            </Typography>
          )}
          {link && (
            <Collapse
              in
              appear
              timeout={reducedMotion ? 0 : "auto"}
              onEntered={() => linkInputRef.current?.focus()}
            >
              <Box sx={{ pt: 1 }}>
                <TextField
                  label={t("share.options.link")}
                  value={link}
                  inputRef={linkInputRef}
                  fullWidth
                  slotProps={{
                    input: {
                      readOnly: true,
                      endAdornment: (
                        <InputAdornment position="end">
                          <Tooltip title={copyLabel}>
                            <IconButton
                              aria-label={copyLabel}
                              onClick={() => void copyLink()}
                              edge="end"
                            >
                              {copied ? <Check /> : <ContentCopy />}
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
        <Button color="inherit" onClick={closeDialog} disabled={pending}>
          {link ? t("common:actions.close") : t("common:actions.cancel")}
        </Button>
        {!link && (
          <Button
            type="submit"
            variant="contained"
            loading={pending}
            disabled={!validName || minutes === undefined}
          >
            {t("share.options.create")}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
};
