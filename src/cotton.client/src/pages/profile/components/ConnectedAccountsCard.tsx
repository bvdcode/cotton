import {
  Alert,
  Avatar,
  Box,
  Button,
  CircularProgress,
  Divider,
  Stack,
  Typography,
} from "@mui/material";
import LinkIcon from "@mui/icons-material/Link";
import LinkOffIcon from "@mui/icons-material/LinkOff";
import LoginIcon from "@mui/icons-material/Login";
import { useConfirm } from "material-ui-confirm";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { oidcApi, type UserExternalIdentityDto } from "@shared/api/oidcApi";
import {
  useOidcLinksQuery,
  usePublicOidcProvidersQuery,
  useUnlinkOidcIdentityMutation,
} from "@shared/api/queries/oidc";
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

export const ConnectedAccountsCard = () => {
  const { t } = useTranslation("profile");
  const confirm = useConfirm();
  const linksQuery = useOidcLinksQuery();
  const providersQuery = usePublicOidcProvidersQuery();
  const unlinkMutation = useUnlinkOidcIdentityMutation();
  const [error, setError] = useState<string | null>(null);
  const [linkingSlug, setLinkingSlug] = useState<string | null>(null);

  const links = useMemo(() => linksQuery.data ?? [], [linksQuery.data]);
  const providers = useMemo(
    () => providersQuery.data ?? [],
    [providersQuery.data],
  );
  const linkedSlugs = useMemo(
    () => new Set(links.map((identity) => identity.providerSlug)),
    [links],
  );
  const linkableProviders = providers.filter(
    (provider) => !linkedSlugs.has(provider.slug),
  );

  const loading = linksQuery.isLoading || providersQuery.isLoading;
  const loadFailed = linksQuery.isError || providersQuery.isError;

  const handleLink = async (providerSlug: string) => {
    setError(null);
    setLinkingSlug(providerSlug);
    try {
      const authorizationUrl = await oidcApi.createLinkAuthorizationUrl(
        providerSlug,
        "/settings",
      );
      window.location.assign(authorizationUrl);
    } catch {
      setError(t("connectedAccounts.errors.linkFailed"));
      setLinkingSlug(null);
    }
  };

  const handleUnlink = async (identity: UserExternalIdentityDto) => {
    const result = await confirm({
      title: t("connectedAccounts.unlink.title", {
        provider: identity.providerName,
      }),
      description: t("connectedAccounts.unlink.description"),
      confirmationText: t("connectedAccounts.unlink.confirm"),
      cancellationText: t("connectedAccounts.unlink.cancel"),
    });

    if (!result.confirmed) return;

    setError(null);
    try {
      await unlinkMutation.mutateAsync(identity.id);
    } catch {
      setError(t("connectedAccounts.errors.unlinkFailed"));
    }
  };

  return (
    <ProfileAccordionCard
      id="connected-accounts-header"
      ariaControls="connected-accounts-content"
      icon={<LinkIcon color="primary" />}
      title={t("connectedAccounts.title")}
      description={t("connectedAccounts.description")}
      count={links.length}
    >
      <Stack spacing={2} paddingY={2}>
        {loadFailed && (
          <Alert severity="error">
            {t("connectedAccounts.errors.loadFailed")}
          </Alert>
        )}
        {error && <Alert severity="error">{error}</Alert>}

        {loading ? (
          <Box display="flex" alignItems="center" gap={1.5}>
            <CircularProgress size={18} />
            <Typography variant="body2" color="text.secondary">
              {t("connectedAccounts.loading")}
            </Typography>
          </Box>
        ) : (
          <>
            {links.length === 0 ? (
              <Alert severity="info">{t("connectedAccounts.empty")}</Alert>
            ) : (
              <Stack spacing={0} divider={<Divider />}>
                {links.map((identity) => (
                  <ConnectedAccountRow
                    key={identity.id}
                    identity={identity}
                    unlinking={unlinkMutation.isPending}
                    onUnlink={() => void handleUnlink(identity)}
                  />
                ))}
              </Stack>
            )}

            {linkableProviders.length > 0 && (
              <Stack direction="row" flexWrap="wrap" gap={1}>
                {linkableProviders.map((provider) => {
                  const linking = linkingSlug === provider.slug;
                  return (
                    <Button
                      key={provider.slug}
                      type="button"
                      variant="outlined"
                      startIcon={
                        linking ? (
                          <CircularProgress color="inherit" size={16} />
                        ) : (
                          <LoginIcon />
                        )
                      }
                      onClick={() => void handleLink(provider.slug)}
                      disabled={linkingSlug !== null}
                    >
                      {t("connectedAccounts.link", { provider: provider.name })}
                    </Button>
                  );
                })}
              </Stack>
            )}
          </>
        )}
      </Stack>
    </ProfileAccordionCard>
  );
};

interface ConnectedAccountRowProps {
  identity: UserExternalIdentityDto;
  unlinking: boolean;
  onUnlink: () => void;
}

const ConnectedAccountRow = ({
  identity,
  unlinking,
  onUnlink,
}: ConnectedAccountRowProps) => (
  <Box
    sx={{
      display: "flex",
      alignItems: "center",
      gap: 1.5,
      py: 1.25,
    }}
  >
    <Avatar src={identity.pictureUrl ?? undefined}>
      {identity.providerName.slice(0, 1).toUpperCase()}
    </Avatar>
    <Box sx={{ flex: 1, minWidth: 0 }}>
      <Typography fontWeight={600} noWrap>
        {identity.providerName}
      </Typography>
      <Typography variant="body2" color="text.secondary" noWrap>
        {identity.email ?? identity.displayName ?? identity.providerSlug}
      </Typography>
      {identity.lastUsedAt && (
        <Typography variant="caption" color="text.secondary">
          {formatDateTime(identity.lastUsedAt)}
        </Typography>
      )}
    </Box>
    <Button
      type="button"
      color="error"
      variant="outlined"
      startIcon={
        unlinking ? (
          <CircularProgress color="inherit" size={16} />
        ) : (
          <LinkOffIcon />
        )
      }
      onClick={onUnlink}
      disabled={unlinking}
    >
      <ConnectedAccountUnlinkLabel />
    </Button>
  </Box>
);

const ConnectedAccountUnlinkLabel = () => {
  const { t } = useTranslation("profile");
  return <>{t("connectedAccounts.unlink.button")}</>;
};
