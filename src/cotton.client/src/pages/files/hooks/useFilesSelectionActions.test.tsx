import { renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { useConfirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import { useFileSelection } from "@shared/hooks/useFileSelection";
import { createFolder } from "../../../test/fileFixtures";
import type { NodeDto } from "@shared/api/layoutsApi";
import { useFilesSelectionActions } from "./useFilesSelectionActions";

vi.mock("material-ui-confirm", () => ({ useConfirm: () => vi.fn() }));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

describe("useFilesSelectionActions", () => {
  it("keeps share in its slot while destination metadata is loading", () => {
    const node = createFolder({ id: "folder", name: "Photos" });
    const noop = vi.fn();
    const { result, rerender } = renderHook<
      ReturnType<typeof useFilesSelectionActions>,
      { activeCurrentNode: NodeDto | null; loading: boolean }
    >(
      ({
        activeCurrentNode,
        loading,
      }: {
        activeCurrentNode: NodeDto | null;
        loading: boolean;
      }) => {
        const fileSelection = useFileSelection();
        const confirm = useConfirm();
        const { t } = useTranslation();
        return useFilesSelectionActions({
          activeCurrentNode,
          loading,
          nodeId: node.id,
          clipboardCount: 0,
          currentFolderName: node.name,
          fileSelection,
          handleCutSelection: noop,
          handleCopySelection: noop,
          handlePasteHere: noop,
          optimisticDeleteFile: noop,
          reloadCurrentNode: noop,
          showToast: noop,
          tiles: [],
          confirm,
          t,
        });
      },
      { initialProps: { activeCurrentNode: node, loading: false } },
    );
    const initialKeys = result.current.customActionItems?.map(
      (action) => action.key,
    );
    expect(initialKeys).toContain("share-current-folder");
    rerender({ activeCurrentNode: null, loading: true });
    expect(
      result.current.customActionItems?.map((action) => action.key),
    ).toEqual(initialKeys);
    expect(result.current.customActionItems?.[0].disabled).toBe(true);
    rerender({ activeCurrentNode: node, loading: false });
    expect(result.current.customActionItems?.[0].disabled).toBe(false);
  });
});
