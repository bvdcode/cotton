import { fromHtml } from "hast-util-from-html";
import { fromMarkdown } from "mdast-util-from-markdown";
import { EXIT, visit } from "unist-util-visit";

export function containsMarkdownIframe(value: string): boolean {
  if (!value.toLowerCase().includes("iframe")) {
    return false;
  }

  let found = false;
  visit(fromMarkdown(value), "html", (node) => {
    visit(fromHtml(node.value, { fragment: true }), "element", (element) => {
      if (element.tagName === "iframe") {
        found = true;
        return EXIT;
      }
    });
    if (found) {
      return EXIT;
    }
  });
  return found;
}
