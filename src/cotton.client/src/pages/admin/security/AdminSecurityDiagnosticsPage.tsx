import { Alert, Divider, Skeleton, Stack } from "@mui/material";
import SecurityIcon from "@mui/icons-material/Security";
import type { TFunction } from "i18next";
import { useTranslation } from "react-i18next";
import type { SecurityDiagnosticsDto } from "../../../shared/api/adminApi";
import { getApiErrorMessage } from "../../../shared/api/httpClient";
import { useSecurityDiagnosticsQuery } from "../../../shared/api/queries/admin";
import { AdminPageHeader } from "../components/AdminPageHeader";
import { AdminPageSurface } from "../components/AdminPageSurface";
import {
  ContainerDiagnosticsSection,
  InstanceDiagnosticsSection,
  MasterKeyDiagnosticsSection,
  MemoryDiagnosticsSection,
  RuntimeDiagnosticsSection,
} from "./SecurityDiagnosticsDetails";
import {
  SecurityPassedSection,
  SecurityScoreSummary,
} from "./SecurityDiagnosticsStatus";
import { SecurityRiskSection } from "./SecurityRiskSection";

interface SecurityDiagnosticsContentProps {
  diagnostics: SecurityDiagnosticsDto;
  t: TFunction<"admin">;
}

const SecurityDiagnosticsContent = ({
  diagnostics,
  t,
}: SecurityDiagnosticsContentProps) => (
  <Stack spacing={3} divider={<Divider flexItem />}>
    <SecurityScoreSummary diagnostics={diagnostics} t={t} />
    <SecurityRiskSection warnings={diagnostics.warnings} t={t} />
    <SecurityPassedSection diagnostics={diagnostics} t={t} />
    <InstanceDiagnosticsSection diagnostics={diagnostics} t={t} />
    <MasterKeyDiagnosticsSection diagnostics={diagnostics} t={t} />
    <MemoryDiagnosticsSection diagnostics={diagnostics} t={t} />
    <ContainerDiagnosticsSection diagnostics={diagnostics} t={t} />
    <RuntimeDiagnosticsSection diagnostics={diagnostics} t={t} />
  </Stack>
);

export const AdminSecurityDiagnosticsPage = () => {
  const { t } = useTranslation("admin");
  const diagnosticsQuery = useSecurityDiagnosticsQuery();
  const loadError = diagnosticsQuery.isError
    ? (getApiErrorMessage(diagnosticsQuery.error) ??
      t("securityDiagnostics.errors.loadFailed"))
    : null;

  return (
    <Stack>
      <AdminPageSurface>
        <Stack p={3} spacing={3} divider={<Divider flexItem />}>
          <AdminPageHeader
            title={t("securityDiagnostics.title")}
            description={t("securityDiagnostics.description")}
            icon={<SecurityIcon color="primary" />}
          />

          {diagnosticsQuery.isPending && (
            <Stack spacing={1.5}>
              <Skeleton variant="rounded" height={96} />
              <Skeleton variant="rounded" height={72} />
              <Skeleton variant="rounded" height={180} />
            </Stack>
          )}

          {loadError && <Alert severity="error">{loadError}</Alert>}

          {diagnosticsQuery.data && (
            <SecurityDiagnosticsContent
              diagnostics={diagnosticsQuery.data}
              t={t}
            />
          )}
        </Stack>
      </AdminPageSurface>
    </Stack>
  );
};
