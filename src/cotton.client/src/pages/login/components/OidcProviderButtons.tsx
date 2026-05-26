import { Button, CircularProgress, Divider, Stack, Typography } from "@mui/material";
import LoginIcon from "@mui/icons-material/Login";
import { useTranslation } from "react-i18next";
import { oidcApi, type PublicOidcProviderDto } from "@shared/api/oidcApi";
import { usePublicOidcProvidersQuery } from "@shared/api/queries/oidc";

interface OidcProviderButtonsProps {
  disabled: boolean;
  returnUrl: string;
  trustDevice: boolean;
  visible: boolean;
}

export const OidcProviderButtons = ({
  disabled,
  returnUrl,
  trustDevice,
  visible,
}: OidcProviderButtonsProps) => {
  const { t } = useTranslation("login");
  const providersQuery = usePublicOidcProvidersQuery(visible);

  if (!visible) {
    return null;
  }

  if (providersQuery.isLoading) {
    return (
      <Stack alignItems="center" spacing={1}>
        <CircularProgress size={18} />
        <Typography variant="body2" color="text.secondary">
          {t("oidc.loading")}
        </Typography>
      </Stack>
    );
  }

  const providers = providersQuery.data ?? [];
  if (providers.length === 0) {
    return null;
  }

  return (
    <Stack spacing={1.5}>
      <Divider>{t("oidc.divider")}</Divider>
      <Stack spacing={1}>
        {providers.map((provider) => (
          <OidcProviderButton
            key={provider.slug}
            provider={provider}
            disabled={disabled}
            returnUrl={returnUrl}
            trustDevice={trustDevice}
          />
        ))}
      </Stack>
    </Stack>
  );
};

interface OidcProviderButtonProps {
  provider: PublicOidcProviderDto;
  disabled: boolean;
  returnUrl: string;
  trustDevice: boolean;
}

const OidcProviderButton = ({
  provider,
  disabled,
  returnUrl,
  trustDevice,
}: OidcProviderButtonProps) => {
  const { t } = useTranslation("login");

  const handleClick = () => {
    window.location.assign(
      oidcApi.buildSignInUrl(provider.slug, returnUrl, trustDevice),
    );
  };

  return (
    <Button
      type="button"
      variant="outlined"
      startIcon={<LoginIcon />}
      onClick={handleClick}
      disabled={disabled}
      fullWidth
    >
      {t("oidc.signInWith", { provider: provider.name })}
    </Button>
  );
};
