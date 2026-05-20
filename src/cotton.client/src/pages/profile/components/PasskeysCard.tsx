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
  type PasskeyCredential,
} from "../../../shared/api/passkeysApi";
import {
  isPasskeySupported,
  serializeAttestationCredential,
  toCredentialCreationOptions,
} from "../../../shared/passkeys/webauthn";
import { ProfileAccordionCard } from "./ProfileAccordionCard";

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

export const PasskeysCard = () => {
  const { t } = useTranslation("profile");
  const [credentials, setCredentials] = useState<PasskeyCredential[]>([]);
  const [loading, setLoading] = useState(true);
  const [adding, setAdding] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [renameCredential, setRenameCredential] =
    useState<PasskeyCredential | null>(null);
  const [renameName, setRenameName] = useState("");
  const [renaming, setRenaming] = useState(false);
  const [error, setError] = useState<string | null>(null);

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

  const buildDefaultName = (transports: string[]): string => {
    const normalized = new Set(transports.map((transport) => transport.toLowerCase()));
    if (
      normalized.has("usb") ||
      normalized.has("nfc") ||
      normalized.has("ble") ||
      normalized.has("smart-card")
    ) {
      return t("passkeys.defaultNames.securityKey");
    }

    if (normalized.has("internal") || normalized.has("hybrid")) {
      return t("passkeys.defaultNames.device");
    }

    return t("passkeys.defaultName", { count: credentials.length + 1 });
  };

  const openRenameDialog = (credential: PasskeyCredential) => {
    setRenameCredential(credential);
    setRenameName(credential.name);
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
        buildDefaultName(serializedCredential.transports),
        serializedCredential,
      );
      setCredentials((current) => [saved, ...current]);
      openRenameDialog(saved);
    } catch {
      setError(t("passkeys.errors.addFailed"));
    } finally {
      setAdding(false);
    }
  };

  const handleRename = async () => {
    if (!renameCredential) return;

    const trimmedName = renameName.trim();
    if (!trimmedName) return;

    setRenaming(true);
    setError(null);
    try {
      const updated = await passkeysApi.rename(renameCredential.id, trimmedName);
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
                adding ? <CircularProgress color="inherit" size={16} /> : <AddIcon />
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
              {credentials.map((credential) => (
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
                    <Typography fontWeight={600} noWrap>
                      {credential.name}
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
              ))}
            </Stack>
          )}
        </Stack>
      </ProfileAccordionCard>

      <Dialog
        open={Boolean(renameCredential)}
        onClose={closeRenameDialog}
        maxWidth="xs"
        fullWidth
      >
        <DialogTitle>{t("passkeys.rename.title")}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} pt={1}>
            <Typography variant="body2" color="text.secondary">
              {t("passkeys.rename.description")}
            </Typography>
            <TextField
              autoFocus
              label={t("passkeys.rename.nameLabel")}
              value={renameName}
              onChange={(event) => setRenameName(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  void handleRename();
                }
              }}
              fullWidth
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={closeRenameDialog} disabled={renaming}>
            {t("passkeys.rename.cancel")}
          </Button>
          <Button
            variant="contained"
            onClick={() => void handleRename()}
            disabled={renaming || !renameName.trim()}
          >
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
      </Dialog>
    </>
  );
};
