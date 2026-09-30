import Link from "next/link";
import { EmptyState } from "@/components/common/EmptyState";

/** Trang 404 — đường dẫn không tồn tại hoặc chức năng đã bỏ. */
export default function NotFound() {
  return (
    <div className="page-wrapper">
      <div className="page-body">
        <EmptyState
          icon="bi-signpost-split"
          title="Không tìm thấy trang"
          description="Đường dẫn không tồn tại hoặc chức năng đã được chuyển sang trang khác."
          action={<Link href="/" className="btn btn-primary btn-sm">Về trang Tổng quan</Link>}
        />
      </div>
    </div>
  );
}
