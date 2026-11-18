import { StrictMode } from "react";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { toast } from "../../notifications/toast";
import { containsMarkdownIframe } from "./containsMarkdownIframe";
import { MarkdownEditor } from "./MarkdownEditor";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock("../../notifications/toast", () => ({
  toast: { warning: vi.fn() },
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

function preview(value: string) {
  return render(
    <MarkdownEditor
      value={value}
      onChange={vi.fn()}
      isEditing={false}
      fileName="document.md"
    />,
  );
}

describe("Markdown preview security", () => {
  it.each([
    [
      "iframe",
      '<iframe srcdoc="&lt;script&gt;parent.alert(1)&lt;/script&gt;"></iframe>',
    ],
    ["iframe", '<IFRAME src="/" />'],
    ["script", '<script>document.body.dataset.executed = "yes"</script>'],
    [
      "form",
      '<form action="/submit"><input name="secret"><button>Submit</button></form>',
    ],
    ["object", '<object data="/payload.html"></object>'],
    ["embed", '<embed src="/payload.html">'],
    [
      "svg",
      '<svg><foreignObject><iframe srcdoc="test"></iframe></foreignObject></svg>',
    ],
    ["math", '<math><mtext><img src="x" onerror="alert(1)"></mtext></math>'],
    ["style", "<style>body { display: none }</style>"],
    ["link", '<link rel="stylesheet" href="/payload.css">'],
    ["meta", '<meta http-equiv="refresh" content="0;url=/other">'],
    ["base", '<base href="https://example.invalid/">'],
  ])("removes active %s content", (tag, value) => {
    const { container } = preview(value);

    expect(container.querySelector(tag)).toBeNull();
    expect(container.querySelector("[srcdoc]")).toBeNull();
  });

  it("removes event handlers, CSS and DOM-clobbering identifiers", () => {
    const { container } = preview(
      '<img src="/image.png" onerror="alert(1)" style="position:fixed" id="location" name="document">',
    );
    const image = screen.getByRole("img");

    expect(image).not.toHaveAttribute("onerror");
    expect(image).not.toHaveAttribute("style");
    expect(container.querySelector("#location, [name='document']")).toBeNull();
    expect(image).toHaveAttribute("src", "/image.png");
  });

  it.each([
    "[link](javascript:alert%281%29)",
    "[link](data:text/html,test)",
    '<a href="java&#x73;cript:alert(1)">link</a>',
    '<a href="jav&#x09;ascript:alert(1)">link</a>',
    '<a href="vbscript:msgbox(1)">link</a>',
    '<a href="data:text/html,test">link</a>',
  ])("removes dangerous link destinations: %s", (value) => {
    const { container } = preview(value);

    expect(container.querySelector("a")).not.toHaveAttribute("href");
  });

  it("preserves normal Markdown and safe HTML", () => {
    const { container } = preview(
      [
        "# Heading",
        "**Bold** and *italic* with [a link](https://example.org/path?q=1&x=2).",
        "![Picture](/picture.png)",
        "| First | Second |\n| --- | --- |\n| A | B |",
        "- [x] Complete\n- [ ] Pending",
        '```js\nconst sample = "<iframe>";\n```',
        "<details><summary>Details</summary>More information</details>",
      ].join("\n\n"),
    );

    expect(screen.getByRole("heading", { name: "Heading" })).toBeVisible();
    expect(container.querySelector("strong")).toHaveTextContent("Bold");
    expect(container.querySelector("em")).toHaveTextContent("italic");
    expect(screen.getByRole("link", { name: "a link" })).toHaveAttribute(
      "href",
      "https://example.org/path?q=1&x=2",
    );
    expect(screen.getByRole("img", { name: "Picture" })).toHaveAttribute(
      "src",
      "/picture.png",
    );
    expect(screen.getByRole("table")).toHaveTextContent("FirstSecondAB");
    expect(screen.getAllByRole("checkbox")).toHaveLength(2);
    expect(container.querySelector("pre code")).toHaveTextContent(
      'const sample = "<iframe>";',
    );
    expect(container.querySelector("summary")).toHaveTextContent("Details");
    expect(toast.warning).not.toHaveBeenCalled();
  });

  it("preserves encoded HTML as literal text", () => {
    const { container } = preview("&lt;img src=x onerror=alert(1)&gt; &amp;");

    expect(container.querySelector("img")).toBeNull();
    expect(container).toHaveTextContent("<img src=x onerror=alert(1)> &");
  });

  it("keeps heading and named-anchor links working with protected identifiers", () => {
    const { container } = preview(
      '# Heading\n\n[Section](#heading)\n\n<a name="old"></a>\n\n[Old](#old)\n\n## Раздел\n\n[Unicode](#%D1%80%D0%B0%D0%B7%D0%B4%D0%B5%D0%BB)',
    );

    expect(screen.getByRole("link", { name: "Section" })).toHaveAttribute(
      "href",
      "#user-content-heading",
    );
    expect(screen.getByRole("link", { name: "Old" })).toHaveAttribute(
      "href",
      "#user-content-old",
    );
    expect(screen.getByRole("link", { name: "Unicode" })).toHaveAttribute(
      "href",
      "#user-content-%D1%80%D0%B0%D0%B7%D0%B4%D0%B5%D0%BB",
    );
    expect(container.querySelector("#heading, [name='old']")).toBeNull();
  });

  it("warns once per opening, including StrictMode and source changes", () => {
    const onChange = vi.fn();
    const { rerender, unmount } = render(
      <StrictMode>
        <MarkdownEditor
          value={'<iframe src="/"></iframe>'}
          onChange={onChange}
          isEditing={false}
          fileName="document.md"
        />
      </StrictMode>,
    );
    rerender(
      <StrictMode>
        <MarkdownEditor
          value={'<iframe src="/other"></iframe>'}
          onChange={onChange}
          isEditing={false}
          fileName="document.md"
        />
      </StrictMode>,
    );

    expect(toast.warning).toHaveBeenCalledExactlyOnceWith(
      "preview.markdown.blockedIframe",
    );
    expect(onChange).not.toHaveBeenCalled();
    unmount();
    preview('<iframe src="/"></iframe>');
    expect(toast.warning).toHaveBeenCalledTimes(2);
  });

  it("retains the original source for editing and warns when preview opens", () => {
    const value = '<iframe srcdoc="example"></iframe>';
    const onChange = vi.fn();
    const { container, rerender } = render(
      <MarkdownEditor
        value={value}
        onChange={onChange}
        isEditing
        fileName="document.md"
      />,
    );

    expect(container.querySelector("textarea")).toHaveValue(value);
    expect(toast.warning).not.toHaveBeenCalled();
    rerender(
      <MarkdownEditor
        value={value}
        onChange={onChange}
        isEditing={false}
        fileName="document.md"
      />,
    );

    expect(container.querySelector("iframe")).toBeNull();
    expect(toast.warning).toHaveBeenCalledOnce();
    expect(onChange).not.toHaveBeenCalled();
  });
});

describe("Markdown iframe detection", () => {
  it.each([
    '<iframe src="/"></iframe>',
    '<IFRAME src="/" />',
    '<div><iframe srcdoc="test"></iframe></div>',
  ])("recognizes embedded frames: %s", (value) => {
    expect(containsMarkdownIframe(value)).toBe(true);
  });

  it.each([
    "Text mentioning iframe.",
    "`<iframe>`",
    '```html\n<iframe src="/"></iframe>\n```',
    '~~~html\n<iframe src="/"></iframe>\n~~~',
    '    <iframe src="/"></iframe>',
    '<!-- <iframe src="/"></iframe> -->',
    '&lt;iframe src="/"&gt;&lt;/iframe&gt;',
    '<span title="<iframe>">Example</span>',
  ])("ignores literal examples and comments: %s", (value) => {
    expect(containsMarkdownIframe(value)).toBe(false);
  });
});
