import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { VectorExtensionStatusDto } from "@shared/api/adminApi";
import { PgvectorIndexSetup } from "./PgvectorIndexSetup";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
    i18n: { language: "en" },
  }),
}));

const status: VectorExtensionStatusDto = {
  extensionEnabled: true,
  extensionAvailable: true,
  postgresMajorVersion: 18,
  databaseName: "cotton_test",
  vectorCount: 10,
  indexReady: false,
  indexBuilding: false,
  indexSizeBytes: 0,
  indexErrorCode: null,
  indexCreateSql: "CREATE INDEX search_index ON file_embeddings (id)",
};

const renderIndex = (overrides: Partial<VectorExtensionStatusDto> = {}) => {
  const onPrepare = vi.fn();
  render(
    <PgvectorIndexSetup
      status={{ ...status, ...overrides }}
      pending={false}
      disabled={false}
      error={null}
      onPrepare={onPrepare}
    />,
  );
  return onPrepare;
};

describe("PgvectorIndexSetup", () => {
  it("offers preparation when the extension is enabled but the index is absent", () => {
    const onPrepare = renderIndex();
    expect(screen.getByText("smartSearch.enabled")).toBeInTheDocument();
    expect(screen.getByText("smartSearch.index.missing")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "smartSearch.actions.enable" }));
    expect(onPrepare).toHaveBeenCalledOnce();
  });

  it("prevents duplicate requests while the index is building", () => {
    renderIndex({ indexBuilding: true });
    expect(screen.getByText("smartSearch.index.building")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "smartSearch.actions.enable" })).toBeDisabled();
    expect(screen.queryByText("smartSearch.index.ready")).not.toBeInTheDocument();
  });

  it("shows the actual index size and removes preparation when ready", () => {
    renderIndex({ indexReady: true, indexSizeBytes: 1_048_576 });
    expect(screen.getByText("smartSearch.index.ready")).toBeInTheDocument();
    expect(screen.getByText("1.00 MB")).toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("offers the exact server command and retry when index privileges are missing", () => {
    renderIndex({ indexErrorCode: "pgvector_index_permission_denied" });
    expect(screen.getByRole("alert")).toHaveTextContent("smartSearch.index.permissionDenied");
    expect(screen.getByText(status.indexCreateSql)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "smartSearch.actions.enable" })).toBeEnabled();
  });
});
