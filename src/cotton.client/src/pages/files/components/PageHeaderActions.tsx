import type { ReactElement } from "react";
import {
  ArrowUpward,
  CheckBox,
  CheckBoxOutlineBlank,
  CreateNewFolder,
  Deselect,
  NoteAdd,
  SelectAll,
  UploadFile,
} from "@mui/icons-material";
import type { useTranslation } from "react-i18next";
import type { PageHeaderProps, PageHeaderActionItem } from "./PageHeader";
import { formatBytes } from "../../../shared/utils/formatBytes";

type PageHeaderActionFactoryOptions = Pick<
  PageHeaderProps,
  | "canGoUp"
  | "customActionItems"
  | "primaryActionItems"
  | "goUpDropHandlers"
  | "isCreatingFile"
  | "isCreatingFolder"
  | "loading"
  | "onDeselectAll"
  | "onGoUp"
  | "onNewFileClick"
  | "onNewFolderClick"
  | "onSelectAll"
  | "onToggleSelectionMode"
  | "onUploadClick"
  | "onViewModeCycle"
  | "selectedCount"
  | "selectionMode"
  | "showNewFile"
  | "showNewFolder"
  | "showUpload"
  | "showViewModeToggle"
> & {
  nextViewTitleKey: string;
  t: ReturnType<typeof useTranslation>["t"];
  viewIcon: ReactElement;
};

export const buildPageHeaderActions = (
  options: PageHeaderActionFactoryOptions,
): PageHeaderActionItem[] => {
  const actions: PageHeaderActionItem[] = [
    createGoUpAction(
      options.canGoUp,
      options.loading,
      options.onGoUp,
      options.t,
      options.goUpDropHandlers,
    ),
  ];
  actions.push(...(options.primaryActionItems ?? []));

  appendCreationActions(actions, {
    isCreatingFile: options.isCreatingFile,
    isCreatingFolder: options.isCreatingFolder,
    loading: options.loading,
    onNewFileClick: options.onNewFileClick,
    onNewFolderClick: options.onNewFolderClick,
    onUploadClick: options.onUploadClick,
    showNewFile: options.showNewFile,
    showNewFolder: options.showNewFolder,
    showUpload: options.showUpload,
    t: options.t,
  });
  appendViewModeAction(
    actions,
    options.showViewModeToggle ?? true,
    options.onViewModeCycle,
    options.nextViewTitleKey,
    options.viewIcon,
    options.t,
  );
  appendSelectionActions(actions, {
    onDeselectAll: options.onDeselectAll,
    onSelectAll: options.onSelectAll,
    onToggleSelectionMode: options.onToggleSelectionMode,
    selectedCount: options.selectedCount,
    selectionMode: options.selectionMode,
    t: options.t,
  });

  return options.customActionItems?.length
    ? [...actions, ...options.customActionItems]
    : actions;
};

const createGoUpAction = (
  canGoUp: boolean,
  loading: boolean,
  onGoUp: () => void,
  t: ReturnType<typeof useTranslation>["t"],
  goUpDropHandlers: PageHeaderProps["goUpDropHandlers"],
): PageHeaderActionItem => ({
  key: "go-up",
  icon: <ArrowUpward />,
  title: t("actions.goUp"),
  onClick: onGoUp,
  disabled: loading || !canGoUp,
  onDragOver: goUpDropHandlers?.onDragOver,
  onDragLeave: goUpDropHandlers?.onDragLeave,
  onDrop: goUpDropHandlers?.onDrop,
  dropActive: goUpDropHandlers?.active,
});

type CreationActionOptions = Pick<
  PageHeaderActionFactoryOptions,
  | "isCreatingFile"
  | "isCreatingFolder"
  | "loading"
  | "onNewFileClick"
  | "onNewFolderClick"
  | "onUploadClick"
  | "showNewFile"
  | "showNewFolder"
  | "showUpload"
  | "t"
>;

const appendCreationActions = (
  actions: PageHeaderActionItem[],
  options: CreationActionOptions,
) => {
  if (options.showUpload && options.onUploadClick) {
    actions.push({
      key: "upload",
      icon: <UploadFile />,
      title: options.t("actions.upload"),
      onClick: options.onUploadClick,
      disabled: options.loading,
    });
  }

  if (options.showNewFile && options.onNewFileClick) {
    actions.push({
      key: "new-markdown-file",
      icon: <NoteAdd />,
      title: options.t("actions.newMarkdownFile"),
      onClick: options.onNewFileClick,
      disabled: options.loading || options.isCreatingFile,
    });
  }

  if (options.showNewFolder && options.onNewFolderClick) {
    actions.push({
      key: "new-folder",
      icon: <CreateNewFolder />,
      title: options.t("actions.newFolder"),
      onClick: options.onNewFolderClick,
      disabled: options.loading || options.isCreatingFolder,
    });
  }
};

const appendViewModeAction = (
  actions: PageHeaderActionItem[],
  showViewModeToggle: boolean,
  onViewModeCycle: () => void,
  nextViewTitleKey: string,
  viewIcon: ReactElement,
  t: ReturnType<typeof useTranslation>["t"],
) => {
  if (!showViewModeToggle) {
    return;
  }

  actions.push({
    key: "view-mode",
    icon: viewIcon,
    title: t(nextViewTitleKey),
    onClick: onViewModeCycle,
    disabled: false,
  });
};

type SelectionActionOptions = Pick<
  PageHeaderActionFactoryOptions,
  | "onDeselectAll"
  | "onSelectAll"
  | "onToggleSelectionMode"
  | "selectedCount"
  | "selectionMode"
  | "t"
>;

const appendSelectionActions = (
  actions: PageHeaderActionItem[],
  options: SelectionActionOptions,
) => {
  if (options.onToggleSelectionMode) {
    actions.push({
      key: "selection-mode",
      icon: options.selectionMode ? <CheckBox /> : <CheckBoxOutlineBlank />,
      title: options.t(
        options.selectionMode ? "selection.exit" : "selection.enter",
      ),
      onClick: options.onToggleSelectionMode,
      disabled: false,
      active: options.selectionMode,
    });
  }

  if (options.selectionMode && options.onSelectAll) {
    actions.push({
      key: "select-all",
      icon: <SelectAll />,
      title: options.t("selection.selectAll"),
      onClick: options.onSelectAll,
      disabled: false,
    });
  }

  if (
    options.selectionMode &&
    (options.selectedCount ?? 0) > 0 &&
    options.onDeselectAll
  ) {
    actions.push({
      key: "deselect-all",
      icon: <Deselect />,
      title: options.t("selection.deselectAll"),
      onClick: options.onDeselectAll,
      disabled: false,
    });
  }
};

export const buildStatsSummary = (
  stats: PageHeaderProps["stats"],
  statsNamespace: string,
  t: ReturnType<typeof useTranslation>["t"],
): string | null => {
  const parts: string[] = [];

  if (stats.folders > 0) {
    parts.push(
      t("stats.folders", { ns: statsNamespace, count: stats.folders }),
    );
  }

  if (stats.files > 0) {
    parts.push(t("stats.files", { ns: statsNamespace, count: stats.files }));
  }

  if (stats.sizeBytes > 0) {
    parts.push(formatBytes(stats.sizeBytes));
  }

  if (parts.length === 0) {
    return null;
  }

  return parts.join(t("stats.separator", { ns: statsNamespace }));
};
