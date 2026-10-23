"use client";

import React, { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { PageHeader, Button } from "@/components/common";
import { Form16DraftPanel } from "@/components/reports/Form16DraftPanel";
import { EvaluationPeriodDto } from "@/services/evaluationService";
import { BranchItem, organizationService } from "@/services/organizationService";
import { organizationSettingsService } from "@/services/organizationSettingsService";
import { usePermissionScope, withinScope } from "@/services/authService";
import { REPORTS, ReportDefinition, ReportFileFormat, hd03FileName, internalFileName, reportService, scopedFileName, withQuery } from "@/services/reportService";
import { errorMessage } from "@/components/admin/adminUtils";

const ROMAN = ["", "I", "II", "III", "IV"];

/** Nhãn kỳ trong tên tệp, vd. "Quy III-2026". */
const periodLabel = (period?: EvaluationPeriodDto | null) =>
  period ? `Quy ${ROMAN[period.quarter] ?? period.quarter}-${period.year}` : "Ky bao cao";

/** Biểu mẫu xuất theo Chi bộ: cần phạm vi tổ chức Đảng hoặc toàn công ty. */
const BRANCH_REPORTS = new Set(["mau-11", "mau-14", "mau-15a", "mau-15b", "mau-16", "decision", "excellent-quota"]);

export default function ReportsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const { periodId, period } = usePeriod();
  const [branches, setBranches] = useState<BranchItem[]>([]);
  const [branchId, setBranchId] = useState("");
  const [shortName, setShortName] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [showForm16, setShowForm16] = useState(false);

  const canExport = hasPermission("report.export");
  const canMeeting = hasPermission("meeting.read");
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
      toast.error(errorMessage(err, "Không xuất được báo cáo. Hãy kiểm tra quyền và phạm vi được giao."));
    } finally {
      setBusy(null);
    }
  };

  const renderActions = (report: ReportDefinition) => {
    if (report.scope === "collective" || report.scope === "meeting") {
      return (
        <Link href="/collective-evaluations" className="btn btn-sm btn-outline-secondary">
          <i className="bi bi-box-arrow-up-right me-1" />
          Trang Tập thể & Hội nghị
        </Link>
      );
    }
    if (!canRun(report)) {
      return <span className="small text-secondary">Chưa được cấp quyền</span>;
    }
    if (noBranchScope && BRANCH_REPORTS.has(report.id)) {
      return <span className="small text-secondary">Cần được giao ở Chi bộ hoặc toàn công ty</span>;
    }
    if (report.id === "decision" && decisionBlocked) {
      return <span className="small text-secondary d-inline-block text-start" style={{ maxWidth: 280 }}>{decisionBlocked}</span>;
    }
    return (
      <div className="d-flex flex-wrap gap-1 justify-content-end">
        {report.id === "mau-16" && (
          <Button size="sm" variant={showForm16 ? "primary" : "outline-primary"} icon="bi-pencil-square" onClick={() => setShowForm16(!showForm16)}>
            Soạn báo cáo
          </Button>
        )}
        <Button
          size="sm"
          variant="outline-primary"
          icon={report.fileType === "XLSX" ? "bi-file-earmark-excel" : "bi-file-earmark-word"}
          loading={busy === `${report.id}-original`}
          loadingText="Đang xuất..."
          onClick={() => download(report, "original")}
        >
          {report.fileType === "XLSX" ? "Excel" : "Word"}
        </Button>
        {report.scope !== "none" && (
          <Button
            size="sm"
            variant="outline-secondary"
            icon="bi-file-earmark-pdf"
            loading={busy === `${report.id}-pdf`}
            loadingText="Đang xuất..."
            onClick={() => download(report, "pdf")}
          >
            PDF
          </Button>
        )}
      </div>
    );
  };

  const renderGroup = (title: string, reports: ReportDefinition[]) => (
    <section className="card">
      <div className="card-header">
        <h2 className="mb-0">{title}</h2>
      </div>
      <div className="table-responsive">
        <table className="table table-hover align-middle mb-0 report-table">
          <thead>
            <tr>
              <th style={{ width: 110 }}>Mẫu</th>
              <th>Tên biểu mẫu</th>
              <th style={{ width: 90 }} className="text-center">Định dạng</th>
              <th style={{ width: 280 }}></th>
            </tr>
          </thead>
          <tbody>
            {reports.map((report) => (
              <React.Fragment key={report.id}>
                <tr>
                  <td>
                    <span className="tag t-info">{report.code}</span>
                  </td>
                  <td className="fw-semibold">{report.title}</td>
                  <td className="text-center text-secondary">{report.fileType}</td>
                  <td className="text-end">{renderActions(report)}</td>
                </tr>
                {report.id === "mau-16" && showForm16 && periodId && canExport && !noBranchScope && (
                  <tr>
                    <td colSpan={4} className="p-0" style={{ background: "var(--tint)" }}>
                      <Form16DraftPanel periodId={periodId} branchId={branchId || undefined} periodLabel={periodLabel(period)} shortName={shortName} />
                    </td>
                  </tr>
                )}
              </React.Fragment>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );

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
        {renderGroup(
          "Biểu mẫu",
          REPORTS.filter((r) => r.group === "hd03")
        )}
        {renderGroup(
          "Báo cáo nội bộ",
          REPORTS.filter((r) => r.group === "internal")
        )}
      </div>
    </div>
  );
}
