import {
  AxiosHeaders,
  type AxiosProgressEvent,
  type AxiosResponse,
} from "axios";

export const createHttpResponse = <T>(data: T): AxiosResponse<T> => ({
  data,
  status: 200,
  statusText: "OK",
  headers: new AxiosHeaders(),
  config: { headers: new AxiosHeaders() },
});

export const createUploadProgress = (
  loaded: number,
  total?: number,
): AxiosProgressEvent => ({
  loaded,
  total,
  bytes: loaded,
  lengthComputable: total !== undefined,
  upload: true,
});
