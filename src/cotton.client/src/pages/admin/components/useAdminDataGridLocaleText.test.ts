import { enUS, ruRU } from "@mui/x-data-grid/locales";
import { describe, expect, it } from "vitest";
import { getDataGridLocaleText } from "@shared/hooks/useDataGridLocaleText";

describe("getDataGridLocaleText", () => {
  it("uses the matching grid locale and the page-specific empty text", () => {
    const localeText = getDataGridLocaleText("ru-RU", "Нет пользователей");

    expect(localeText.toolbarColumns).toBe(
      ruRU.components.MuiDataGrid.defaultProps.localeText.toolbarColumns,
    );
    expect(localeText.noRowsLabel).toBe("Нет пользователей");
  });

  it("falls back to English for an unsupported language", () => {
    const localeText = getDataGridLocaleText("unsupported", "Empty");

    expect(localeText.toolbarColumns).toBe(
      enUS.components.MuiDataGrid.defaultProps.localeText.toolbarColumns,
    );
    expect(localeText.noRowsLabel).toBe("Empty");
  });
});
