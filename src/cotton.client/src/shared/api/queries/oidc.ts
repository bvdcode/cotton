import {
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";
import {
  oidcApi,
  type OidcProviderDto,
  type OidcProviderRequestDto,
  type PublicOidcProviderDto,
  type UserExternalIdentityDto,
} from "../oidcApi";
import { queryKeys } from "./queryKeys";

const invalidateOidc = async (queryClient: QueryClient): Promise<void> => {
  await queryClient.invalidateQueries({ queryKey: queryKeys.oidc.all() });
};

export const usePublicOidcProvidersQuery = (enabled = true) =>
  useQuery<PublicOidcProviderDto[]>({
    queryKey: queryKeys.oidc.publicProviders(),
    queryFn: () => oidcApi.listPublicProviders(),
    enabled,
  });

export const useAdminOidcProvidersQuery = () =>
  useQuery<OidcProviderDto[]>({
    queryKey: queryKeys.oidc.adminProviders(),
    queryFn: () => oidcApi.listAdminProviders(),
  });

export const useOidcLinksQuery = () =>
  useQuery<UserExternalIdentityDto[]>({
    queryKey: queryKeys.oidc.links(),
    queryFn: () => oidcApi.listLinks(),
  });

export const useCreateOidcProviderMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: OidcProviderRequestDto) =>
      oidcApi.createProvider(request),
    onSuccess: () => invalidateOidc(queryClient),
  });
};

export const useUpdateOidcProviderMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (vars: {
      providerId: string;
      request: OidcProviderRequestDto;
    }) => oidcApi.updateProvider(vars.providerId, vars.request),
    onSuccess: () => invalidateOidc(queryClient),
  });
};

export const useDeleteOidcProviderMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (providerId: string) => oidcApi.deleteProvider(providerId),
    onSuccess: () => invalidateOidc(queryClient),
  });
};

export const useUnlinkOidcIdentityMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (identityId: string) => oidcApi.unlink(identityId),
    onSuccess: () => invalidateOidc(queryClient),
  });
};
