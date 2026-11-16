import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { UserRole } from "@features/auth/types";
import { ADMIN_SMART_SEARCH_ROUTE } from "@shared/config/adminRoutes";
import { useAuthStore } from "@shared/store/authStore";
import { showActionToast } from "@shared/ui/ActionToast";
import { toast } from "@shared/ui/notifications";

const NOTIFICATION_ID = "smart-search:configuration-required";

export function useSmartSearchSetupNotification(
  error: string | null,
  onConfigure?: () => void,
): boolean {
  const { t } = useTranslation("search");
  const navigate = useNavigate();
  const isAdmin = useAuthStore((state) => state.user?.role === UserRole.Admin);
  const configurationRequired = error === "smartSearch.notConfigured";

  useEffect(() => {
    if (!configurationRequired) {
      return;
    }

    if (isAdmin) {
      showActionToast({
        toastId: NOTIFICATION_ID,
        message: t("smartSearch.notConfigured"),
        action: t("smartSearch.configure"),
        type: "info",
        onAction: () => {
          onConfigure?.();
          navigate(ADMIN_SMART_SEARCH_ROUTE);
        },
      });
    } else {
      toast.info(t("smartSearch.contactAdministrator"), {
        toastId: NOTIFICATION_ID,
      });
    }
  }, [configurationRequired, isAdmin, navigate, onConfigure, t]);

  return configurationRequired;
}
