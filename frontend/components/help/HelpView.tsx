"use client";

import React, { useEffect, useMemo, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { PageHeader } from "@/components/common/PageHeader";
import { buildToc, plainHeading, slugify } from "./markdown";
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
  // Màn hẹp: mục lục thu gọn để nội dung hiện ngay.
  const [tocOpen, setTocOpen] = useState(true);
  useEffect(() => {
    if (window.matchMedia("(max-width: 991px)").matches) setTocOpen(false);
  }, []);
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
      <div className={`page-body ${styles.layout}`}>
        <nav className={styles.toc} aria-label="Mục lục hướng dẫn">
          <details className={styles.tocDetails} open={tocOpen} onToggle={(e) => setTocOpen(e.currentTarget.open)}>
            <summary className={styles.tocTitle}>Mục lục</summary>
            <ul>
              {toc.map((t) => (
                <li key={t.id} className={t.level === 3 ? styles.sub : undefined}>
                  <a href={`#${t.id}`}>{t.text}</a>
                </li>
              ))}
            </ul>
          </details>
        </nav>
        <article className={styles.content}>
          <ReactMarkdown remarkPlugins={[remarkGfm]} components={components}>
            {markdown}
          </ReactMarkdown>
        </article>
      </div>
    </div>
  );
}

export default HelpView;
