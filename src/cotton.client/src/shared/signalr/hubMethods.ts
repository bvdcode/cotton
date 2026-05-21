/**
 * Canonical names of SignalR hub methods sent by the server.
 *
 * Some older server builds emitted lowercased method names on the wire, so
 * consumers may subscribe to both canonical and lowercase variants.
 */
export const HUB_METHODS = {
  NotificationReceived: "OnNotificationReceived",
  SessionRevoked: "SessionRevoked",
  PreferencesUpdated: "PreferencesUpdated",
  PreviewGenerated: "PreviewGenerated",
  FileCreated: "FileCreated",
  FileUpdated: "FileUpdated",
  FileDeleted: "FileDeleted",
  FileMoved: "FileMoved",
  FileRenamed: "FileRenamed",
  FileRestored: "FileRestored",
  NodeCreated: "NodeCreated",
  NodeDeleted: "NodeDeleted",
  NodeMetadataUpdated: "NodeMetadataUpdated",
  NodeMoved: "NodeMoved",
  NodeRenamed: "NodeRenamed",
  NodeRestored: "NodeRestored",
} as const;

export type HubMethod = (typeof HUB_METHODS)[keyof typeof HUB_METHODS];
export type HubMethodOrLower = HubMethod | Lowercase<HubMethod>;

export const getHubMethodVariants = (
  methods: ReadonlyArray<HubMethod>,
): ReadonlyArray<HubMethodOrLower> =>
  methods.flatMap(
    (method) => [method, method.toLowerCase() as Lowercase<HubMethod>] as const,
  );

export const FILE_AND_NODE_MUTATION_HUB_METHODS = [
  HUB_METHODS.FileCreated,
  HUB_METHODS.FileUpdated,
  HUB_METHODS.FileDeleted,
  HUB_METHODS.FileMoved,
  HUB_METHODS.FileRenamed,
  HUB_METHODS.FileRestored,
  HUB_METHODS.NodeCreated,
  HUB_METHODS.NodeDeleted,
  HUB_METHODS.NodeMetadataUpdated,
  HUB_METHODS.NodeMoved,
  HUB_METHODS.NodeRenamed,
  HUB_METHODS.NodeRestored,
] as const satisfies ReadonlyArray<HubMethod>;

export const SILENCED_HUB_METHODS = getHubMethodVariants([
  ...FILE_AND_NODE_MUTATION_HUB_METHODS,
  HUB_METHODS.PreviewGenerated,
]);
