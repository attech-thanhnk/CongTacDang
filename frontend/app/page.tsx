"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { evaluationService, EvaluationPeriodDto, EvaluationRecordDto } from "@/services/evaluationService";
import { organizationService, BranchItem } from "@/services/organizationService";
import { userService, CadreItem } from "@/services/userService";
import { useAuth } from "@/contexts/AuthContext";
import { Button } from "@/components/common";

interface ActionTask {
  id: string;
  title: string;
  code: string;
  role: string;
  statusText: string;
  actionText: string;
  accentColor: string;
  href: string;
  urgency: "warning" | "info" | "primary" | "violet";
}

function getPeriodShortName(period?: EvaluationPeriodDto | null) {
  if (!period) return "";
  if (period.quarter && period.year) {
    const roman = period.quarter === 1 ? "I" : period.quarter === 2 ? "II" : period.quarter === 3 ? "III" : "IV";
    return `Quý ${roman}/${period.year}`;
  }
  return period.name.replace(/^Đánh giá, xếp loại cán bộ\s*/i, "").trim();
}

export default function DashboardPage() {
  const { user, hasPermission, hasRole } = useAuth();
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [selectedPeriodId, setSelectedPeriodId] = useState<string>("");
  const [loading, setLoading] = useState<boolean>(true);

  // State dành cho Cán bộ / Đảng viên thông thường
  const [myRecord, setMyRecord] = useState<EvaluationRecordDto | null>(null);

  // State dành cho Quản trị viên hệ thống (Admin)
  const [allRecords, setAllRecords] = useState<EvaluationRecordDto[]>([]);
  const [branches, setBranches] = useState<BranchItem[]>([]);
  const [cadres, setCadres] = useState<CadreItem[]>([]);

  const isAdmin = hasRole("QUAN_TRI_HE_THONG") || hasPermission("roles.manage");
  const canAppraise = hasPermission("evaluations.appraise");
  const canApprove = hasPermission("evaluations.approve");
  const canBranchReview =
    hasPermission("evaluations.branch_vote");

  // 1. Tải danh sách kỳ đánh giá ban đầu
  useEffect(() => {
    let isMounted = true;
    setLoading(true);

    const initPeriods = async () => {
      try {
        const pList = await evaluationService.getPeriods().catch(() => []);
        if (!isMounted) return;
        setPeriods(pList);
        const active = pList.find((p) => p.isActive) || pList[0];
        if (active) {
          setSelectedPeriodId(active.id);
        }
      } finally {
        if (isMounted) setLoading(false);
      }
    };

    initPeriods();
    return () => { isMounted = false; };
  }, []);

  // 2. Tải dữ liệu tương ứng theo vai trò (Admin vs Cán bộ) và Kỳ được chọn
  useEffect(() => {
    if (!selectedPeriodId || !user) return;
    let isMounted = true;

    const loadDashboardData = async () => {
      try {
        if (isAdmin) {
          // Quản trị viên: tải dữ liệu giám sát toàn đơn vị
          const [recs, bList, cList] = await Promise.all([
            evaluationService.getRecordsByPeriod(selectedPeriodId).catch(() => []),
            organizationService.getBranches().catch(() => []),
            userService.getUsers().catch(() => []),
          ]);
          if (!isMounted) return;
          setAllRecords(recs);
          setBranches(bList);
          setCadres(cList);
        } else {
          // Cán bộ thông thường: tải hồ sơ cá nhân
          const rec = await evaluationService.getMyRecord(selectedPeriodId).catch(() => null);
          if (isMounted) setMyRecord(rec);
        }
      } catch {
        if (isMounted) {
          setAllRecords([]);
          setMyRecord(null);
        }
      }
    };

    loadDashboardData();
    return () => { isMounted = false; };
  }, [selectedPeriodId, user, isAdmin]);

  const activePeriod = periods.find((p) => p.id === selectedPeriodId) || null;

  // ──────────────────────────────────────────────────────────────────────────
  // LOGIC & DATA DÀNH CHO CÁN BỘ / ĐẢNG VIÊN (NON-ADMIN)
  // ──────────────────────────────────────────────────────────────────────────
  const hasRegisteredTasks = (myRecord?.tasks?.length || 0) > 0;
  const hasSelfScored = (myRecord?.totalSelfScore || 0) > 0;
  const hasBranchReviewed = Boolean(myRecord?.partyCellComment || (myRecord?.totalVoters || 0) > 0);
  const hasAppraised = Boolean(myRecord?.appraisalScore || myRecord?.appraisalComment);
  const isFinalApproved = Boolean(myRecord?.finalScore || myRecord?.finalGrade);

  const pendingActions: ActionTask[] = [];

  if (!hasRegisteredTasks) {
    pendingActions.push({
      id: "step-1",
      title: "Đăng ký nhiệm vụ",
      code: "M01",
      role: "Cá nhân",
      statusText: "Chưa thực hiện",
      actionText: "Thực hiện",
      accentColor: "#d97706",
      href: `/evaluations?periodId=${selectedPeriodId}&step=1`,
      urgency: "warning",
    });
  } else if (!hasSelfScored) {
    pendingActions.push({
      id: "step-2",
      title: "Tự chấm điểm",
      code: "M02",
      role: "Cá nhân",
      statusText: "Chưa chấm điểm",
      actionText: "Chấm điểm",
      accentColor: "#d97706",
      href: `/evaluations?periodId=${selectedPeriodId}&step=2`,
      urgency: "warning",
    });
  }

  if (canBranchReview && !hasBranchReviewed) {
    pendingActions.push({
      id: "step-3",
      title: "Chi bộ đánh giá",
      code: "M10",
      role: "Chi ủy",
      statusText: "Chưa đánh giá",
      actionText: "Đánh giá",
      accentColor: "var(--color-cobalt)",
      href: `/evaluations?periodId=${selectedPeriodId}&step=3`,
      urgency: "info",
    });
  }

  if (canAppraise) {
    pendingActions.push({
      id: "step-4",
      title: "Thẩm định hồ sơ",
      code: "M03",
      role: "Tổ Thẩm định",
      statusText: "Đang thẩm định",
      actionText: "Thẩm định",
      accentColor: "var(--color-cobalt)",
      href: `/evaluations?periodId=${selectedPeriodId}&step=4`,
      urgency: "primary",
    });
  }

  if (canApprove) {
    pendingActions.push({
      id: "step-5",
      title: "Chuẩn y kết quả",
      code: "M07",
      role: "Ban Thường vụ",
      statusText: "Chưa chuẩn y",
      actionText: "Chuẩn y",
      accentColor: "#7c3aed",
      href: `/evaluations?periodId=${selectedPeriodId}&step=5`,
      urgency: "violet",
    });
  }

  const timelineSteps = [
    {
      num: "01", code: "M01", name: "Đăng ký",
      desc: hasRegisteredTasks ? `${myRecord?.tasks?.length} nhiệm vụ` : "Chưa đăng ký",
      isDone: hasRegisteredTasks,
      isCurrent: !hasRegisteredTasks,
      href: `/evaluations?periodId=${selectedPeriodId}&step=1`,
    },
    {
      num: "02", code: "M02", name: "Tự chấm",
      desc: hasSelfScored ? `${myRecord?.totalSelfScore} điểm` : "Khung 100 điểm",
      isDone: hasSelfScored,
      isCurrent: hasRegisteredTasks && !hasSelfScored,
      href: `/evaluations?periodId=${selectedPeriodId}&step=2`,
    },
    {
      num: "03", code: "M10", name: "Chi bộ",
      desc: hasBranchReviewed ? (myRecord?.partyCellProposedGrade || "Đã họp") : "Bỏ phiếu",
      isDone: hasBranchReviewed,
      isCurrent: hasSelfScored && !hasBranchReviewed,
      href: `/evaluations?periodId=${selectedPeriodId}&step=3`,
    },
    {
      num: "04", code: "M03", name: "Thẩm định",
      desc: hasAppraised ? `${myRecord?.appraisalScore} điểm` : "Trần 20%",
      isDone: hasAppraised,
      isCurrent: hasBranchReviewed && !hasAppraised,
      href: `/evaluations?periodId=${selectedPeriodId}&step=4`,
    },
    {
      num: "05", code: "M07", name: "Chuẩn y",
      desc: isFinalApproved ? (myRecord?.finalGrade || "Đã duyệt") : "Quyết định",
      isDone: isFinalApproved,
      isCurrent: hasAppraised && !isFinalApproved,
      href: `/evaluations?periodId=${selectedPeriodId}&step=5`,
    },
  ];

  const urgencyColors: Record<string, { bg: string; color: string; border: string }> = {
    warning: { bg: "var(--color-warning-bg)", color: "var(--color-warning)", border: "var(--color-warning-border)" },
    info:    { bg: "var(--color-info-bg)", color: "var(--color-info)", border: "var(--color-info-border)" },
    primary: { bg: "var(--color-primary-light)", color: "var(--color-cobalt)", border: "var(--color-primary-border)" },
    violet:  { bg: "#f5f3ff", color: "#7c3aed", border: "#ddd6fe" },
  };

  // ──────────────────────────────────────────────────────────────────────────
  // LOGIC & DATA DÀNH CHO QUẢN TRỊ VIÊN HỆ THỐNG (ADMIN)
  // ──────────────────────────────────────────────────────────────────────────
  const totalCadresCount = cadres.length || allRecords.length;
  const countStep1 = allRecords.filter((r) => (r.tasks?.length || 0) > 0).length;
  const countStep2 = allRecords.filter((r) => (r.totalSelfScore || 0) > 0).length;
  const countStep3 = allRecords.filter((r) => Boolean(r.partyCellProposedGrade || (r.totalVoters || 0) > 0)).length;
  const countStep4 = allRecords.filter((r) => Boolean(r.appraisalScore || r.appraisalProposedGrade)).length;
  const countStep5 = allRecords.filter((r) => Boolean(r.finalGrade)).length;

  return (
    <div className="page-wrapper">
      {/* ── PAGE HEADER ── */}
      <div className="page-header-bar">
        <div className="page-header-content">
          <h1 style={{ fontSize: "20px", fontWeight: 700, margin: 0, color: "var(--text-primary)" }}>
            {isAdmin ? "Tổng quan quản trị" : "Tổng quan"}
          </h1>
          {!isAdmin && myRecord?.partyCellName && (
            <div className="page-subtitle" style={{ fontSize: "12.5px", color: "var(--text-secondary)", marginTop: "2px" }}>
              {myRecord.partyCellName}
            </div>
          )}
        </div>

        <div className="page-header-actions dashboard-header-actions">
          {/* Bộ chọn kỳ đánh giá */}
          {periods.length > 0 && (
            <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
              <label htmlFor="dashboard-period-select" style={{ fontSize: "12.5px", color: "var(--text-secondary)", fontWeight: 500 }}>
                Kỳ:
              </label>
              <select
                id="dashboard-period-select"
                value={selectedPeriodId}
                onChange={(e) => setSelectedPeriodId(e.target.value)}
                className="form-select form-select-sm fw-medium"
                style={{
                  fontSize: "13px",
                  padding: "4px 28px 4px 10px",
                  borderRadius: "var(--radius-sm)",
                  borderColor: "var(--border-base)",
                  minWidth: "165px",
                  cursor: "pointer",
                }}
              >
                {periods.map((p) => (
                  <option key={p.id} value={p.id}>
                    {getPeriodShortName(p)} {p.isActive ? "(Hiện hành)" : ""}
                  </option>
                ))}
              </select>
            </div>
          )}

          <Link
            href={selectedPeriodId ? `/evaluations?periodId=${selectedPeriodId}` : "/evaluations"}
            style={{ textDecoration: "none" }}
          >
            <Button size="sm" variant="primary">
              Quy trình đánh giá
            </Button>
          </Link>
        </div>
      </div>

      {/* ── PAGE BODY ── */}
      <div className="page-body">
        {isAdmin ? (
          /* ================================================================ */
          /* GIAO DIỆN QUẢN TRỊ VIÊN HỆ THỐNG (SYSTEM ADMINISTRATOR)          */
          /* ================================================================ */
          <>
            {/* CHỈ SỐ HỆ THỐNG TOÀN ĐẢNG BỘ */}
            <div
              style={{
                background: "var(--bg-card)",
                border: "1px solid var(--border-base)",
                borderRadius: "var(--radius-lg)",
                display: "flex",
                flexWrap: "wrap",
                boxShadow: "var(--shadow-sm)",
                marginBottom: "20px",
              }}
            >
              <div className="stat-block" style={{ flex: 1, minWidth: "120px" }}>
                <div className="stat-label">Chi bộ trực thuộc</div>
                <div className="stat-value" style={{ color: "var(--text-primary)" }}>
                  {branches.length || 4}
                </div>
                <div className="stat-meta">Tổ chức cơ sở Đảng</div>
              </div>

              <div className="stat-block" style={{ flex: 1, minWidth: "120px" }}>
                <div className="stat-label">Cán bộ, Đảng viên</div>
                <div className="stat-value" style={{ color: "var(--text-primary)" }}>
                  {totalCadresCount}
                </div>
                <div className="stat-meta">Đối tượng đánh giá</div>
              </div>

              <div className="stat-block" style={{ flex: 1, minWidth: "120px" }}>
                <div className="stat-label">Hồ sơ trong kỳ</div>
                <div className="stat-value" style={{ color: "var(--color-cobalt)" }}>
                  {allRecords.length}
                </div>
                <div className="stat-meta">{getPeriodShortName(activePeriod)}</div>
              </div>

              <div className="stat-block" style={{ flex: 1, minWidth: "120px" }}>
                <div className="stat-label">Chuẩn y hoàn tất</div>
                <div className="stat-value" style={{ color: "var(--color-success)" }}>
                  {countStep5} <span style={{ fontSize: "14px", fontWeight: 400, color: "var(--text-muted)" }}>/ {allRecords.length}</span>
                </div>
                <div className="stat-meta">Quyết định BTV</div>
              </div>
            </div>

            {/* TIẾN ĐỘ 5 BƯỚC TOÀN ĐẢNG BỘ */}
            <div
              style={{
                background: "var(--bg-card)",
                border: "1px solid var(--border-base)",
                borderRadius: "var(--radius-lg)",
                padding: "16px 20px",
                marginBottom: "20px",
                boxShadow: "var(--shadow-sm)",
              }}
            >
              <div style={{ display: "flex", alignItems: "center", marginBottom: "14px" }}>
                <div>
                  <h2 style={{ fontSize: "15px", fontWeight: 700, margin: 0, color: "var(--text-primary)" }}>
                    Tiến độ quy trình 5 bước
                  </h2>
                </div>
              </div>

              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "12px" }}>
                {[
                  { step: "Bước 1", code: "M01", name: "Đăng ký nhiệm vụ", done: countStep1, total: allRecords.length, color: "#d97706" },
                  { step: "Bước 2", code: "M02", name: "Tự chấm điểm", done: countStep2, total: allRecords.length, color: "#2563eb" },
                  { step: "Bước 3", code: "M10", name: "Chi bộ đánh giá", done: countStep3, total: allRecords.length, color: "#0891b2" },
                  { step: "Bước 4", code: "M03", name: "Thẩm định hồ sơ", done: countStep4, total: allRecords.length, color: "#7c3aed" },
                  { step: "Bước 5", code: "M07", name: "Chuẩn y xếp loại", done: countStep5, total: allRecords.length, color: "#16a34a" },
                ].map((s) => {
                  const pct = s.total > 0 ? Math.round((s.done / s.total) * 100) : 0;
                  return (
                    <div
                      key={s.step}
                      style={{
                        border: "1px solid var(--border-base)",
                        borderRadius: "var(--radius-md)",
                        padding: "12px 14px",
                        background: "var(--bg-base)",
                      }}
                    >
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "6px" }}>
                        <span style={{ fontSize: "12px", fontWeight: 700, color: s.color }}>
                          {s.step} · {s.code}
                        </span>
                        <span style={{ fontSize: "12.5px", fontWeight: 700, color: "var(--text-primary)" }}>
                          {pct}%
                        </span>
                      </div>
                      <div style={{ fontSize: "13.5px", fontWeight: 600, color: "var(--text-primary)", marginBottom: "8px" }}>
                        {s.name}
                      </div>
                      <div
                        style={{
                          height: "6px",
                          borderRadius: "3px",
                          background: "var(--border-base)",
                          overflow: "hidden",
                          marginBottom: "6px",
                        }}
                      >
                        <div style={{ height: "100%", width: `${pct}%`, background: s.color, borderRadius: "3px" }} />
                      </div>
                      <div style={{ fontSize: "12px", color: "var(--text-secondary)" }}>
                        Đã hoàn thành: <strong>{s.done}</strong> / {s.total} cán bộ
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* DANH SÁCH CHI BỘ VÀ TRẠNG THÁI TIẾN ĐỘ */}
            <div
              style={{
                background: "var(--bg-card)",
                border: "1px solid var(--border-base)",
                borderRadius: "var(--radius-lg)",
                padding: "16px 20px",
                boxShadow: "var(--shadow-sm)",
              }}
            >
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "12px" }}>
                <h2 style={{ fontSize: "15px", fontWeight: 700, margin: 0, color: "var(--text-primary)" }}>
                  Tình trạng nộp hồ sơ theo từng Chi bộ
                </h2>
                <Link href="/users" style={{ fontSize: "12.5px", color: "var(--color-cobalt)", fontWeight: 600, textDecoration: "none" }}>
                  Xem danh sách cán bộ →
                </Link>
              </div>

              <div style={{ overflowX: "auto" }}>
                <table className="table table-hover align-middle mb-0" style={{ fontSize: "13.5px" }}>
                  <thead style={{ background: "var(--bg-base)" }}>
                    <tr>
                      <th style={{ padding: "8px 12px" }}>Chi bộ trực thuộc</th>
                      <th style={{ padding: "8px 12px", textAlign: "center" }}>Số cán bộ</th>
                      <th style={{ padding: "8px 12px", textAlign: "center" }}>M01 Đăng ký</th>
                      <th style={{ padding: "8px 12px", textAlign: "center" }}>M02 Tự chấm</th>
                      <th style={{ padding: "8px 12px", textAlign: "center" }}>M10 Chi bộ</th>
                      <th style={{ padding: "8px 12px", textAlign: "center" }}>M03 Thẩm định</th>
                      <th style={{ padding: "8px 12px", textAlign: "center" }}>Trần 20%</th>
                    </tr>
                  </thead>
                  <tbody>
                    {branches.map((b) => {
                      const branchRecs = allRecords.filter((r) => r.partyCellName === b.name || (r as any).partyCellId === b.id);
                      const bTotal = branchRecs.length;
                      const bM01 = branchRecs.filter((r) => (r.tasks?.length || 0) > 0).length;
                      const bM02 = branchRecs.filter((r) => (r.totalSelfScore || 0) > 0).length;
                      const bM10 = branchRecs.filter((r) => Boolean(r.partyCellProposedGrade || (r.totalVoters || 0) > 0)).length;
                      const bM03 = branchRecs.filter((r) => Boolean(r.appraisalScore)).length;

                      const renderCountBadge = (done: number, total: number) => {
                        if (total === 0) return <span style={{ color: "var(--text-muted)" }}>—</span>;
                        const isDone = done === total;
                        return (
                          <span
                            style={{
                              display: "inline-block",
                              padding: "2px 8px",
                              borderRadius: "12px",
                              fontSize: "12px",
                              fontWeight: 600,
                              background: isDone ? "var(--color-success-bg)" : "var(--bg-base)",
                              color: isDone ? "var(--color-success)" : "var(--text-secondary)",
                              border: `1px solid ${isDone ? "var(--color-success-border)" : "var(--border-base)"}`,
                            }}
                          >
                            {done}/{total}
                          </span>
                        );
                      };

                      return (
                        <tr key={b.id}>
                          <td style={{ padding: "10px 12px" }}>
                            <div style={{ fontWeight: 600, color: "var(--text-primary)" }}>{b.name}</div>
                            <div style={{ fontSize: "12px", color: "var(--text-secondary)" }}>{b.code} · {b.description || "Chi bộ cơ sở"}</div>
                          </td>
                          <td style={{ padding: "10px 12px", textAlign: "center", fontWeight: 600 }}>
                            {bTotal}
                          </td>
                          <td style={{ padding: "10px 12px", textAlign: "center" }}>
                            {renderCountBadge(bM01, bTotal)}
                          </td>
                          <td style={{ padding: "10px 12px", textAlign: "center" }}>
                            {renderCountBadge(bM02, bTotal)}
                          </td>
                          <td style={{ padding: "10px 12px", textAlign: "center" }}>
                            {renderCountBadge(bM10, bTotal)}
                          </td>
                          <td style={{ padding: "10px 12px", textAlign: "center" }}>
                            {renderCountBadge(bM03, bTotal)}
                          </td>
                          <td style={{ padding: "10px 12px", textAlign: "center", fontSize: "12.5px", color: "var(--text-secondary)" }}>
                            Đảm bảo &le; 20%
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          </>
        ) : (
          /* ================================================================ */
          /* GIAO DIỆN CÁN BỘ / ĐẢNG VIÊN (CADRE EVALUATION DASHBOARD)        */
          /* ================================================================ */
          <>
            {/* STATS ROW */}
            <div
              style={{
                background: "var(--bg-card)",
                border: "1px solid var(--border-base)",
                borderRadius: "var(--radius-lg)",
                display: "flex",
                flexWrap: "wrap",
                boxShadow: "var(--shadow-sm)",
              }}
            >
              <div className="stat-block" style={{ flex: 1, minWidth: "120px" }}>
                <div className="stat-label">Nhiệm vụ đăng ký</div>
                <div className="stat-value" style={{ color: hasRegisteredTasks ? "var(--text-primary)" : "var(--text-muted)" }}>
                  {loading ? (
                    <span className="skeleton" style={{ display: "inline-block", width: "60px", height: "26px" }} />
                  ) : hasRegisteredTasks ? (
                    myRecord?.tasks?.length
                  ) : (
                    <span style={{ fontSize: "14px", fontWeight: 500 }}>Chưa đăng ký</span>
                  )}
                </div>
                <div className="stat-meta">Mẫu M01</div>
              </div>

              <div className="stat-block" style={{ flex: 1, minWidth: "120px" }}>
                <div className="stat-label">Điểm tự chấm</div>
                <div className="stat-value" style={{ color: hasSelfScored ? "var(--color-cobalt)" : "var(--text-muted)" }}>
                  {loading ? (
                    <span className="skeleton" style={{ display: "inline-block", width: "60px", height: "26px" }} />
                  ) : hasSelfScored ? (
                    myRecord?.totalSelfScore
                  ) : (
                    <span style={{ fontSize: "14px", fontWeight: 500 }}>Chưa chấm</span>
                  )}
                </div>
                <div className="stat-meta">Mẫu M02 · Thang 100</div>
              </div>

              <div className="stat-block" style={{ flex: 1, minWidth: "120px" }}>
                <div className="stat-label">Chi bộ xếp loại</div>
                <div className="stat-value" style={{ color: hasBranchReviewed ? "var(--color-cobalt)" : "var(--text-muted)" }}>
                  {loading ? (
                    <span className="skeleton" style={{ display: "inline-block", width: "60px", height: "26px" }} />
                  ) : myRecord?.partyCellProposedGrade ? (
                    <span style={{ fontSize: "15px", fontWeight: 700 }}>{myRecord.partyCellProposedGrade}</span>
                  ) : (
                    <span style={{ fontSize: "14px", fontWeight: 500 }}>Chưa họp</span>
                  )}
                </div>
                <div className="stat-meta">Mẫu M10 · Chi ủy</div>
              </div>

              <div className="stat-block" style={{ flex: 1, minWidth: "120px" }}>
                <div className="stat-label">Xếp loại chính thức</div>
                <div className="stat-value" style={{ color: isFinalApproved ? "var(--color-success)" : "var(--text-muted)" }}>
                  {loading ? (
                    <span className="skeleton" style={{ display: "inline-block", width: "60px", height: "26px" }} />
                  ) : myRecord?.finalGrade ? (
                    <span style={{ fontSize: "15px", fontWeight: 700 }}>{myRecord.finalGrade}</span>
                  ) : (
                    <span style={{ fontSize: "14px", fontWeight: 500 }}>Chưa chuẩn y</span>
                  )}
                </div>
                <div className="stat-meta">Mẫu M07 · BTV Đảng ủy</div>
              </div>
            </div>

            {/* TIẾN ĐỘ QUY TRÌNH ĐÁNH GIÁ CỦA CÁ NHÂN */}
            <div
              style={{
                marginTop: "16px",
                background: "var(--bg-card)",
                border: "1px solid var(--border-base)",
                borderRadius: "var(--radius-lg)",
                padding: "16px 20px",
                boxShadow: "var(--shadow-sm)",
              }}
            >
              <div style={{ display: "flex", alignItems: "center", marginBottom: "16px" }}>
                <div>
                  <h2 style={{ fontSize: "15px", fontWeight: 700, margin: 0, color: "var(--text-primary)" }}>
                    Tiến độ đánh giá cá nhân
                  </h2>
                </div>
              </div>

              {/* TIMELINE 5 BƯỚC */}
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: "10px" }}>
                {timelineSteps.map((step) => (
                  <Link key={step.num} href={step.href} style={{ textDecoration: "none" }}>
                    <div
                      style={{
                        border: step.isCurrent ? "1.5px solid var(--color-cobalt)" : "1px solid var(--border-base)",
                        background: step.isCurrent ? "var(--color-primary-light)" : "var(--bg-base)",
                        borderRadius: "var(--radius-md)",
                        padding: "10px 12px",
                        cursor: "pointer",
                        transition: "all 0.15s ease",
                      }}
                    >
                      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "4px" }}>
                        <span style={{ fontSize: "11px", fontWeight: 700, color: step.isCurrent ? "var(--color-cobalt)" : "var(--text-muted)" }}>
                          {step.num} · {step.code}
                        </span>
                        {step.isDone && (
                          <span style={{ fontSize: "12px", color: "var(--color-success)", fontWeight: 700 }}>✓</span>
                        )}
                      </div>
                      <div style={{ fontSize: "12.5px", fontWeight: 600, color: "var(--text-primary)" }}>
                        {step.name}
                      </div>
                      <div style={{ fontSize: "11px", color: "var(--text-secondary)", marginTop: "2px" }}>
                        {step.desc}
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            </div>

            {/* NHIỆM VỤ CẦN XỬ LÝ THEO PHÂN QUYỀN */}
            {pendingActions.length > 0 && (
              <div
                style={{
                  marginTop: "16px",
                  background: "var(--bg-card)",
                  border: "1px solid var(--border-base)",
                  borderRadius: "var(--radius-lg)",
                  padding: "16px 20px",
                  boxShadow: "var(--shadow-sm)",
                }}
              >
                <div style={{ fontSize: "14px", fontWeight: 700, color: "var(--text-primary)", marginBottom: "12px" }}>
                  Việc cần xử lý ({pendingActions.length})
                </div>

                <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                  {pendingActions.map((task) => {
                    const uColor = urgencyColors[task.urgency] || urgencyColors.info;

                    return (
                      <div
                        key={task.id}
                        style={{
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "space-between",
                          padding: "10px 14px",
                          borderRadius: "var(--radius-md)",
                          border: `1px solid ${uColor.border}`,
                          background: uColor.bg,
                          gap: "12px",
                          flexWrap: "wrap",
                        }}
                      >
                        <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                          <span
                            style={{
                              fontSize: "11px",
                              fontWeight: 700,
                              padding: "2px 6px",
                              borderRadius: "4px",
                              background: "rgba(0,0,0,0.06)",
                              color: uColor.color,
                            }}
                          >
                            {task.code}
                          </span>
                          <div>
                            <div style={{ fontSize: "13px", fontWeight: 600, color: "var(--text-primary)" }}>
                              {task.title}
                            </div>
                            <div style={{ fontSize: "11.5px", color: "var(--text-secondary)" }}>
                              Vai trò: {task.role} · Trạng thái: <strong>{task.statusText}</strong>
                            </div>
                          </div>
                        </div>

                        <Link href={task.href} style={{ textDecoration: "none" }}>
                          <Button size="sm" variant="primary">
                            {task.actionText}
                          </Button>
                        </Link>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
