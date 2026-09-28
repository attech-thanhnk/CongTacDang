"use client";

import React, { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import {
  evaluationService,
  EvaluationPeriodDto,
  EvaluationRecordDto,
  WorkQueueDto,
} from "@/services/evaluationService";
import { organizationService } from "@/services/organizationService";
import { userService } from "@/services/userService";
import { useAuth } from "@/contexts/AuthContext";
import { Button } from "@/components/common";

const card: React.CSSProperties = {
  background: "var(--bg-card)",
  border: "1px solid var(--border-base)",
  borderRadius: "var(--radius-lg)",
  padding: "16px 20px",
  boxShadow: "var(--shadow-sm)",
  marginBottom: "16px",
};

const STEP_STATE_STYLE: Record<string, { border: string; background: string; color: string; mark: string }> = {
  done: { border: "1px solid var(--color-success-border)", background: "var(--color-success-bg)", color: "var(--color-success)", mark: "✓" },
  current: { border: "1.5px solid var(--color-cobalt)", background: "var(--color-primary-light)", color: "var(--color-cobalt)", mark: "●" },
  pending: { border: "1px solid var(--border-base)", background: "var(--bg-base)", color: "var(--text-muted)", mark: "" },
  skipped: { border: "1px dashed var(--border-base)", background: "var(--bg-base)", color: "var(--text-muted)", mark: "—" },
};

function periodShortName(period?: EvaluationPeriodDto | null) {
  if (!period) return "";
  const roman = ["", "I", "II", "III", "IV"][period.quarter] || String(period.quarter);
  return `Quý ${roman}/${period.year}`;
}

/**
 * Trang tổng quan. Mọi trạng thái/tiến trình lấy từ máy chủ: `work-queue` (việc đang chờ người dùng),
 * `my-record` (hồ sơ của mình, kèm `progress` 9 bước), danh sách hồ sơ trong phạm vi `evaluation.read`.
 */
export default function DashboardPage() {
  const { user, hasPermission } = useAuth();
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [periodId, setPeriodId] = useState("");
  const [queue, setQueue] = useState<WorkQueueDto | null>(null);
  const [myRecord, setMyRecord] = useState<EvaluationRecordDto | null>(null);
  const [records, setRecords] = useState<EvaluationRecordDto[]>([]);
  const [adminStats, setAdminStats] = useState<{ accounts: number; cells: number; departments: number } | null>(null);
  const [loading, setLoading] = useState(true);

  const canReadRecords = hasPermission("evaluation.read");
  const canReadUsers = hasPermission("system.users.read");
  const canManageCatalog = hasPermission("catalog.manage");

  useEffect(() => {
    evaluationService
      .getPeriods()
      .then((list) => {
        setPeriods(list);
        const active = list.find((p) => p.isActive) || list[0];
        if (active) setPeriodId(active.id);
        else setLoading(false);
      })
      .catch(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!user || (!canReadUsers && !canManageCatalog)) return;
    let mounted = true;
    Promise.all([
      canReadUsers ? userService.search({ pageSize: 1 }).then((r) => r.totalCount).catch(() => 0) : Promise.resolve(0),
      organizationService.getBranches().then((r) => r.length).catch(() => 0),
      organizationService.getDepartments().then((r) => r.length).catch(() => 0),
    ]).then(([accounts, cells, departments]) => {
      if (mounted) setAdminStats({ accounts, cells, departments });
    });
    return () => {
      mounted = false;
    };
  }, [user, canReadUsers, canManageCatalog]);

  useEffect(() => {
    if (!periodId || !user) return;
    let mounted = true;
    setLoading(true);
    Promise.all([
      evaluationService.getWorkQueue(periodId).catch(() => null),
      evaluationService.getMyRecord(periodId).catch(() => null),
      canReadRecords ? evaluationService.getRecordsByPeriod(periodId).catch(() => []) : Promise.resolve([]),
    ])
      .then(([q, mine, list]) => {
        if (!mounted) return;
        setQueue(q);
        setMyRecord(mine);
        setRecords(list);
      })
      .finally(() => mounted && setLoading(false));
    return () => {
      mounted = false;
    };
  }, [periodId, user, canReadRecords]);

  const period = periods.find((p) => p.id === periodId) || null;

  // Số hồ sơ theo trạng thái (bước đang chờ) — trạng thái và tên hiển thị do máy chủ trả.
  const statusCounts = useMemo(() => {
    const map = new Map<string, { name: string; count: number }>();
    for (const r of records) {
      const entry = map.get(r.status) || { name: r.statusDisplayName, count: 0 };
      entry.count++;
      map.set(r.status, entry);
    }
    return Array.from(map.entries()).map(([status, v]) => ({ status, ...v }));
  }, [records]);

  const byCell = useMemo(() => {
    const map = new Map<string, { total: number; published: number }>();
    for (const r of records) {
      const key = r.partyCellName || "Chưa thuộc Chi bộ";
      const entry = map.get(key) || { total: 0, published: 0 };
      entry.total++;
      if (r.status === "Published") entry.published++;
      map.set(key, entry);
    }
    return Array.from(map.entries()).sort(([a], [b]) => a.localeCompare(b, "vi"));
  }, [records]);

  return (
    <div className="page-wrapper">
      <div className="page-header-bar">
        <div className="page-header-content">
          <h1 style={{ fontSize: "20px", fontWeight: 700, margin: 0, color: "var(--text-primary)" }}>Tổng quan</h1>
          {period && (
            <div className="page-subtitle" style={{ fontSize: "12.5px", color: "var(--text-secondary)", marginTop: "2px" }}>
              {period.name} · {period.statusDisplayName}
            </div>
          )}
        </div>
        <div className="page-header-actions dashboard-header-actions">
          {periods.length > 0 && (
            <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
              <label htmlFor="dashboard-period-select" style={{ fontSize: "12.5px", color: "var(--text-secondary)", fontWeight: 500 }}>
                Kỳ:
              </label>
              <select
                id="dashboard-period-select"
                value={periodId}
                onChange={(e) => setPeriodId(e.target.value)}
                className="form-select form-select-sm fw-medium"
                style={{ fontSize: "13px", minWidth: "165px" }}
              >
                {periods.map((p) => (
                  <option key={p.id} value={p.id}>
                    {periodShortName(p)} {p.isActive ? "(Hiện hành)" : ""}
                  </option>
                ))}
              </select>
            </div>
          )}
          <Link href="/work-queue" style={{ textDecoration: "none" }}>
            <Button size="sm" variant="primary">Việc cần xử lý</Button>
          </Link>
        </div>
      </div>

      <div className="page-body">
        {adminStats && (
          <div style={card}>
            <h2 style={{ fontSize: "15px", fontWeight: 700, margin: "0 0 12px" }}>Quản trị hệ thống</h2>
            <div style={{ display: "flex", flexWrap: "wrap" }}>
              {canReadUsers && (
                <Link href="/admin/users" className="stat-block" style={{ flex: 1, minWidth: "140px", textDecoration: "none" }}>
                  <div className="stat-label">Tài khoản</div>
                  <div className="stat-value">{adminStats.accounts}</div>
                  <div className="stat-meta">Trong phạm vi được xem</div>
                </Link>
              )}
              <Link href="/catalog" className="stat-block" style={{ flex: 1, minWidth: "140px", textDecoration: "none" }}>
                <div className="stat-label">Phòng/đơn vị</div>
                <div className="stat-value">{adminStats.departments}</div>
                <div className="stat-meta">Danh mục tổ chức</div>
              </Link>
              <Link href="/catalog" className="stat-block" style={{ flex: 1, minWidth: "140px", textDecoration: "none" }}>
                <div className="stat-label">Chi bộ</div>
                <div className="stat-value">{adminStats.cells}</div>
                <div className="stat-meta">Tổ chức cơ sở Đảng</div>
              </Link>
            </div>
          </div>
        )}

        {!loading && periods.length === 0 && (
          <div style={card}>
            <div style={{ color: "var(--text-secondary)", fontSize: "13.5px" }}>
              Chưa có kỳ đánh giá nào.
              {hasPermission("period.manage") && (
                <> <Link href="/periods">Tạo kỳ đánh giá</Link>.</>
              )}
            </div>
          </div>
        )}

        {/* VIỆC ĐANG CHỜ NGƯỜI DÙNG (work-queue) */}
        {period && (
          <div style={card}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
              <h2 style={{ fontSize: "15px", fontWeight: 700, margin: 0 }}>
                Việc cần xử lý {queue ? `(${queue.total})` : ""}
              </h2>
              <Link href="/work-queue" style={{ fontSize: "12.5px", fontWeight: 600, textDecoration: "none" }}>
                Xem tất cả →
              </Link>
            </div>
            {loading ? (
              <span className="skeleton" style={{ display: "inline-block", width: "200px", height: "20px" }} />
            ) : !queue || queue.total === 0 ? (
              <div style={{ color: "var(--text-secondary)", fontSize: "13.5px" }}>Không có hồ sơ nào đang chờ bạn xử lý trong kỳ này.</div>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: "10px" }}>
                {queue.groups.map((group) => (
                  <div key={group.step}>
                    <div style={{ fontSize: "13px", fontWeight: 700, color: "var(--color-cobalt)", marginBottom: "4px" }}>
                      {group.stepName} ({group.count})
                    </div>
                    <ul style={{ margin: 0, paddingLeft: "18px", fontSize: "13.5px" }}>
                      {group.items.slice(0, 5).map((item) => (
                        <li key={item.recordId}>
                          <Link href={`/evaluations/${item.recordId}`}>{item.isOwnRecord ? "Hồ sơ của tôi" : item.fullName}</Link>
                          {item.departmentName ? <span style={{ color: "var(--text-secondary)" }}> · {item.departmentName}</span> : null}
                          {item.overdue ? <span style={{ color: "var(--color-danger)", fontWeight: 600 }}> · Quá hạn</span> : null}
                        </li>
                      ))}
                      {group.items.length > 5 && <li style={{ color: "var(--text-secondary)" }}>… và {group.items.length - 5} hồ sơ khác</li>}
                    </ul>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* HỒ SƠ CỦA TÔI (progress do máy chủ tính) */}
        {period && myRecord && (
          <div style={card}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
              <h2 style={{ fontSize: "15px", fontWeight: 700, margin: 0 }}>
                Hồ sơ của tôi · <span style={{ color: "var(--color-cobalt)" }}>{myRecord.statusDisplayName}</span>
              </h2>
              <Link href={`/evaluations/${myRecord.id}`} style={{ textDecoration: "none" }}>
                <Button size="sm" variant="primary">Mở hồ sơ</Button>
              </Link>
            </div>
            {myRecord.returnReason && (
              <div className="alert alert-warning py-2" style={{ fontSize: "13px" }}>
                Hồ sơ bị trả lại: {myRecord.returnReason}
              </div>
            )}
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(150px, 1fr))", gap: "8px" }}>
              {myRecord.progress.map((step) => {
                const style = STEP_STATE_STYLE[step.state] || STEP_STATE_STYLE.pending;
                return (
                  <div key={step.step} style={{ border: style.border, background: style.background, borderRadius: "var(--radius-md)", padding: "8px 10px" }}>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "11px", fontWeight: 700, color: style.color }}>
                      <span>{step.step}</span>
                      <span>{style.mark}</span>
                    </div>
                    <div style={{ fontSize: "12.5px", fontWeight: 600, color: "var(--text-primary)" }}>{step.name}</div>
                    {step.deadline && (
                      <div style={{ fontSize: "11px", color: step.overdue ? "var(--color-danger)" : "var(--text-secondary)" }}>
                        Hạn: {new Intl.DateTimeFormat("vi-VN").format(new Date(step.deadline))}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
            <div style={{ display: "flex", gap: "24px", flexWrap: "wrap", marginTop: "12px", fontSize: "13px", color: "var(--text-secondary)" }}>
              <span>Điểm tự chấm: <strong>{myRecord.totalSelfScore || "—"}</strong></span>
              <span>Thẩm định: <strong>{myRecord.appraisalScore ?? "—"}</strong></span>
              <span>Xếp loại chính thức: <strong>{myRecord.status === "Published" ? myRecord.finalGrade : "Chưa công bố"}</strong></span>
            </div>
          </div>
        )}

        {/* TIẾN ĐỘ TRONG PHẠM VI (evaluation.read) */}
        {period && canReadRecords && (
          <div style={card}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
              <h2 style={{ fontSize: "15px", fontWeight: 700, margin: 0 }}>
                Tiến độ trong phạm vi được xem ({records.length} hồ sơ)
              </h2>
              <Link href="/evaluations" style={{ fontSize: "12.5px", fontWeight: 600, textDecoration: "none" }}>
                Danh sách hồ sơ →
              </Link>
            </div>
            {records.length === 0 ? (
              <div style={{ color: "var(--text-secondary)", fontSize: "13.5px" }}>Chưa có hồ sơ nào trong phạm vi của bạn.</div>
            ) : (
              <>
                <div style={{ display: "flex", flexWrap: "wrap", gap: "8px", marginBottom: "14px" }}>
                  {statusCounts.map((s) => (
                    <span
                      key={s.status}
                      style={{ padding: "4px 10px", borderRadius: "12px", fontSize: "12.5px", border: "1px solid var(--border-base)", background: "var(--bg-base)" }}
                    >
                      {s.name}: <strong>{s.count}</strong>
                    </span>
                  ))}
                </div>
                <table className="table table-sm align-middle mb-0" style={{ fontSize: "13.5px" }}>
                  <thead>
                    <tr>
                      <th>Chi bộ</th>
                      <th style={{ textAlign: "center" }}>Số hồ sơ</th>
                      <th style={{ textAlign: "center" }}>Đã công bố</th>
                    </tr>
                  </thead>
                  <tbody>
                    {byCell.map(([name, v]) => (
                      <tr key={name}>
                        <td>{name}</td>
                        <td style={{ textAlign: "center" }}>{v.total}</td>
                        <td style={{ textAlign: "center" }}>{v.published}/{v.total}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
