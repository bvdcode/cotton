import * as React from "react";

export type LightingPreset = "balanced" | "studio" | "dramatic";
export type SurfacePreset = "original" | "metal" | "smooth";

type ModelPreviewControlsState = {
  key: string;
  paletteAnchorEl: HTMLElement | null;
  materialColor: string | null;
  autoAlignToken: number;
  autoOrientToken: number;
  flipToken: number;
  lightingPreset: LightingPreset;
  surfacePreset: SurfacePreset;
  shadowsEnabled: boolean;
};

type ValueOrUpdater<T> = T | ((current: T) => T);

const LIGHTING_PRESET_ORDER: ReadonlyArray<LightingPreset> = [
  "balanced",
  "studio",
  "dramatic",
];

const SURFACE_PRESET_ORDER: ReadonlyArray<SurfacePreset> = [
  "original",
  "metal",
  "smooth",
];

const createModelPreviewControlsState = (
  key: string,
  defaultMaterialColor: string | null,
): ModelPreviewControlsState => ({
  key,
  paletteAnchorEl: null,
  materialColor: defaultMaterialColor,
  autoAlignToken: 0,
  autoOrientToken: 0,
  flipToken: 0,
  lightingPreset: "dramatic",
  surfacePreset: "metal",
  shadowsEnabled: true,
});

const resolveValue = <T>(current: T, next: ValueOrUpdater<T>): T => {
  return typeof next === "function" ? (next as (value: T) => T)(current) : next;
};

const nextInOrder = <T>(order: ReadonlyArray<T>, value: T): T => {
  const index = order.indexOf(value);
  return order[(index + 1) % order.length];
};

interface UseModelPreviewControlsArgs {
  stateKey: string;
  defaultMaterialColor: string | null;
}

export const useModelPreviewControls = ({
  stateKey,
  defaultMaterialColor,
}: UseModelPreviewControlsArgs) => {
  const [state, setState] = React.useState<ModelPreviewControlsState>(() =>
    createModelPreviewControlsState(stateKey, defaultMaterialColor),
  );
  const controls =
    state.key === stateKey
      ? state
      : createModelPreviewControlsState(stateKey, defaultMaterialColor);

  const update = React.useCallback(
    (
      updater: (
        current: ModelPreviewControlsState,
      ) => ModelPreviewControlsState,
    ) => {
      setState((previous) => {
        const current =
          previous.key === stateKey
            ? previous
            : createModelPreviewControlsState(stateKey, defaultMaterialColor);
        const next = updater(current);
        if (next === current) {
          return previous.key === stateKey ? previous : current;
        }

        return next;
      });
    },
    [defaultMaterialColor, stateKey],
  );

  const setPaletteAnchorEl = React.useCallback(
    (next: ValueOrUpdater<HTMLElement | null>) => {
      update((current) => {
        const nextValue = resolveValue(current.paletteAnchorEl, next);
        return nextValue === current.paletteAnchorEl
          ? current
          : { ...current, paletteAnchorEl: nextValue };
      });
    },
    [update],
  );

  const closePalette = React.useCallback(() => {
    setPaletteAnchorEl(null);
  }, [setPaletteAnchorEl]);

  const togglePaletteAnchor = React.useCallback(
    (anchorEl: HTMLElement) => {
      setPaletteAnchorEl((current) => (current ? null : anchorEl));
    },
    [setPaletteAnchorEl],
  );

  const setMaterialColor = React.useCallback(
    (next: string | null) => {
      update((current) =>
        next === current.materialColor
          ? current
          : { ...current, materialColor: next },
      );
    },
    [update],
  );

  const cycleLightingPreset = React.useCallback(() => {
    update((current) => ({
      ...current,
      lightingPreset: nextInOrder(
        LIGHTING_PRESET_ORDER,
        current.lightingPreset,
      ),
    }));
  }, [update]);

  const cycleSurfacePreset = React.useCallback(() => {
    update((current) => ({
      ...current,
      surfacePreset: nextInOrder(SURFACE_PRESET_ORDER, current.surfacePreset),
    }));
  }, [update]);

  const toggleShadowsEnabled = React.useCallback(() => {
    update((current) => ({
      ...current,
      shadowsEnabled: !current.shadowsEnabled,
    }));
  }, [update]);

  const requestAutoAlign = React.useCallback(() => {
    update((current) => ({
      ...current,
      autoAlignToken: current.autoAlignToken + 1,
    }));
  }, [update]);

  const requestAutoOrient = React.useCallback(() => {
    update((current) => ({
      ...current,
      autoOrientToken: current.autoOrientToken + 1,
    }));
  }, [update]);

  const requestFlip = React.useCallback(() => {
    update((current) => ({
      ...current,
      flipToken: current.flipToken + 1,
    }));
  }, [update]);

  return {
    ...controls,
    closePalette,
    cycleLightingPreset,
    cycleSurfacePreset,
    requestAutoAlign,
    requestAutoOrient,
    requestFlip,
    setMaterialColor,
    togglePaletteAnchor,
    toggleShadowsEnabled,
  };
};
