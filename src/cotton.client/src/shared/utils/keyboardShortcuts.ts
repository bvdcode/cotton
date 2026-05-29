interface KeyboardShortcutEvent {
  readonly code: string;
  readonly ctrlKey: boolean;
  readonly key: string;
  readonly metaKey: boolean;
  readonly repeat: boolean;
}

export const SYSTEM_KEYBOARD_SHORTCUTS = {
  cut: { code: "KeyX", key: "x" },
  paste: { code: "KeyV", key: "v" },
  search: { code: "KeyF", key: "f" },
} as const;

export type SystemKeyboardShortcut = keyof typeof SYSTEM_KEYBOARD_SHORTCUTS;

const hasSystemModifier = (event: KeyboardShortcutEvent): boolean =>
  event.ctrlKey || event.metaKey;

const matchesShortcutKey = (
  event: KeyboardShortcutEvent,
  shortcut: SystemKeyboardShortcut,
): boolean => {
  const definition = SYSTEM_KEYBOARD_SHORTCUTS[shortcut];

  return (
    event.code === definition.code ||
    event.key.toLowerCase() === definition.key
  );
};

export const isSystemKeyboardShortcut = (
  event: KeyboardShortcutEvent,
  shortcut: SystemKeyboardShortcut,
): boolean =>
  hasSystemModifier(event) &&
  !event.repeat &&
  matchesShortcutKey(event, shortcut);

export const getSystemKeyboardShortcut = <
  const TShortcut extends SystemKeyboardShortcut,
>(
  event: KeyboardShortcutEvent,
  shortcuts: ReadonlyArray<TShortcut>,
): TShortcut | null => {
  if (!hasSystemModifier(event) || event.repeat) return null;

  for (const shortcut of shortcuts) {
    if (matchesShortcutKey(event, shortcut)) return shortcut;
  }

  return null;
};
