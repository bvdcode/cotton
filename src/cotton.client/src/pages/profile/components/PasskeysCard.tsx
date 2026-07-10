import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  Stack,
  TextField,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import KeyOutlinedIcon from "@mui/icons-material/KeyOutlined";
import PhonelinkLockOutlinedIcon from "@mui/icons-material/PhonelinkLockOutlined";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  passkeysApi,
  type PasskeyAuthenticatorKind,
  type PasskeyCredential,
} from "../../../shared/api/passkeysApi";
import {
  isPasskeySupported,
  serializeAttestationCredential,
  toCredentialCreationOptions,
} from "../../../shared/passkeys/webauthn";
import { resolvePasskeyDisplayName } from "../../../shared/passkeys/passkeyDisplay";
import { ProfileAccordionCard } from "./ProfileAccordionCard";

const passkeyCancellationErrorNames = new Set([
  "AbortError",
  "NotAllowedError",
]);

const isPasskeyCreationCancelled = (error: unknown): boolean => {
  return (
    typeof DOMException !== "undefined" &&
    error instanceof DOMException &&
    passkeyCancellationErrorNames.has(error.name)
  );
};

const formatDateTime = (iso: string): string => {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }

  return new Intl.DateTimeFormat(undefined, {
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
};

const defaultNameKeys: Record<
  PasskeyAuthenticatorKind,
  | "passkeys.defaultNames.passkey"
  | "passkeys.defaultNames.securityKey"
  | "passkeys.defaultNames.device"
> = {
  Unknown: "passkeys.defaultNames.passkey",
  SecurityKey: "passkeys.defaultNames.securityKey",
  Device: "passkeys.defaultNames.device",
};

export const PasskeysCard = () => {
  const { t } = useTranslation("profile");
  const theme = useTheme();
  const fullScreenRenameDialog = useMediaQuery(theme.breakpoints.down("sm"));
  const [credentials, setCredentials] = useState<PasskeyCredential[]>([]);
  const [loading, setLoading] = useState(true);
  const [adding, setAdding] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [renameCredential, setRenameCredential] =
    useState<PasskeyCredential | null>(null);
  const [renameName, setRenameName] = useState("");
  const [renaming, setRenaming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const localizedDefaultNames: Record<PasskeyAuthenticatorKind, string> = {
    Unknown: t(defaultNameKeys.Unknown),
    SecurityKey: t(defaultNameKeys.SecurityKey),
    Device: t(defaultNameKeys.Device),
  };

  useEffect(() => {
    let mounted = true;

    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const response = await passkeysApi.list();
        if (mounted) {
          setCredentials(response);
        }
      } catch {
        if (mounted) {
          setError(t("passkeys.errors.loadFailed"));
        }
      } finally {
        if (mounted) {
          setLoading(false);
        }
      }
    };

    void load();

    return () => {
      mounted = false;
    };
  }, [t]);

  const openRenameDialog = (credential: PasskeyCredential) => {
    setRenameCredential(credential);
    setRenameName(credential.label ?? "");
  };

  const closeRenameDialog = () => {
    if (renaming) return;
    setRenameCredential(null);
    setRenameName("");
  };

  const handleAdd = async () => {
    if (!isPasskeySupported()) {
      setError(t("passkeys.errors.notSupported"));
      return;
    }

    setAdding(true);
    setError(null);
    try {
      const optionsResponse = await passkeysApi.beginRegistration(null);
      const credential = await navigator.credentials.create({
        publicKey: toCredentialCreationOptions(optionsResponse.options),
      });

      if (!(credential instanceof PublicKeyCredential)) {
        setError(t("passkeys.errors.cancelled"));
        return;
      }

      const serializedCredential = serializeAttestationCredential(credential);
      const saved = await passkeysApi.finishRegistration(
        optionsResponse.requestId,
        null,
        serializedCredential,
      );
      setCredentials((current) => [saved, ...current]);
    } catch (caught) {
      setError(
        isPasskeyCreationCancelled(caught)
          ? t("passkeys.errors.cancelled")
          : t("passkeys.errors.addFailed"),
      );
    } finally {
      setAdding(false);
    }
  };

  const handleRename = async () => {
    if (!renameCredential || renaming) return;

    const trimmedName = renameName.trim();
    setRenaming(true);
    setError(null);
    try {
      const updated = await passkeysApi.setLabel(
        renameCredential.id,
        trimmedName || null,
      );
      setCredentials((current) =>
        current.map((credential) =>
          credential.id === updated.id ? updated : credential,
        ),
      );
      closeRenameDialog();
    } catch {
      setError(t("passkeys.errors.renameFailed"));
    } finally {
      setRenaming(false);
    }
  };

  const handleDelete = async (credentialId: string) => {
    setDeletingId(credentialId);
    setError(null);
    try {
      await passkeysApi.delete(credentialId);
      setCredentials((current) =>
        current.filter((credential) => credential.id !== credentialId),
      );
    } catch {
      setError(t("passkeys.errors.deleteFailed"));
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <>
      <ProfileAccordionCard
        id="passkeys-header"
        ariaControls="passkeys-content"
        icon={<KeyOutlinedIcon color="primary" />}
        title={t("passkeys.title")}
        description={t("passkeys.description")}
        count={credentials.length}
      >
        <Stack spacing={2} paddingY={2}>
          <Box>
            <Button
              variant="contained"
              startIcon={
                adding ? (
                  <CircularProgress color="inherit" size={16} />
                ) : (
                  <AddIcon />
                )
              }
              onClick={handleAdd}
              disabled={adding || loading}
            >
              {adding ? t("passkeys.adding") : t("passkeys.add")}
            </Button>
          </Box>

          {error && <Alert severity="error">{error}</Alert>}

          {loading ? (
            <Box display="flex" alignItems="center" gap={1.5}>
              <CircularProgress size={18} />
              <Typography variant="body2" color="text.secondary">
                {t("passkeys.loading")}
              </Typography>
            </Box>
          ) : credentials.length === 0 ? (
            <Alert severity="info">{t("passkeys.empty")}</Alert>
          ) : (
            <Stack spacing={1}>
              {credentials.map((credential) => {
                const title = resolvePasskeyDisplayName(
                  credential,
                  localizedDefaultNames,
                );

                return (
                  <Box
                    key={credential.id}
                    sx={{
                      display: "flex",
                      alignItems: "center",
                      gap: 1.5,
                      py: 1,
                      borderBottom: "1px solid",
                      borderColor: "divider",
                      "&:last-of-type": {
                        borderBottom: 0,
                      },
                    }}
                  >
                    <PhonelinkLockOutlinedIcon color="action" />
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      <Typography fontWeight={600} noWrap title={title}>
                        {title}
                      </Typography>
                      <Typography variant="body2" color="text.secondary" noWrap>
                        {credential.lastUsedAt
                          ? t("passkeys.lastUsed", {
                              date: formatDateTime(credential.lastUsedAt),
                            })
                          : t("passkeys.created", {
                              date: formatDateTime(credential.createdAt),
                            })}
                      </Typography>
                    </Box>
                    <Tooltip title={t("passkeys.rename.button")}>
                      <span>
                        <IconButton
                          aria-label={t("passkeys.rename.button")}
                          onClick={() => openRenameDialog(credential)}
                          disabled={Boolean(deletingId)}
                        >
                          <EditOutlinedIcon />
                        </IconButton>
                      </span>
                    </Tooltip>
                    <Tooltip title={t("passkeys.delete")}>
                      <span>
                        <IconButton
                          aria-label={t("passkeys.delete")}
                          color="error"
                          onClick={() => void handleDelete(credential.id)}
                          disabled={deletingId === credential.id}
                        >
                          {deletingId === credential.id ? (
                            <CircularProgress color="inherit" size={18} />
                          ) : (
                            <DeleteOutlineIcon />
                          )}
                        </IconButton>
                      </span>
                    </Tooltip>
                  </Box>
                );
              })}
            </Stack>
          )}
        </Stack>
      </ProfileAccordionCard>

      <Dialog
        open={Boolean(renameCredential)}
        onClose={closeRenameDialog}
        maxWidth="xs"
        fullWidth
        fullScreen={fullScreenRenameDialog}
        aria-labelledby="passkey-rename-dialog-title"
        aria-describedby="passkey-rename-dialog-description"
      >
        <DialogTitle id="passkey-rename-dialog-title">
          {t("passkeys.rename.title")}
        </DialogTitle>
        <Box
          component="form"
          onSubmit={(event) => {
            event.preventDefault();
            void handleRename();
          }}
        >
          <DialogContent>
            <Stack spacing={2} pt={1}>
              <Typography
                id="passkey-rename-dialog-description"
                variant="body2"
                color="text.secondary"
              >
                {t("passkeys.rename.description")}
              </Typography>
              <TextField
                autoFocus
                label={t("passkeys.rename.nameLabel")}
                value={renameName}
                onChange={(event) => setRenameName(event.target.value)}
                fullWidth
              />
            </Stack>
          </DialogContent>
          <DialogActions sx={{ flexWrap: "wrap" }}>
            <Button
              type="button"
              onClick={closeRenameDialog}
              disabled={renaming}
            >
              {t("passkeys.rename.cancel")}
            </Button>
            <Button type="submit" variant="contained" disabled={renaming}>
              {renaming ? (
                <>
                  <CircularProgress color="inherit" size={16} sx={{ mr: 1 }} />
                  {t("passkeys.rename.saving")}
                </>
              ) : (
                t("passkeys.rename.save")
              )}
            </Button>
          </DialogActions>
        </Box>
      </Dialog>
    </>
  );
};
