"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { Pagination } from "@/components/common/Pagination";
import { usePagination } from "@/components/common/usePagination";
import { Badge } from "@/components/common/StatusBadge";
import { NoAccess } from "@/components/admin/NoAccess";
import { errorMessage } from "@/components/admin/adminUtils";
import { PickerRow, RecordPickerDialog } from "@/components/quality/RecordPickerDialog";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { useToast } from "@/contexts/ToastContext";
import { formatDate } from "@/lib/dateFormat";
import { gradeLabel } from "@/services/evaluationService";
import {
  CALIBRATION_STATUS_LABELS,
  CALIBRATION_STATUS_TONES,
  CalibrationSessionListItemDto,
  CalibrationStatus,
  calibrationService,
} from "@/services/calibrationService";
import {
  CONTROL_STATUS_LABELS,
  CONTROL_STATUS_TONES,
  ControlCheckStatus,
  ControlCheckSummaryDto,
  controlCheckService,
} from "@/services/controlCheckService";

type Tab = "calibration" | "control";

const CAL_STATUSES = Object.keys(CALIBRATION_STATUS_LABELS) as CalibrationStatus[];
const CTL_STATUSES = Object.keys(CONTROL_STATUS_LABELS) as ControlCheckStatus[];

/** Duyệt kết quả đánh giá (họp thống nhất và kiểm tra): biên bản họp thống nhất và phiếu kiểm tra, theo kỳ đang chọn. */
export default function QualityControlPage() {
  const { hasPermission, loading: authLoading } = useAuth();
  const { periods, periodId } = usePeriod();
  const { toast } = useToast();
  const router = useRouter();

  const canCal = hasPermission("evaluation.calibration.manage") || hasPermission("evaluation.calibration.conclude");
  const canCtl = hasPermission("evaluation.control.perform") || hasPermission("evaluation.control.confirm");
  const canCreateCal = hasPermission("evaluation.calibration.manage");
  const canCreateCtl = hasPermission("evaluation.control.perform");

  const [tab, setTab] = useState<Tab | null>(null);
  const [period, setPeriod] = useState<string | null>(null);
  const [status, setStatus] = useState("");
  const [calItems, setCalItems] = useState<CalibrationSessionListItemDto[]>([]);
  const [ctlItems, setCtlItems] = useState<ControlCheckSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [pickerRows, setPickerRows] = useState<PickerRow[]>([]);
  const [pickerLoading, setPickerLoading] = useState(false);
  const [creating, setCreating] = useState(false);
  const seq = useRef(0);

  // Tab ban đầu theo đường dẫn quay về (?tab=control) hoặc quyền đang có.
  useEffect(() => {
    if (authLoading) return;
    let wanted: Tab | null = null;
    try {
      const q = new URLSearchParams(window.location.search).get("tab");
      if (q === "control" || q === "calibration") wanted = q;
    } catch {
      // Bỏ qua: dùng tab mặc định.
    }
    setTab((cur) => cur ?? (wanted === "control" && canCtl ? "control" : wanted === "calibration" && canCal ? "calibration" : canCal ? "calibration" : "control"));
  }, [authLoading, canCal, canCtl]);

  const periodFilter = period ?? periodId ?? "";
  const active: Tab = tab ?? "calibration";
  const allowed = active === "calibration" ? canCal : canCtl;

  const selectTab = (next: Tab) => {
    setTab(next);
    setStatus("");
    try {
      window.history.replaceState(null, "", `/quality-control?tab=${next}`);
    } catch {
      // Không đổi được địa chỉ: tab vẫn đổi trong trang.
    }
  };

  const load = useCallback(async () => {
    if (tab === null || !allowed) {
      setLoading(false);
      return;
    }
    const mine = ++seq.current;
    setLoading(true);
    try {
      if (tab === "calibration") {
        const rows = await calibrationService.list(periodFilter || undefined);
        if (seq.current === mine) setCalItems(rows);
      } else {
        const rows = await controlCheckService.list(periodFilter || undefined);
        if (seq.current === mine) setCtlItems(rows);
      }
    } catch (err: unknown) {
      if (seq.current === mine) toast.error(errorMessage(err, tab === "calibration" ? "Tải danh sách biên bản họp thống nhất không thành công." : "Tải danh sách phiếu kiểm tra không thành công."));
    } finally {
      if (seq.current === mine) setLoading(false);
    }
  }, [tab, allowed, periodFilter, toast]);

  useEffect(() => {
    load();
  }, [load]);

  const calShown = calItems.filter((c) => !status || c.status === status);
  const ctlShown = ctlItems.filter((c) => !status || c.status === status);
  const calPaging = usePagination(calShown, { storageKey: "quality-cal", resetKeys: [periodFilter, status] });
  const ctlPaging = usePagination(ctlShown, { storageKey: "quality-ctl", resetKeys: [periodFilter, status] });

  const openPicker = async () => {
    if (!periodFilter) {
      toast.error("Hãy chọn một kỳ đánh giá trước khi lập.");
      return;
    }
    const selected = periods.find((p) => p.id === periodFilter);
    if (selected && selected.status !== "Open") {
      toast.error("Chỉ lập khi kỳ đánh giá đang mở. Hãy chọn kỳ đang mở.");
      return;
    }
    setPickerOpen(true);
    setPickerLoading(true);
    setPickerRows([]);
    try {
      if (active === "calibration") {
        const rows = await calibrationService.candidates(periodFilter);
        setPickerRows(
          rows.map((r) => ({
            id: r.recordId,
            name: r.fullName,
            detail: [r.departmentName, r.partyCellName, `Trước họp thống nhất: ${gradeLabel(r.gradeBefore)}${r.scoreBefore != null ? ` (${r.scoreBefore})` : ""}`].filter(Boolean).join(" · "),
            group: r.workflowProfileName,
            unit: r.departmentName || r.partyCellName || undefined,
          }))
        );
      } else {
        const rows = await controlCheckService.candidates(periodFilter);
        setPickerRows(
          rows.map((r) => ({
            id: r.recordId,
            name: r.fullName,
            detail: [r.departmentName, r.partyCellName, `Mức hiện tại: ${gradeLabel(r.grade)} (${r.score})`].filter(Boolean).join(" · "),
            group: r.workflowProfileName,
            unit: r.departmentName || r.partyCellName || undefined,
          }))
        );
      }
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Tải danh sách hồ sơ không thành công."));
      setPickerOpen(false);
    } finally {
      setPickerLoading(false);
    }
  };

  const create = async (recordIds: string[], groupName: string) => {
    setCreating(true);
    try {
      if (active === "calibration") {
        const created = await calibrationService.create({ periodId: periodFilter, groupName: groupName || null, recordIds });
        toast.success("Đã lập biên bản.");
        router.push(`/quality-control/calibrations/${created.id}`);
      } else {
        const created = await controlCheckService.create({ periodId: periodFilter, groupName, recordIds });
        toast.success("Đã lập phiếu.");
        router.push(`/quality-control/control-checks/${created.id}`);
      }
    } catch (err: unknown) {
      toast.error(errorMessage(err, active === "calibration" ? "Lập biên bản không thành công." : "Lập phiếu không thành công."));
    } finally {
      setCreating(false);
    }
  };

  if (authLoading) return null;
  if (!canCal && !canCtl) return <NoAccess title="Duyệt kết quả" permissionName="Lập biên bản họp thống nhất, phiếu kiểm tra" />;

  const canCreate = active === "calibration" ? canCreateCal : canCreateCtl;
  const unit = (a?: string | null, b?: string | null) => [a, b].filter(Boolean).join(" · ");
  const tabs: { key: Tab; label: string; show: boolean }[] = [
    { key: "calibration", label: "Họp thống nhất", show: canCal },
    { key: "control", label: "Kiểm tra", show: canCtl },
  ];
  const statuses = active === "calibration" ? CAL_STATUSES.map((s) => ({ v: s, l: CALIBRATION_STATUS_LABELS[s] })) : CTL_STATUSES.map((s) => ({ v: s, l: CONTROL_STATUS_LABELS[s] }));

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Duyệt kết quả"
        actions={
          <div className="d-flex gap-2 flex-wrap align-items-center">
            <select className="form-select form-select-sm" aria-label="Lọc theo kỳ" value={periodFilter} onChange={(e) => setPeriod(e.target.value)}>
              <option value="">Tất cả kỳ</option>
              {periods.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
            <select className="form-select form-select-sm" aria-label="Lọc theo trạng thái" value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="">Mọi trạng thái</option>
              {statuses.map((s) => <option key={s.v} value={s.v}>{s.l}</option>)}
            </select>
            {canCreate && (
              <button type="button" className="btn btn-primary" onClick={openPicker}>
                {active === "calibration" ? "Lập biên bản" : "Lập phiếu"}
              </button>
            )}
          </div>
        }
      />
      <div className="page-body d-flex flex-column gap-3">
        <ul className="nav nav-tabs flex-nowrap" role="tablist" style={{ overflowX: "auto", overflowY: "hidden" }}>
          {tabs.filter((t) => t.show).map((t) => (
            <li className="nav-item" key={t.key}>
              <button type="button" role="tab" aria-selected={active === t.key} className={`nav-link ${active === t.key ? "active" : ""}`} onClick={() => selectTab(t.key)}>
                {t.label}
              </button>
            </li>
          ))}
        </ul>

        {loading || tab === null ? (
          <div className="text-secondary loading-delay">
            <span className="spinner-border spinner-border-sm me-2" />
            Đang tải...
          </div>
        ) : active === "calibration" ? (
          calShown.length === 0 ? (
            <EmptyState icon="bi-sliders" title="Chưa có biên bản họp thống nhất" description="Biên bản trong phạm vi của bạn sẽ hiện ở đây." />
          ) : (
            <>
              <div className="table-responsive d-none d-md-block">
                <table className="table table-hover align-middle mb-0">
                  <thead>
                    <tr>
                      <th>Nhóm chức danh / phạm vi</th>
                      <th>Kỳ</th>
                      <th className="text-end">Hồ sơ</th>
                      <th>Ngày họp</th>
                      <th>Người lập</th>
                      <th>Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody>
                    {calPaging.pageItems.map((c) => (
                      <tr key={c.id} role="link" style={{ cursor: "pointer" }} onClick={() => router.push(`/quality-control/calibrations/${c.id}`)}>
                        <td>
                          <Link href={`/quality-control/calibrations/${c.id}`} onClick={(e) => e.stopPropagation()} className="fw-semibold text-decoration-none">{c.groupName || "—"}</Link>
                          <div className="small text-secondary">{unit(c.departmentName, c.partyCellName) || "—"}</div>
                        </td>
                        <td>{c.periodName}</td>
                        <td className="text-end num">{c.recordCount}</td>
                        <td className="num">{formatDate(c.heldAt)}</td>
                        <td>{c.preparedByName}</td>
                        <td><Badge tone={CALIBRATION_STATUS_TONES[c.status] ?? "neutral"}>{c.statusName}</Badge></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="d-md-none d-flex flex-column gap-2">
                {calPaging.pageItems.map((c) => (
                  <Link key={c.id} href={`/quality-control/calibrations/${c.id}`} className="card text-decoration-none text-reset">
                    <div className="card-body py-2 px-3 d-flex flex-column gap-1">
                      <div className="d-flex justify-content-between align-items-start gap-2">
                        <span className="fw-semibold">{c.groupName || "—"}</span>
                        <Badge tone={CALIBRATION_STATUS_TONES[c.status] ?? "neutral"}>{c.statusName}</Badge>
                      </div>
                      <div className="small text-secondary">{unit(c.departmentName, c.partyCellName) || c.periodName}</div>
                      <div className="small text-secondary num">{c.recordCount} hồ sơ · {formatDate(c.heldAt)} · {c.preparedByName}</div>
                    </div>
                  </Link>
                ))}
              </div>
              <Pagination page={calPaging.page} pageSize={calPaging.pageSize} total={calPaging.total} onPageChange={calPaging.setPage} onPageSizeChange={calPaging.setPageSize} />
            </>
          )
        ) : ctlShown.length === 0 ? (
          <EmptyState icon="bi-shield-check" title="Chưa có phiếu kiểm tra" description="Phiếu trong phạm vi của bạn sẽ hiện ở đây." />
        ) : (
          <>
            <div className="table-responsive d-none d-md-block">
              <table className="table table-hover align-middle mb-0">
                <thead>
                  <tr>
                    <th>Nhóm chức danh / phạm vi</th>
                    <th>Kỳ</th>
                    <th className="text-end">Hồ sơ</th>
                    <th>Kết luận</th>
                    <th>Người lập</th>
                    <th>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {ctlPaging.pageItems.map((c) => (
                    <tr key={c.id} role="link" style={{ cursor: "pointer" }} onClick={() => router.push(`/quality-control/control-checks/${c.id}`)}>
                      <td>
                        <Link href={`/quality-control/control-checks/${c.id}`} onClick={(e) => e.stopPropagation()} className="fw-semibold text-decoration-none">{c.groupName || "—"}</Link>
                      </td>
                      <td>{c.periodName}</td>
                      <td className="text-end num">{c.recordCount}</td>
                      <td>{c.conclusionName || "—"}</td>
                      <td>{c.performedByName}</td>
                      <td>
                        <Badge tone={CONTROL_STATUS_TONES[c.status] ?? "neutral"}>{c.statusName}</Badge>
                        {c.isStale && <Badge tone="danger">Hết hiệu lực</Badge>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="d-md-none d-flex flex-column gap-2">
              {ctlPaging.pageItems.map((c) => (
                <Link key={c.id} href={`/quality-control/control-checks/${c.id}`} className="card text-decoration-none text-reset">
                  <div className="card-body py-2 px-3 d-flex flex-column gap-1">
                    <div className="d-flex justify-content-between align-items-start gap-2">
                      <span className="fw-semibold">{c.groupName || "—"}</span>
                      <span className="d-flex gap-1">
                        <Badge tone={CONTROL_STATUS_TONES[c.status] ?? "neutral"}>{c.statusName}</Badge>
                        {c.isStale && <Badge tone="danger">Hết hiệu lực</Badge>}
                      </span>
                    </div>
                    <div className="small text-secondary">{c.periodName}</div>
                    <div className="small text-secondary num">{c.recordCount} hồ sơ · {c.conclusionName || "Chưa kết luận"} · {c.performedByName}</div>
                  </div>
                </Link>
              ))}
            </div>
            <Pagination page={ctlPaging.page} pageSize={ctlPaging.pageSize} total={ctlPaging.total} onPageChange={ctlPaging.setPage} onPageSizeChange={ctlPaging.setPageSize} />
          </>
        )}
      </div>

      <RecordPickerDialog
        isOpen={pickerOpen}
        title={active === "calibration" ? "Lập biên bản" : "Lập phiếu"}
        confirmText={active === "calibration" ? "Lập biên bản" : "Lập phiếu"}
        rows={pickerRows}
        loading={pickerLoading}
        busy={creating}
        askGroupName
        groupNameRequired
        emptyText={active === "calibration" ? "Chưa có hồ sơ đã thẩm định để họp thống nhất." : "Chưa có hồ sơ chờ quyết định để kiểm tra."}
        onCancel={() => setPickerOpen(false)}
        onConfirm={create}
      />
    </div>
  );
}
