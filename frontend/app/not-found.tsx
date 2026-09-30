import Link from "next/link";
import { EmptyState } from "@/components/common/EmptyState";

/** Trang 404 — đường dẫn không tồn tại hoặc chức năng đã bỏ. */
export default function NotFound() {
  return (
    <div className="page-wrapper">
      <div className="page-body">
        <section className="card">
          <EmptyState
            icon="bi-signpost-split"
            title="Không tìm thấy trang"
            action={<Link href="/" className="btn btn-primary">Về trang Tổng quan</Link>}
          />
        </section>
      </div>
    </div>
  );
}
