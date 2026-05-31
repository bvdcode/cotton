const FILES_ROOT_PATH = "/files";
const FILES_NODE_PATH_PREFIX = "/files/";

const isFilesNodePath = (path: string): boolean =>
  path.startsWith(FILES_NODE_PATH_PREFIX) &&
  path.length > FILES_NODE_PATH_PREFIX.length;

export const getSafeAuthReturnPath = (returnPath: string): string =>
  isFilesNodePath(returnPath) ? FILES_ROOT_PATH : returnPath;
