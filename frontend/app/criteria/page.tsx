"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { CriteriaSetListItem, criteriaService } from "@/services/criteriaService";

const STATUS_BADGE: Record<string, string> = {
  Draft: "text-bg-warning",
  Published: "text-bg-success",
  Archived: "text-bg-secondary",
};

/**
 * Bộ tiêu chí và thang điểm theo phiên bản: danh sách, tạo bản nháp, nhân bản, lưu trữ, xóa bản nháp.
 * Xem: criteria.manage hoặc period.manage (chọn bộ cho kỳ); ghi: criteria.manage.
 */
export default function CriteriaSetsPage() {
  const { hasPermission } = useAuth();
  const canManage = hasPermission("criteria.manage");
  const canView = canManage || hasPermission("period.manage");
  const { toast, confirm } = useToast();
  const router = useRouter();
  const [sets, setSets] = useState<CriteriaSetListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({ code: "", name: "", selfScoreForm: "09A", notes: "" });

  const load = useCallback(async () => {
    if (!canView) return;
    try {
      setSets(await criteriaService.list());
    } catch (err: any) {
      toast.error(err?.message || "Không tải được danh sách bộ tiêu chí.");
    } finally {
      setLoading(false);
    }
  }, [canView, toast]);

  useEffect(() => {
    load();
  }, [load]);

  const run = async (work: () => Promise<unknown>, success: string) => {
    setBusy(true);
    try {
      await work();
      toast.success(success);
      await load();
    } catch (err: any) {
      toast.error(err?.message || "Không thực hiện được thao tác.");
      if (err?.status === 409) await load();
    } finally {
      setBusy(false);
    }
  };

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      const created = await criteriaService.create({ ...form, code: form.code.trim(), name: form.name.trim() });
      toast.success("Đã tạo bản nháp từ nội dung mặc định theo bản trích xuất HD03. Hãy sửa rồi xuất bản.");
      router.push(`/criteria/${created.id}`);
    } catch (err: any) {
      toast.error(err?.message || "Không tạo được bộ tiêu chí.");
    } finally {
      setBusy(false);
    }
  };

  const clone = (set: CriteriaSetListItem) =>
    run(async () => {
      const created = await criteriaService.clone(set.id);
      router.push(`/criteria/${created.id}`);
    }, "Đã nhân bản thành bản nháp mới.");

  if (!canView) {
    return <div className="page-wrapper"><div className="alert alert-warning m-4">Bạn cần quyền "Quản lý bộ tiêu chí" hoặc "Quản lý kỳ đánh giá" để xem trang này.</div></div>;
  }

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Bộ tiêu chí và thang điểm"
        subTitle="Tiêu chí chung, trục kết quả, khung tỷ trọng A-B-C-D, mức xếp loại và tham số theo phiên bản. Bộ đã xuất bản không sửa được — nhân bản thành bản nháp mới."
        actions={<Link href="/periods" className="btn btn-outline-secondary btn-sm"><i className="bi bi-calendar3 me-1" />Kỳ đánh giá</Link>}
      />
      <div className="page-body">
        <div className="row g-3">
          <div className={canManage ? "col-12 col-xl-8" : "col-12"}>
            <section className="card border-0 shadow-sm">
              <div className="card-body p-0">
                {loading ? (
                  <div className="text-secondary p-3"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
                ) : sets.length === 0 ? (
                  <EmptyState title="Chưa có bộ tiêu chí" />
                ) : (
                  <div className="table-responsive">
                    <table className="table table-hover table-sm align-middle mb-0">
                      <thead>
                        <tr className="small text-secondary">
                          <th className="ps-3">Bộ tiêu chí</th><th>Trạng thái</th><th>Mẫu</th><th>Nội dung</th><th>Kỳ đang dùng</th><th />
                        </tr>
                      </thead>
                      <tbody>
                        {sets.map((s) => (
                          <tr key={s.id} className="small">
                            <td className="ps-3">
                              <Link href={`/criteria/${s.id}`} className="fw-semibold">{s.name}</Link>
                              <div className="text-secondary">{s.code}</div>
                            </td>
                            <td><span className={`badge ${STATUS_BADGE[s.status] ?? "text-bg-light"}`}>{s.statusDisplayName}</span></td>
                            <td>{s.selfScoreForm}</td>
                            <td>{s.generalItemCount} tiêu chí con · {s.axisCount} trục · {s.weightFrameCount} khung</td>
                            <td>{s.usedByPeriods.length > 0 ? s.usedByPeriods.join(", ") : "—"}</td>
                            <td className="text-end pe-3 text-nowrap">
                              <Link href={`/criteria/${s.id}`} className="btn btn-link btn-sm p-0 me-2">{canManage && s.status === "Draft" ? "Sửa" : "Xem"}</Link>
                              {canManage && <button type="button" className="btn btn-link btn-sm p-0 me-2" disabled={busy} onClick={() => clone(s)}>Nhân bản</button>}
                              {canManage && s.status === "Published" && (
                                <button type="button" className="btn btn-link btn-sm p-0 me-2 text-warning" disabled={busy}
                                  onClick={() => confirm({ title: "Lưu trữ bộ tiêu chí", message: `Kỳ đã dùng "${s.name}" giữ nguyên ảnh chụp; bộ không chọn được cho kỳ mới.`, confirmText: "Lưu trữ", onConfirm: () => run(() => criteriaService.archive(s.id, s.version), "Đã lưu trữ bộ tiêu chí.") })}>
                                  Lưu trữ
                                </button>
                              )}
                              {canManage && s.status === "Draft" && (
                                <button type="button" className="btn btn-link btn-sm p-0 text-danger" disabled={busy}
                                  onClick={() => confirm({ title: "Xóa bản nháp", message: `Xóa hẳn bản nháp "${s.name}"?`, confirmText: "Xóa", isDanger: true, onConfirm: () => run(() => criteriaService.remove(s.id, s.version), "Đã xóa bản nháp.") })}>
                                  Xóa
                                </button>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </section>
          </div>

          {canManage && (
            <div className="col-12 col-xl-4">
              <section className="card border-0 shadow-sm">
                <div className="card-body">
                  <h2 className="h6">Tạo bộ tiêu chí (bản nháp)</h2>
                  <form onSubmit={create} className="row g-2">
                    <div className="col-12"><label className="form-label small" htmlFor="cs-code">Mã bộ</label>
                      <input id="cs-code" className="form-control form-control-sm" required maxLength={50} placeholder="VD: HD03-09A-2027-V2" value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} />
                    </div>
                    <div className="col-12"><label className="form-label small" htmlFor="cs-name">Tên bộ</label>
                      <input id="cs-name" className="form-control form-control-sm" required maxLength={200} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
                    </div>
                    <div className="col-12"><label className="form-label small" htmlFor="cs-form">Mẫu tự chấm</label>
                      <select id="cs-form" className="form-select form-select-sm" value={form.selfScoreForm} onChange={(e) => setForm({ ...form, selfScoreForm: e.target.value })}>
                        <option value="09A">09A — chấm theo nhiệm vụ (Mẫu 01/02, A-B-C-D)</option>
                        <option value="09B">09B — chấm trực tiếp theo trục</option>
                      </select>
                    </div>
                    <div className="col-12"><label className="form-label small" htmlFor="cs-notes">Ghi chú (căn cứ văn bản)</label>
                      <textarea id="cs-notes" className="form-control form-control-sm" rows={2} maxLength={4000} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
                    </div>
                    <div className="col-12 small text-secondary">Nội dung khởi tạo theo bản trích xuất HD03 (chờ nghiệp vụ xác nhận) — sửa trong bản nháp.</div>
                    <div className="col-12"><button type="submit" className="btn btn-primary btn-sm" disabled={busy}>Tạo bản nháp</button></div>
                  </form>
                </div>
              </section>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
