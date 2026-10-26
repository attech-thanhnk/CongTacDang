"use client";

import React, { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { PageHeader } from "@/components/common";
import { EmptyState } from "@/components/common/EmptyState";
import { RowActions, RowAction } from "@/components/common/RowActions";
import styles from "@/components/reports/Reports.module.css";
import { Form16DraftPanel } from "@/components/reports/Form16DraftPanel";
import { EvaluationPeriodDto } from "@/services/evaluationService";
import { BranchItem, organizationService } from "@/services/organizationService";
import { organizationSettingsService } from "@/services/organizationSettingsService";
import { usePermissionScope, withinScope } from "@/services/authService";
import { REPORTS, ReportDefinition, ReportFileFormat, hd03FileName, internalFileName, reportService, scopedFileName, withQuery } from "@/services/reportService";
import { errorMessage } from "@/components/admin/adminUtils";
import { NoAccess } from "@/components/admin/NoAccess";

const ROMAN = ["", "I", "II", "III", "IV"];

/** Nhãn kỳ trong tên tệp, vd. "Quy III-2026". */
const periodLabel = (period?: EvaluationPeriodDto | null) =>
  period ? `Quy ${ROMAN[period.quarter] ?? period.quarter}-${period.year}` : "Ky bao cao";

/** Biểu mẫu xuất theo Chi bộ: cần phạm vi tổ chức Đảng hoặc toàn công ty. */
const BRANCH_REPORTS = new Set(["mau-11", "mau-14", "mau-15a", "mau-15b", "mau-16", "decision", "excellent-quota"]);

export default function ReportsPage() {
  const { hasPermission, loading: authLoading } = useAuth();
  const { toast } = useToast();
  const { periodId, period } = usePeriod();
  const [branches, setBranches] = useState<BranchItem[]>([]);
  const [branchId, setBranchId] = useState("");
  const [shortName, setShortName] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [showForm16, setShowForm16] = useState(false);

  const canExport = hasPermission("report.export");
  const canMeeting = hasPermission("meeting.read");
  const canCollectivePage = hasPermission("evaluation.read") || hasPermission("collective.manage");
  const canMeetingPage = canMeeting || hasPermission("meeting.manage");
  // Chỉ cho chọn tổ chức Đảng trong phạm vi được giao; "Toàn Đảng bộ" chỉ khi được giao toàn công ty.
  const scope = usePermissionScope(canExport ? "report.export" : "meeting.read");
  const scopedBranches = useMemo(() => withinScope(branches, scope, "partyCell"), [branches, scope]);
  const canWholeParty = !!scope?.isGlobal;
  const noBranchScope = !!scope && !canWholeParty && scopedBranches.length === 0;

  useEffect(() => {
    organizationService.getBranches().then(setBranches).catch(() => setBranches([]));
    organizationSettingsService
      .getPublic()
      .then((info) => setShortName(info.shortName))
      .catch(() => setShortName(""));
  }, []);

  useEffect(() => {
    if (scope && !canWholeParty && !branchId && scopedBranches.length > 0) setBranchId(scopedBranches[0].id);
  }, [scope, canWholeParty, branchId, scopedBranches]);

  // Quyết định xếp loại: kiểm tra trước, chưa đủ điều kiện thì hiện lý do thay cho nút xuất.
  const [decisionBlocked, setDecisionBlocked] = useState<string | null>(null);
  useEffect(() => {
    setDecisionBlocked(null);
    if (!periodId || !canExport || !scope || noBranchScope) return;
    if (!canWholeParty && !branchId && scopedBranches.length > 0) return;
    let active = true;
    reportService
      .getDecisionReadiness(periodId, branchId || undefined)
      .then((r) => active && setDecisionBlocked(r.ready ? null : r.message || "Chưa lập được Quyết định."))
      .catch(() => active && setDecisionBlocked(null));
    return () => {
      active = false;
    };
  }, [periodId, branchId, canExport, scope, noBranchScope, canWholeParty, scopedBranches.length]);

  const branch = useMemo(() => branches.find((b) => b.id === branchId), [branches, branchId]);

  const canRun = (report: ReportDefinition) =>
    report.id === "mau-11" ? canExport || canMeeting : canExport;

  const download = async (report: ReportDefinition, format: ReportFileFormat) => {
    if (!report.endpoint) return;
    if (report.scope === "period" && !periodId) {
      toast.error("Hãy chọn kỳ đánh giá trước khi xuất.");
      return;
    }
    setBusy(`${report.id}-${format}`);
    try {
      const extension = report.fileType === "XLSX" ? ".xlsx" : ".docx";
      const endpoint = report.scope === "period" ? withQuery(report.endpoint, { periodId, branchId: branchId || undefined }) : report.endpoint;
      const unit = branch?.name || shortName;
      const fileName = report.id === "cadres"
        ? "DanhSach_CanBo_{SHORT_NAME}.xlsx"
        : report.filePrefix
          ? scopedFileName(report.filePrefix, unit, periodLabel(period), extension)
          : report.group === "internal"
            ? internalFileName(report.fileCode, unit, periodLabel(period), extension)
            : hd03FileName(report.fileCode, unit, periodLabel(period), extension);
      await reportService.downloadReport(endpoint, fileName, format);
      toast.success(`Đã xuất ${report.code === "Nội bộ" ? report.title : report.code}.`);
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Xuất báo cáo không thành công."));
    } finally {
      setBusy(null);
    }
  };

  /** Biểu mẫu lập theo từng hồ sơ/biên bản: xuất ở trang Tập thể & Hội nghị. */
  const isElsewhere = (report: ReportDefinition) => report.scope === "collective" || report.scope === "meeting";

  // Không có quyền thì không hiện dòng.
  const visible = (report: ReportDefinition) =>
    isElsewhere(report) ? (report.scope === "collective" ? canCollectivePage : canMeetingPage) : canRun(report);

  const fileLabel = (report: ReportDefinition) => (report.fileType === "XLSX" ? "Excel" : "Word");

  /** Lý do chưa xuất được (hiện dưới tên biểu mẫu, nút xuất bị tắt). */
  const blockedReason = (report: ReportDefinition): string | null => {
    if (noBranchScope && BRANCH_REPORTS.has(report.id)) return "Cần được giao ở Chi bộ hoặc toàn công ty.";
    if (report.id === "decision" && decisionBlocked) return decisionBlocked;
    return null;
  };

  const renderActions = (report: ReportDefinition) => {
    if (isElsewhere(report)) {
      return (
        <Link href="/collective-evaluations" className={styles.navLink}>
          Đến Tập thể & Hội nghị
          <i className="bi bi-arrow-right" aria-hidden="true" />
        </Link>
      );
    }
    const reason = blockedReason(report);
    const exportWord: RowAction = {
      key: "original",
      label: `Xuất ${fileLabel(report)}`,
      icon: "bi-download",
      disabled: !!reason,
      title: reason ?? undefined,
      onClick: () => download(report, "original"),
    };
    const exportPdf: RowAction = {
      key: "pdf",
      label: "Xuất PDF",
      icon: "bi-filetype-pdf",
      disabled: !!reason,
      hidden: report.scope === "none",
      onClick: () => download(report, "pdf"),
    };
    if (report.id === "mau-16") {
      return (
        <RowActions
          primary={[{ key: "draft", label: showForm16 ? "Đóng soạn thảo" : "Soạn báo cáo", icon: showForm16 ? "bi-x-lg" : "bi-pencil-square", disabled: !!reason || !periodId, title: reason ?? undefined, onClick: () => setShowForm16(!showForm16) }]}
          menu={[exportWord, exportPdf]}
          itemLabel={report.code}
          disabled={busy !== null}
        />
      );
    }
    return <RowActions primary={[exportWord]} menu={[exportPdf]} itemLabel={report.code === "Nội bộ" ? report.title : report.code} disabled={busy !== null} />;
  };

  const renderGroup = (title: string, reports: ReportDefinition[], showCode: boolean) => {
    const rows = reports.filter(visible);
    if (rows.length === 0) return null;
    return (
      <section className="card">
        <div className="card-header">
          <h2 className="mb-0">{title}</h2>
        </div>
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0 report-table">
            <thead>
              <tr>
                <th style={{ width: showCode ? 110 : 0 }}>{showCode ? "Mẫu" : ""}</th>
                <th>Tên biểu mẫu</th>
                <th style={{ width: 90 }} className="text-center">Định dạng</th>
                <th className={`${styles.actionsCell} text-end`}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((report) => {
                const reason = isElsewhere(report) ? null : blockedReason(report);
                const exporting = busy?.startsWith(`${report.id}-`);
                return (
                  <React.Fragment key={report.id}>
                    <tr>
                      <td>{showCode && <span className="tag t-info">{report.code}</span>}</td>
                      <td className="fw-semibold">
                        {report.title}
                        {exporting && (
                          <div className={styles.note}><span className="spinner-border spinner-border-sm me-1" />Đang xuất{busy?.endsWith("-pdf") ? " PDF" : ""}…</div>
                        )}
                        {!exporting && reason && <div className={`${styles.note} ${styles.noteWarn}`}>{reason}</div>}
                      </td>
                      <td className="text-center text-secondary">{report.fileType}</td>
                      <td className={styles.actionsCell}>{renderActions(report)}</td>
                    </tr>
                    {report.id === "mau-16" && showForm16 && periodId && canExport && !noBranchScope && (
                      <tr>
                        <td colSpan={4} className="p-0">
                          <Form16DraftPanel periodId={periodId} branchId={branchId || undefined} periodLabel={periodLabel(period)} shortName={shortName} />
                        </td>
                      </tr>
                    )}
                  </React.Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>
    );
  };

  if (authLoading) return null;
  if (!canExport && !canMeeting) return <NoAccess title="Báo cáo" permissionName="Xuất báo cáo hoặc xem hội nghị" />;

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Báo cáo"
        actions={
          <select className="form-select w-auto" style={{ minWidth: 240 }} aria-label="Phạm vi tổ chức Đảng" value={branchId} onChange={(e) => setBranchId(e.target.value)}>
            {(canWholeParty || scopedBranches.length === 0) && <option value="">{canWholeParty ? "Toàn Đảng bộ" : "Theo phạm vi được giao"}</option>}
            {scopedBranches.map((b) => (
              <option key={b.id} value={b.id}>{b.name}</option>
            ))}
          </select>
        }
      />

      <div className="page-body">
        {renderGroup("Biểu mẫu", REPORTS.filter((r) => r.group === "hd03"), true)}
        {renderGroup("Báo cáo nội bộ", REPORTS.filter((r) => r.group === "internal"), false)}
        {!REPORTS.some(visible) && (
          <section className="card">
            <EmptyState icon="bi-file-earmark-bar-graph" title="Không có báo cáo để xuất" description="Tài khoản chưa được giao quyền xuất báo cáo." />
          </section>
        )}
      </div>
    </div>
  );
}
