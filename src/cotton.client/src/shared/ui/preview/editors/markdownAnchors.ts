import type { Root } from "hast";
import { defaultSchema } from "rehype-sanitize";
import { visit } from "unist-util-visit";

export const markdownSanitizationSchema = {
  ...defaultSchema,
  clobberPrefix: "user-content-",
};

export function restoreMarkdownAnchors() {
  return (tree: Root) => {
    const links = new Map<string, string>();
    const prefix = markdownSanitizationSchema.clobberPrefix;
    visit(tree, "element", (node) => {
      for (const target of [node.properties.id, node.properties.name]) {
        if (typeof target !== "string" || !target.startsWith(prefix)) {
          continue;
        }
        const original = target.slice(prefix.length);
        links.set(`#${original}`, `#${target}`);
        links.set(
          `#${encodeURIComponent(original)}`,
          `#${encodeURIComponent(target)}`,
        );
      }
    });
    visit(tree, "element", (node) => {
      if (node.tagName !== "a" || typeof node.properties.href !== "string") {
        return;
      }
      const target = links.get(node.properties.href);
      if (target) {
        node.properties.href = target;
      }
    });
  };
}
