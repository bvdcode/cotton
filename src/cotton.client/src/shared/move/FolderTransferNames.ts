import { fetchAllRawNodeChildren } from "../api/nodeChildren";
import { readFolderName } from "../crypto/folderDisplayMeta";
import { readFileDisplayMeta } from "../crypto/displayMeta";
import { isFileEncrypted } from "../crypto/metadataFlags";
import { getFileNameKey } from "../utils/fileNameUtils";
import type { RestoreConflictKind } from "../api/nodesApi";
import { requireMasterKey } from "../crypto/vault";

export class DisplayNameConflictError extends Error {
  readonly kind: RestoreConflictKind;
  readonly canOverwrite: boolean;
  constructor(kind: RestoreConflictKind, canOverwrite = false) {
    super("An item with this display name already exists.");
    this.kind = kind;
    this.canOverwrite = canOverwrite;
  }
}

interface TransferName {
  id: string;
  kind: RestoreConflictKind;
  serverName: string;
  encrypted: boolean;
}

export class FolderTransferNames {
  private key: CryptoKey | null = null;
  private pending: Promise<Map<string, TransferName>> | null = null;
  private names: Map<string, TransferName> | null = null;

  private readonly parentId: string;

  constructor(parentId: string) {
    this.parentId = parentId;
  }

  private load() {
    const key = requireMasterKey();
    if (this.key && this.key !== key) {
      throw new Error("The encryption key changed during the transfer.");
    }
    this.key = key;
    this.pending ??= fetchAllRawNodeChildren(this.parentId).then(
      async ({ content }) => {
        const names = new Map<string, TransferName>();
        const [folders, files] = await Promise.all([
          Promise.all(content.nodes.map(readFolderName)),
          Promise.all(content.files.map(readFileDisplayMeta)),
        ]);
        for (const [index, node] of content.nodes.entries()) {
          names.set(getFileNameKey(folders[index]), {
            id: node.id,
            kind: "Folder",
            serverName: node.name,
            encrypted: false,
          });
        }
        for (const [index, file] of content.files.entries()) {
          names.set(getFileNameKey(files[index].name), {
            id: file.id,
            kind: "File",
            serverName: file.name,
            encrypted: isFileEncrypted(file.metadata),
          });
        }
        if (requireMasterKey() !== key) {
          throw new Error("The encryption key changed during the name lookup.");
        }
        this.names = names;
        return names;
      },
    );
    return this.pending;
  }

  async check(
    name: string,
    excludedId?: string,
    overwrite = false,
    sourceEncrypted = true,
  ): Promise<string | undefined> {
    const existing = (await this.load()).get(getFileNameKey(name));
    if (existing && existing.id !== excludedId) {
      const canOverwrite =
        existing.kind === "File" && existing.encrypted === sourceEncrypted;
      if (overwrite && canOverwrite) {
        return existing.serverName;
      }
      throw new DisplayNameConflictError(existing.kind, canOverwrite);
    }
  }

  async add(
    name: string,
    id: string,
    kind: RestoreConflictKind = "Folder",
    serverName = name,
  ): Promise<void> {
    (await this.load()).set(getFileNameKey(name), {
      id,
      kind,
      serverName,
      encrypted: false,
    });
  }

  record(
    name: string,
    id: string,
    kind: RestoreConflictKind,
    serverName: string,
    encrypted: boolean,
  ): void {
    this.names?.set(getFileNameKey(name), { id, kind, serverName, encrypted });
  }

  async takenNameKeys(): Promise<Set<string>> {
    return new Set((await this.load()).keys());
  }
}
