import {
  Box,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  TextField,
  type SelectChangeEvent,
} from "@mui/material";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { UserRole } from "@features/auth/types";
import type { OidcProviderDto } from "@shared/api/oidcApi";
import { usePublicBaseUrlQuery } from "@shared/api/queries/serverSettings";
import {
  buildOidcCallbackUrl,
  resolveOidcCallbackBaseUrl,
  type OidcProviderFormState,
  type OidcProviderFormUpdater,
} from "./oidcProviderForm";

interface OidcProviderTextFieldsProps {
  form: OidcProviderFormState;
  provider: OidcProviderDto | null;
  saving: boolean;
  onUpdate: OidcProviderFormUpdater;
}

const getBrowserOrigin = (): string =>
  typeof window === "undefined" ? "" : window.location.origin;

export const OidcProviderTextFields = ({
  form,
  provider,
  saving,
  onUpdate,
}: OidcProviderTextFieldsProps) => {
  const { t } = useTranslation(["admin", "common"]);
  const publicBaseUrlQuery = usePublicBaseUrlQuery();

  const callbackUrl = useMemo(() => {
    const baseUrl = resolveOidcCallbackBaseUrl(
      publicBaseUrlQuery.data,
      getBrowserOrigin(),
    );
    return buildOidcCallbackUrl(baseUrl);
  }, [publicBaseUrlQuery.data]);

  const handleRoleChange = (event: SelectChangeEvent<UserRole>) => {
    const numericRole = Number(event.target.value);
    if (numericRole === UserRole.Admin || numericRole === UserRole.User) {
      onUpdate("defaultRole", numericRole);
    }
  };

  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: {
          xs: "minmax(0, 1fr)",
          sm: "repeat(2, minmax(0, 1fr))",
        },
        gap: 2,
      }}
    >
      <TextField
        label={t("identityProviders.fields.name")}
        value={form.name}
        onChange={(event) => onUpdate("name", event.target.value)}
        autoFocus
        fullWidth
        disabled={saving}
        required
      />
      <TextField
        label={t("identityProviders.fields.redirectUrl")}
        value={callbackUrl}
        fullWidth
        helperText={t("identityProviders.help.redirectUrl")}
        slotProps={{ input: { readOnly: true } }}
        sx={{ gridColumn: { xs: "auto", sm: "1 / -1" } }}
      />
      <TextField
        label={t("identityProviders.fields.issuer")}
        value={form.issuer}
        onChange={(event) => onUpdate("issuer", event.target.value)}
        fullWidth
        disabled={saving}
        required
        sx={{ gridColumn: { xs: "auto", sm: "1 / -1" } }}
      />
      <TextField
        label={t("identityProviders.fields.clientId")}
        value={form.clientId}
        onChange={(event) => onUpdate("clientId", event.target.value)}
        fullWidth
        disabled={saving}
        required
      />
      <TextField
        label={t("identityProviders.fields.clientSecret")}
        type="password"
        value={form.clientSecret}
        onChange={(event) => onUpdate("clientSecret", event.target.value)}
        fullWidth
        disabled={saving || form.clearClientSecret}
        helperText={
          provider?.hasClientSecret
            ? t("identityProviders.help.clientSecretConfigured")
            : t("identityProviders.help.clientSecret")
        }
      />
      <TextField
        label={t("identityProviders.fields.scopes")}
        value={form.scopes}
        onChange={(event) => onUpdate("scopes", event.target.value)}
        fullWidth
        disabled={saving}
        helperText={t("identityProviders.help.scopes")}
      />
      <TextField
        label={t("identityProviders.fields.allowedEmailDomains")}
        value={form.allowedEmailDomains}
        onChange={(event) =>
          onUpdate("allowedEmailDomains", event.target.value)
        }
        fullWidth
        disabled={saving}
        helperText={t("identityProviders.help.allowedEmailDomains")}
      />
      <FormControl fullWidth>
        <InputLabel id="oidc-provider-default-role-label">
          {t("identityProviders.fields.defaultRole")}
        </InputLabel>
        <Select<UserRole>
          labelId="oidc-provider-default-role-label"
          label={t("identityProviders.fields.defaultRole")}
          value={form.defaultRole}
          onChange={handleRoleChange}
          disabled={saving}
        >
          <MenuItem value={UserRole.User}>{t("roles.user")}</MenuItem>
          <MenuItem value={UserRole.Admin} disabled={form.allowAccountCreation}>
            {t("roles.admin")}
          </MenuItem>
        </Select>
      </FormControl>
    </Box>
  );
};
