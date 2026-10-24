import { useEffect } from "react";
import type { RefObject } from "react";

export function findNameInitial(names: readonly string[], key: string): number {
  const normalized = key.normalize("NFC");
  if (!/^[\p{L}\p{N}]$/u.test(normalized)) {
    return -1;
  }
  const collator = new Intl.Collator(undefined, {
    sensitivity: "accent",
    usage: "search",
  });
  return names.findIndex((name) => {
    const first = Array.from(name.normalize("NFC"))[0];
    return first !== undefined && collator.compare(first, normalized) === 0;
  });
}

export function useNameNavigation(
  container: RefObject<HTMLElement | null>,
  getNames: () => readonly string[],
  focusIndex: (index: number) => void,
) {
  useEffect(() => {
    const navigate = (event: KeyboardEvent) => {
      if (
        event.defaultPrevented ||
        event.altKey ||
        event.ctrlKey ||
        event.metaKey ||
        event.isComposing
      ) {
        return;
      }
      const host = container.current;
      const active = document.activeElement;
      if (
        !host ||
        (active !== document.body &&
          active !== document.documentElement &&
          active !== null &&
          !host.contains(active))
      ) {
        return;
      }
      if (
        event.target instanceof Element &&
        event.target.closest(
          "input, textarea, select, [contenteditable]:not([contenteditable='false']), [role='menu'], [role='menuitem'], .card-menu-slot, .card-menu-button",
        )
      ) {
        return;
      }
      const index = findNameInitial(getNames(), event.key);
      if (index < 0) {
        return;
      }
      event.preventDefault();
      event.stopPropagation();
      focusIndex(index);
    };
    window.addEventListener("keydown", navigate, true);
    return () => window.removeEventListener("keydown", navigate, true);
  }, [container, focusIndex, getNames]);
}
