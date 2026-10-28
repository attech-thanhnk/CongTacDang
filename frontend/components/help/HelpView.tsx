"use client";

import React, { useMemo, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { PageHeader } from "@/components/common/PageHeader";
import { DocWithToc, DocTocEntry } from "@/components/common/DocWithToc";
import { buildSearchText, buildToc, foldText, plainHeading, slugify } from "./markdown";
import styles from "./help.module.css";

function textOf(node: React.ReactNode): string {
  if (typeof node === "string" || typeof node === "number") return String(node);
  if (Array.isArray(node)) return node.map(textOf).join("");
  if (React.isValidElement(node)) return textOf((node.props as { children?: React.ReactNode }).children);
  return "";
}

type HeadingProps = { node?: { position?: { start: { line: number } } }; children?: React.ReactNode };

/** Hướng dẫn sử dụng trong ứng dụng: mục lục bên cạnh và nội dung từ tài liệu hướng dẫn. */
export function HelpView({ markdown }: { markdown: string }) {
  const toc = useMemo(() => buildToc(markdown), [markdown]);
  const searchText = useMemo(() => buildSearchText(markdown, toc), [markdown, toc]);
  const [query, setQuery] = useState("");
  const terms = useMemo(() => foldText(query).split(/\s+/).filter(Boolean), [query]);
  const searching = terms.length > 0;

  // Mục khớp từ khóa (tiêu đề hoặc nội dung); giữ mục lớn chứa mục khớp để biết đang ở đâu.
  const { entries, matchCount } = useMemo(() => {
    const all: DocTocEntry[] = toc.map((t) => ({ id: t.id, label: t.text, level: t.level === 2 ? 1 : 2 }));
    if (!searching) return { entries: all, matchCount: 0 };
    const hit = (id: string) => terms.every((w) => (searchText.get(id) ?? "").includes(w));
    const keep = new Set<string>();
    let chapter: string | null = null;
    let count = 0;
    for (const e of all) {
      if (e.level === 1) chapter = e.id;
      if (hit(e.id)) {
        count++;
        keep.add(e.id);
        if (chapter) keep.add(chapter);
      }
    }
    return { entries: all.filter((e) => keep.has(e.id)), matchCount: count };
  }, [toc, searchText, terms, searching]);
  // Mã neo theo dòng của tiêu đề để tiêu đề trùng chữ vẫn khớp mục lục.
  const idByLine = useMemo(() => new Map(toc.map((t) => [t.line, t.id])), [toc]);

  const components = useMemo(() => {
    const heading = (Tag: "h2" | "h3" | "h4") => {
      const Heading = ({ node, children }: HeadingProps) => {
        const line = node?.position?.start.line;
        const id = (line !== undefined ? idByLine.get(line) : undefined) ?? slugify(plainHeading(textOf(children)));
        return <Tag id={id}>{children}</Tag>;
      };
      return Heading;
    };
    return {
      h1: () => null,
      h2: heading("h2"),
      h3: heading("h3"),
      h4: heading("h4"),
      table: ({ children }: { children?: React.ReactNode }) => (
        <div className="table-responsive">
          <table className="table table-sm table-bordered align-middle">{children}</table>
        </div>
      ),
      a: ({ href, children }: { href?: string; children?: React.ReactNode }) => {
        if (href?.startsWith("#")) return <a href={href}>{children}</a>;
        if (href && /^https?:\/\//.test(href)) return <a href={href} target="_blank" rel="noopener noreferrer">{children}</a>;
        // Liên kết sang tài liệu khác trong docs/ không có trong ứng dụng: chỉ giữ chữ.
        return <span>{children}</span>;
      },
    };
  }, [idByLine]);

  return (
    <div className="page-wrapper">
      <PageHeader title="Hướng dẫn sử dụng" />
      <div className="page-body">
        <DocWithToc
          entries={entries}
          ariaLabel="Mục lục hướng dẫn"
          collapsible
          expandAll={searching}
          header={
            <div className={styles.search}>
              <input
                type="text"
                role="searchbox"
                className="form-control form-control-sm"
                placeholder="Tìm trong hướng dẫn"
                aria-label="Tìm trong hướng dẫn"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Escape") setQuery("");
                }}
              />
              {searching && (
                <button type="button" className={styles.clear} aria-label="Xóa tìm kiếm" onClick={() => setQuery("")}>
                  <i className="bi bi-x-lg" aria-hidden="true" />
                </button>
              )}
              {searching && (
                <div className={styles.count} role="status">
                  {matchCount > 0 ? `${matchCount} kết quả` : "Không có kết quả"}
                </div>
              )}
            </div>
          }
        >
          <article className={styles.content}>
            <ReactMarkdown remarkPlugins={[remarkGfm]} components={components}>
              {markdown}
            </ReactMarkdown>
          </article>
        </DocWithToc>
      </div>
    </div>
  );
}

export default HelpView;
