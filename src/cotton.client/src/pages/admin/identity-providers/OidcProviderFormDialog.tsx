import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
} from "@mui/material";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { OidcProviderDto } from "@shared/api/oidcApi";
import {
  useCreateOidcProviderMutation,
  useUpdateOidcProviderMutation,
} from "@shared/api/queries/oidc";
import {
  buildOidcProviderRequest,
  createEmptyOidcProviderForm,
  createOidcProviderFormFromDto,
  isOidcProviderFormValid,
  type OidcProviderFormState,
} from "./oidcProviderForm";
import { OidcProviderOptions } from "./OidcProviderOptions";
import { OidcProviderTextFields } from "./OidcProviderTextFields";

interface OidcProviderFormDialogProps {
  open: boolean;
  provider: OidcProviderDto | null;
  onClose: () => void;
}

export const OidcProviderFormDialog = ({
  open,
  provider,
  onClose,
}: OidcProviderFormDialogProps) => {
  if (!open) {
    return null;
  }

  return <OidcProviderFormDialogContent provider={provider} onClose={onClose} />;
};

interface OidcProviderFormDialogContentProps {
  provider: OidcProviderDto | null;
  onClose: () => void;
}

const OidcProviderFormDialogContent = ({
  provider,
  onClose,
}: OidcProviderFormDialogContentProps) => {
  const { t } = useTranslation(["admin", "common"]);
  const createMutation = useCreateOidcProviderMutation();
  const updateMutation = useUpdateOidcProviderMutation();
  const [form, setForm] = useState<OidcProviderFormState>(() =>
    provider ? createOidcProviderFormFromDto(provider) : createEmptyOidcProviderForm(),
  );
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setForm(
      provider ? createOidcProviderFormFromDto(provider) : createEmptyOidcProviderForm(),
    );
    setError(null);
  }, [provider]);

  const saving = createMutation.isPending || updateMutation.isPending;
  const title = provider
    ? t("identityProviders.edit.title")
    : t("identityProviders.create.title");
  const canSave = useMemo(() => isOidcProviderFormValid(form), [form]);

  const updateField = <K extends keyof OidcProviderFormState>(
    key: K,
    value: OidcProviderFormState[K],
  ) => {
    setForm((current) => ({ ...current, [key]: value }));
  };

  const handleSave = async () => {
    if (!canSave || saving) return;

    setError(null);
    const request = buildOidcProviderRequest(form);

    try {
      if (provider) {
        await updateMutation.mutateAsync({
          providerId: provider.id,
          request,
        });
      } else {
        await createMutation.mutateAsync(request);
      }

      onClose();
    } catch {
      setError(t("identityProviders.errors.saveFailed"));
    }
  };

  const handleClose = () => {
    if (!saving) {
      onClose();
    }
  };

  return (
    <Dialog open onClose={handleClose} maxWidth="md" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2.5}>
          {error && <Alert severity="error">{error}</Alert>}

          <OidcProviderTextFields
            form={form}
            provider={provider}
            saving={saving}
            onUpdate={updateField}
          />

          <OidcProviderOptions
            form={form}
            provider={provider}
            saving={saving}
            onUpdate={updateField}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} disabled={saving}>
          {t("actions.cancel", { ns: "common" })}
        </Button>
        <Button
          variant="contained"
          onClick={() => void handleSave()}
          disabled={!canSave || saving}
        >
          {saving ? (
            <Stack direction="row" spacing={1} alignItems="center">
              <CircularProgress color="inherit" size={16} />
              <span>{t("identityProviders.actions.saving")}</span>
            </Stack>
          ) : (
            t("identityProviders.actions.save")
          )}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
