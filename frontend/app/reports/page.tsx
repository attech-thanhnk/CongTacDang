"use client";

import React, { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { PageHeader, Button } from "@/components/common";
import { Form16DraftPanel } from "@/components/reports/Form16DraftPanel";
import { EvaluationPeriodDto, evaluationService } from "@/services/evaluationService";
import { BranchItem, organizationService } from "@/services/organizationService";
import { organizationSettingsService } from "@/services/organizationSettingsService";
import { REPORTS, ReportDefinition, ReportFileFormat, hd03FileName, reportService, withQuery } from "@/services/reportService";

const ROMAN = ["", "I", "II", "III", "IV"];

/** Nhãn kỳ trong tên tệp theo quy cách HD03 ("Quy III-2026"). */
const periodLabel = (period?: EvaluationPeriodDto) =>
  period ? `Quy ${ROMAN[period.quarter] ?? period.quarter}-${period.year}` : "Ky bao cao";

export default function ReportsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const showError = toast.error;
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [branches, setBranches] = useState<BranchItem[]>([]);
  const [periodId, setPeriodId] = useState("");
  const [branchId, setBranchId] = useState("");
  const [shortName, setShortName] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [showForm16, setShowForm16] = useState(false);

  const canExport = hasPermission("report.export");
  const canMeeting = hasPermission("meeting.read") || hasPermission("meeting.manage");

  useEffect(() => {
    evaluationService
      .getPeriods()
      .then((list) => {
        setPeriods(list);
        const active = list.find((p) => p.isActive) || list[0];
        if (active) setPeriodId(active.id);
      })
      .catch((err: any) => showError(err?.message || "Không tải được danh sách kỳ đánh giá."));
    organizationService.getBranches().then(setBranches).catch(() => setBranches([]));
    organizationSettingsService
      .getPublic()
      .then((info) => setShortName(info.shortName))
      .catch(() => setShortName(""));
  }, [showError]);

  const period = useMemo(() => periods.find((p) => p.id === periodId), [periods, periodId]);
  const branch = useMemo(() => branches.find((b) => b.id === branchId), [branches, branchId]);

  const canRun = (report: ReportDefinition) =>
    report.id === "mau-11" || report.id === "mau-13" ? canExport || canMeeting : canExport;

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
      const fileName = report.id === "cadres"
        ? "DanhSach_CanBo_{SHORT_NAME}.xlsx"
        : hd03FileName(report.fileCode, branch?.name || shortName, periodLabel(period), extension);
      await reportService.downloadReport(endpoint, fileName, format);
      toast.success(`Đã xuất ${report.code === "Nội bộ" ? report.title : report.code}.`);
    } catch (err: any) {
      toast.error(err?.message || "Không xuất được báo cáo. Hãy kiểm tra quyền và phạm vi được giao.");
    } finally {
      setBusy(null);
    }
  };

  const renderActions = (report: ReportDefinition) => {
    if (report.scope === "collective" || report.scope === "meeting") {
      return (
        <Link href="/collective-evaluations" className="btn btn-sm btn-outline-secondary" style={{ fontSize: "12px" }}>
          <i className="bi bi-box-arrow-up-right me-1" />
          Trang Tập thể & Hội nghị
        </Link>
      );
    }
    if (!canRun(report)) {
      return <span className="small text-secondary">Chưa được cấp quyền</span>;
    }
    return (
      <div className="d-flex flex-wrap gap-1 justify-content-end">
        {report.id === "mau-16" && (
          <Button size="sm" variant={showForm16 ? "primary" : "outline-primary"} icon="bi-pencil-square" onClick={() => setShowForm16(!showForm16)} style={{ fontSize: "12px" }}>
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
          style={{ fontSize: "12px" }}
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
            style={{ fontSize: "12px" }}
          >
            PDF
          </Button>
        )}
      </div>
    );
  };

  const renderGroup = (title: string, description: string, reports: ReportDefinition[]) => (
    <section className="card border-0 shadow-sm mb-4">
      <div className="card-header bg-white border-0 pt-3 px-3">
        <h2 className="h6 mb-1">{title}</h2>
        <div className="text-secondary small">{description}</div>
      </div>
      <div className="table-responsive">
        <table className="table align-middle mb-0">
          <thead className="table-light">
            <tr style={{ fontSize: "12.5px" }}>
              <th style={{ width: 90 }}>Mẫu</th>
              <th>Tên biểu mẫu</th>
              <th style={{ width: 80 }} className="text-center">Định dạng</th>
              <th style={{ width: 280 }} className="text-end">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {reports.map((report) => (
              <React.Fragment key={report.id}>
                <tr>
                  <td>
                    <span className="badge text-bg-light border" style={{ fontSize: "12px" }}>{report.code}</span>
                  </td>
                  <td>
                    <div className="fw-semibold" style={{ fontSize: "13.5px" }}>{report.title}</div>
                    {report.note && <div className="text-secondary" style={{ fontSize: "12px" }}>{report.note}</div>}
                  </td>
                  <td className="text-center small text-secondary">{report.fileType}</td>
                  <td className="text-end">{renderActions(report)}</td>
                </tr>
                {report.id === "mau-16" && showForm16 && periodId && canExport && (
                  <tr>
                    <td colSpan={4} className="bg-light p-0">
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
        subTitle="Hồ sơ nộp theo Hướng dẫn 03-HD/TVĐU (mục V.1) và báo cáo nội bộ."
        actions={
          <div className="d-flex flex-wrap gap-2">
            <select className="form-select form-select-sm" style={{ minWidth: 200 }} aria-label="Kỳ đánh giá" value={periodId} onChange={(e) => setPeriodId(e.target.value)}>
              {periods.length === 0 && <option value="">Chưa có kỳ đánh giá</option>}
              {periods.map((p) => (
                <option key={p.id} value={p.id}>{p.name}</option>
              ))}
            </select>
            <select className="form-select form-select-sm" style={{ minWidth: 240 }} aria-label="Phạm vi tổ chức Đảng" value={branchId} onChange={(e) => setBranchId(e.target.value)}>
              <option value="">Toàn Đảng bộ / theo phạm vi được giao</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
          </div>
        }
      />

      <div className="page-body">
        {renderGroup(
          "Hồ sơ nộp theo HD03",
          "Tên và bố cục đúng biểu mẫu gốc. Bảng tính (14, 15A, 15B) và văn bản Word (07, 12, 13, 16) theo quy định mục V.1; tên tệp theo quy cách \"Mau 16_<tên viết tắt>_Quy III-2026\".",
          REPORTS.filter((r) => r.group === "hd03")
        )}
        {renderGroup(
          "Báo cáo nội bộ",
          "Phục vụ theo dõi trong đơn vị, không mang số mẫu HD03.",
          REPORTS.filter((r) => r.group === "internal")
        )}
      </div>
    </div>
  );
}
