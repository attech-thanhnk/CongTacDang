import { redirect } from "next/navigation";

/**
 * Trang cũ "Cán bộ & Tổ chức" đã tách thành: Tài khoản (`/admin/users`), Vai trò (`/admin/roles`)
 * và Danh mục tổ chức (`/catalog`). Giữ đường dẫn cũ để không vỡ liên kết đã lưu.
 */
export default function LegacyUsersPage() {
  redirect("/admin/users");
}
