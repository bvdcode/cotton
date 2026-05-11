import type { NodeDto } from "../../shared/api/layoutsApi";
import type { NodeFileManifestDto } from "../../shared/api/nodesApi";

export type SearchDictionaryEntry = {
  id: string;
  title: string;
  description?: string;
  path: string;
  keywords: string[];
  highlightSettingId?: string;
  adminOnly?: boolean;
};

export type SearchRow =
  | {
      id: string;
      kind: "setting";
      entry: SearchDictionaryEntry;
    }
  | {
      id: string;
      kind: "folder";
      node: NodeDto;
      path?: string;
    }
  | {
      id: string;
      kind: "file";
      file: NodeFileManifestDto;
      path?: string;
    };

export type SearchSettingRow = Extract<SearchRow, { kind: "setting" }>;
