import { httpClient } from "./httpClient";
import type { Guid } from "./layoutsApi";

export interface CreateArchiveDownloadLinkRequest {
  fileIds: Guid[];
  nodeIds: Guid[];
  archiveName?: string;
}

export interface ArchiveDownloadLinkDto {
  url: string;
  fileName: string;
  sizeBytes: number;
  entryCount: number;
}

export const archiveApi = {
  createDownloadLink: async (
    request: CreateArchiveDownloadLinkRequest,
  ): Promise<ArchiveDownloadLinkDto> => {
    const response = await httpClient.post<ArchiveDownloadLinkDto>(
      "archives/download-link",
      request,
    );
    return response.data;
  },
};
