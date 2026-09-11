import type { NodeDto } from "../../../shared/api/layoutsApi";

export interface TrashWrapperFile {
  id: string;
  nodeId?: string | null;
}

export interface TrashWrapperContent {
  nodes?: ReadonlyArray<Pick<NodeDto, "id" | "parentId">>;
  files?: ReadonlyArray<TrashWrapperFile>;
}
