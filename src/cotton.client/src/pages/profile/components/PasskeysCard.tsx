import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
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
  const [name, setName] = useState("");
  const [loading, setLoading] = useState(true);
  const [adding, setAdding] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
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

  const handleAdd = async () => {
    if (!isPasskeySupported()) {
      setError(t("passkeys.errors.notSupported"));
      return;
    }

    setAdding(true);
    setError(null);
    try {
      const requestedName =
        name.trim() || t("passkeys.defaultName", { count: credentials.length + 1 });
      const optionsResponse = await passkeysApi.beginRegistration(requestedName);
      const credential = await navigator.credentials.create({
        publicKey: toCredentialCreationOptions(optionsResponse.options),
      });

      if (!(credential instanceof PublicKeyCredential)) {
        setError(t("passkeys.errors.cancelled"));
        return;
      }

      const saved = await passkeysApi.finishRegistration(
        optionsResponse.requestId,
        requestedName,
        serializeAttestationCredential(credential),
      );
      setCredentials((current) => [saved, ...current]);
      setName("");
    } catch {
      setError(t("passkeys.errors.addFailed"));
    } finally {
      setAdding(false);
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
    <ProfileAccordionCard
      id="passkeys-header"
      ariaControls="passkeys-content"
      icon={<KeyOutlinedIcon color="primary" />}
      title={t("passkeys.title")}
      description={t("passkeys.description")}
      count={credentials.length}
    >
      <Stack spacing={2} paddingY={2}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
          <TextField
            size="small"
            fullWidth
            label={t("passkeys.nameLabel")}
            placeholder={t("passkeys.namePlaceholder")}
            value={name}
            onChange={(event) => setName(event.target.value)}
            disabled={adding}
          />
          <Button
            variant="contained"
            startIcon={
              adding ? <CircularProgress color="inherit" size={16} /> : <AddIcon />
            }
            onClick={handleAdd}
            disabled={adding || loading}
            sx={{ whiteSpace: "nowrap" }}
          >
            {adding ? t("passkeys.adding") : t("passkeys.add")}
          </Button>
        </Stack>

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
  );
};
