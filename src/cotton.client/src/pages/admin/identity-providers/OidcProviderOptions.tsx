import { Box, Checkbox, FormControlLabel } from "@mui/material";
import { useTranslation } from "react-i18next";
import { UserRole } from "@features/auth/types";
import type { OidcProviderDto } from "@shared/api/oidcApi";
import type {
  OidcProviderFormState,
  OidcProviderFormUpdater,
} from "./oidcProviderForm";

interface OidcProviderOptionsProps {
  form: OidcProviderFormState;
  provider: OidcProviderDto | null;
  saving: boolean;
  onUpdate: OidcProviderFormUpdater;
}

export const OidcProviderOptions = ({
  form,
  provider,
  saving,
  onUpdate,
}: OidcProviderOptionsProps) => {
  const { t } = useTranslation(["admin", "common"]);

  const handleAccountCreationChange = (checked: boolean) => {
    onUpdate("allowAccountCreation", checked);
    if (checked && form.defaultRole === UserRole.Admin) {
      onUpdate("defaultRole", UserRole.User);
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
        columnGap: 2,
      }}
    >
      <FormControlLabel
        control={
          <Checkbox
            checked={form.isEnabled}
            onChange={(event) => onUpdate("isEnabled", event.target.checked)}
            disabled={saving}
          />
        }
        label={t("identityProviders.fields.isEnabled")}
      />
      <FormControlLabel
        control={
          <Checkbox
            checked={form.allowAccountCreation}
            onChange={(event) =>
              handleAccountCreationChange(event.target.checked)
            }
            disabled={saving}
          />
        }
        label={t("identityProviders.fields.allowAccountCreation")}
      />
      {form.allowAccountCreation && (
        <FormControlLabel
          control={
            <Checkbox
              checked={form.requireVerifiedEmail}
              onChange={(event) =>
                onUpdate("requireVerifiedEmail", event.target.checked)
              }
              disabled={saving}
            />
          }
          label={t("identityProviders.fields.requireVerifiedEmail")}
        />
      )}
      <FormControlLabel
        control={
          <Checkbox
            checked={form.syncProfile}
            onChange={(event) => onUpdate("syncProfile", event.target.checked)}
            disabled={saving}
          />
        }
        label={t("identityProviders.fields.syncProfile")}
      />
      <FormControlLabel
        control={
          <Checkbox
            checked={form.syncAvatar}
            onChange={(event) => onUpdate("syncAvatar", event.target.checked)}
            disabled={saving}
          />
        }
        label={t("identityProviders.fields.syncAvatar")}
      />
      {provider?.hasClientSecret && (
        <FormControlLabel
          control={
            <Checkbox
              checked={form.clearClientSecret}
              onChange={(event) =>
                onUpdate("clearClientSecret", event.target.checked)
              }
              disabled={saving || form.clientSecret.trim().length > 0}
            />
          }
          label={t("identityProviders.fields.clearClientSecret")}
        />
      )}
    </Box>
  );
};
