import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { NodeFileManifestDto } from "../../api/nodesApi";
import { ENCRYPTED_FLAG_KEY } from "../../crypto";
import { EditorMode } from "./editors/types";
import { TextPreview } from "./TextPreview";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock("./hooks/useTextFileContent", () => ({
  useTextFileContent: () => ({
    content: "# Secret note",
    setContent: vi.fn(),
    originalContent: "# Secret note",
    setOriginalContent: vi.fn(),
    loading: false,
    error: null,
    isFileTooLarge: false,
  }),
}));

vi.mock("./hooks/useTextFileSave", () => ({
  useTextFileSave: () => ({
    saving: false,
    error: null,
    handleSave: vi.fn(),
  }),
}));

vi.mock("./hooks/useEditorMode", () => ({
  useEditorMode: () => ({ mode: EditorMode.Markdown, setMode: vi.fn() }),
}));

vi.mock("./hooks/useLanguageSelection", () => ({
  useLanguageSelection: () => ({ language: "markdown", setLanguage: vi.fn() }),
}));

vi.mock("./factories/EditorFactory", () => ({
  EditorFactory: () => <div />,
}));

function createEncryptedNote(): NodeFileManifestDto {
  return {
    id: "file-1",
    createdAt: "2026-01-01T00:00:00.000Z",
    updatedAt: "2026-01-01T00:00:00.000Z",
    nodeId: "node-1",
    ownerId: "owner-1",
    name: "note.md",
    contentType: "text/markdown",
    sizeBytes: 12,
    metadata: { [ENCRYPTED_FLAG_KEY]: "true" },
  };
}

describe("TextPreview", () => {
  it("offers editing for a decrypted encrypted note", () => {
    const sourceFile = createEncryptedNote();

    render(
      <TextPreview
        nodeFileId={sourceFile.id}
        fileName={sourceFile.name}
        fileSizeBytes={sourceFile.sizeBytes}
        sourceFile={sourceFile}
      />,
    );

    expect(
      screen.getByRole("button", { name: "preview.actions.edit" }),
    ).toBeEnabled();
  });
});
