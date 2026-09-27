import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";
import { defineConfig, globalIgnores } from "eslint/config";

export default defineConfig([
  globalIgnores(["dist"]),
  {
    files: ["**/*.{ts,tsx}"],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      "max-lines": [
        "error",
        { max: 400, skipBlankLines: true, skipComments: true },
      ],
      "@typescript-eslint/no-explicit-any": "error",
      "@typescript-eslint/no-unsafe-type-assertion": "error",
      "no-console": "error",
      "no-restricted-globals": [
        "error",
        { name: "localStorage", message: "Use application state instead." },
      ],
      "no-restricted-syntax": [
        "error",
        {
          selector: 'Identifier[name="console"]',
          message: "Use the client diagnostics module instead of console.",
        },
        {
          selector: 'MemberExpression[property.value="console"]',
          message: "Use the client diagnostics module instead of console.",
        },
        {
          selector: "TSUnknownKeyword",
          message: "Use a specific type and validate data at its boundary.",
        },
        {
          selector: 'MemberExpression[property.name="localStorage"]',
          message: "Use application state instead of localStorage.",
        },
        {
          selector: 'MemberExpression[property.value="localStorage"]',
          message: "Use application state instead of localStorage.",
        },
        {
          selector: "TSAsExpression > TSAsExpression",
          message: "Nested type assertions are not allowed.",
        },
        {
          selector:
            'TSTypeReference[typeName.name="Array"] > TSTypeParameterInstantiation > TSTypeLiteral',
          message:
            "Anonymous array item models are not allowed. Define a named model in a dedicated file.",
        },
        {
          selector: "TSArrayType > TSTypeLiteral",
          message:
            "Anonymous array item models are not allowed. Define a named model in a dedicated file.",
        },
      ],
    },
  },
  {
    files: ["src/shared/**/*.{ts,tsx}", "src/features/**/*.{ts,tsx}"],
    rules: {
      "no-restricted-imports": [
        "error",
        {
          patterns: [
            {
              group: ["@pages/*", "**/pages/*"],
              message:
                "shared/ and features/ must not import from pages/. Move reusable code to shared/ or pass it in from the page layer.",
            },
          ],
        },
      ],
    },
  },
]);
