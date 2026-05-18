import { describe, expect, it, vi } from "vitest";
import { InterfaceLayoutType } from "../api/layoutsApi";
import {
  cycleFileBrowserViewMode,
  getFileBrowserViewMode,
  getNextFileBrowserViewMode,
  getNextFileBrowserViewTitleKey,
  setFileBrowserViewMode,
} from "./viewMode";

describe("getFileBrowserViewMode", () => {
  it("returns table for list layout", () => {
    expect(getFileBrowserViewMode(InterfaceLayoutType.List, "small")).toBe(
      "table",
    );
    expect(getFileBrowserViewMode(InterfaceLayoutType.List, "large")).toBe(
      "table",
    );
  });

  it("maps tile sizes to tile view modes", () => {
    expect(getFileBrowserViewMode(InterfaceLayoutType.Tiles, "small")).toBe(
      "tiles-small",
    );
    expect(getFileBrowserViewMode(InterfaceLayoutType.Tiles, "medium")).toBe(
      "tiles-medium",
    );
    expect(getFileBrowserViewMode(InterfaceLayoutType.Tiles, "large")).toBe(
      "tiles-large",
    );
  });
});

describe("getNextFileBrowserViewMode", () => {
  it("cycles through all supported modes", () => {
    expect(getNextFileBrowserViewMode("table")).toBe("tiles-small");
    expect(getNextFileBrowserViewMode("tiles-small")).toBe("tiles-medium");
    expect(getNextFileBrowserViewMode("tiles-medium")).toBe("tiles-large");
    expect(getNextFileBrowserViewMode("tiles-large")).toBe("table");
  });
});

describe("getNextFileBrowserViewTitleKey", () => {
  it("returns the next mode translation key", () => {
    expect(getNextFileBrowserViewTitleKey("table")).toBe(
      "actions.switchToSmallTilesView",
    );
    expect(getNextFileBrowserViewTitleKey("tiles-small")).toBe(
      "actions.switchToMediumTilesView",
    );
    expect(getNextFileBrowserViewTitleKey("tiles-large")).toBe(
      "actions.switchToTableView",
    );
  });
});

describe("setFileBrowserViewMode", () => {
  it("sets list layout for table mode", () => {
    const setLayoutType = vi.fn();
    const setTilesSize = vi.fn();

    setFileBrowserViewMode("table", setLayoutType, setTilesSize);

    expect(setLayoutType).toHaveBeenCalledWith(InterfaceLayoutType.List);
    expect(setTilesSize).not.toHaveBeenCalled();
  });

  it("sets tiles layout and tile size for tile modes", () => {
    const setLayoutType = vi.fn();
    const setTilesSize = vi.fn();

    setFileBrowserViewMode("tiles-medium", setLayoutType, setTilesSize);

    expect(setLayoutType).toHaveBeenCalledWith(InterfaceLayoutType.Tiles);
    expect(setTilesSize).toHaveBeenCalledWith("medium");
  });
});

describe("cycleFileBrowserViewMode", () => {
  it("applies the next view mode", () => {
    const setLayoutType = vi.fn();
    const setTilesSize = vi.fn();

    cycleFileBrowserViewMode("table", setLayoutType, setTilesSize);

    expect(setLayoutType).toHaveBeenCalledWith(InterfaceLayoutType.Tiles);
    expect(setTilesSize).toHaveBeenCalledWith("small");
  });
});
