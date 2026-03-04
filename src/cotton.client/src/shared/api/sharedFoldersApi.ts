import { httpClient } from "./httpClient";
import type { Guid, NodeDto } from "./layoutsApi";

export interface SharedNodeFileDto {
  id: Guid;
  createdAt: string;
  updatedAt: string;
  nodeId: Guid;
  name: string;
  contentType: string;
  sizeBytes: number;
  previewHashEncryptedHex?: string | null;
}

export interface SharedNodeContentDto {
  id: Guid;
  createdAt: string;
  updatedAt: string;
  totalCount: number;
  nodes: NodeDto[];
  files: SharedNodeFileDto[];
}

export interface SharedNodeInfoDto {
  token: string;
  nodeId: Guid;
  name: string;
  expiresAt?: string | null;
}

export interface SharedNodeChildrenResult {
  content: SharedNodeContentDto;
  totalCount: number;
}

const encodeToken = (token: string): string => encodeURIComponent(token);

export const sharedFoldersApi = {
  getInfo: async (token: string): Promise<SharedNodeInfoDto> => {
    const response = await httpClient.get<SharedNodeInfoDto>(
      `/layouts/shared/${encodeToken(token)}`,
    );
    return response.data;
  },

  getChildren: async (
    token: string,
    options?: { nodeId?: Guid | null; page?: number; pageSize?: number },
  ): Promise<SharedNodeChildrenResult> => {
    const requestedPage = options?.page ?? 1;
    const requestedPageSize = options?.pageSize ?? 100;

    const response = await httpClient.get<SharedNodeContentDto>(
      `/layouts/shared/${encodeToken(token)}/children`,
      {
        params: {
          nodeId: options?.nodeId ?? undefined,
          page: requestedPage,
          pageSize: requestedPageSize,
        },
      },
    );

    const headersAny = response.headers as unknown as {
      [key: string]: unknown;
      get?: (name: string) => unknown;
    };

    const headerValue =
      headersAny?.["x-total-count"] ??
      headersAny?.["X-Total-Count"] ??
      headersAny?.get?.("x-total-count") ??
      headersAny?.get?.("X-Total-Count");

    const totalCount = Number.parseInt(String(headerValue ?? ""), 10);

    if (!Number.isFinite(totalCount)) {
      throw new Error("x-total-count header is missing or invalid");
    }

    return { content: response.data, totalCount };
  },

  getAncestors: async (token: string, nodeId: Guid): Promise<NodeDto[]> => {
    const response = await httpClient.get<NodeDto[]>(
      `/layouts/shared/${encodeToken(token)}/ancestors/${nodeId}`,
    );
    return response.data;
  },

  buildFileContentUrl: (
    token: string,
    nodeFileId: Guid,
    mode: "download" | "inline" = "inline",
  ): string => {
    const encodedToken = encodeToken(token);
    const encodedFileId = encodeURIComponent(nodeFileId);
    const download = mode === "download" ? "true" : "false";
    return `/api/v1/layouts/shared/${encodedToken}/files/${encodedFileId}/content?download=${download}`;
  },

  openFileInline: async (
    token: string,
    nodeFileId: Guid,
  ): Promise<void> => {
    const inlineUrl = sharedFoldersApi.buildFileContentUrl(token, nodeFileId, "inline");
    const response = await fetch(inlineUrl);
    if (!response.ok) {
      throw new Error("Failed to open shared file");
    }

    const blob = await response.blob();
    const objectUrl = URL.createObjectURL(blob);
    const opened = window.open(objectUrl, "_blank", "noopener,noreferrer");
    if (!opened) {
      URL.revokeObjectURL(objectUrl);
      throw new Error("Failed to open new window");
    }

    setTimeout(() => {
      URL.revokeObjectURL(objectUrl);
    }, 60_000);
  },

  downloadFile: async (
    token: string,
    nodeFileId: Guid,
    fileName: string,
  ): Promise<void> => {
    const downloadUrl = sharedFoldersApi.buildFileContentUrl(
      token,
      nodeFileId,
      "download",
    );

    const response = await fetch(downloadUrl);
    if (!response.ok) {
      throw new Error("Failed to download shared file");
    }

    const blob = await response.blob();
    const objectUrl = URL.createObjectURL(blob);

    const link = document.createElement("a");
    link.href = objectUrl;
    link.download = fileName;
    link.target = "_blank";
    link.rel = "noopener noreferrer";
    link.style.display = "none";

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    setTimeout(() => {
      URL.revokeObjectURL(objectUrl);
    }, 0);
  },
};
