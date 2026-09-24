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

export const SILENCED_HUB_METHODS = [
  ...FILE_AND_NODE_MUTATION_HUB_METHODS,
  HUB_METHODS.PreviewGenerated,
] as const satisfies ReadonlyArray<HubMethod>;
