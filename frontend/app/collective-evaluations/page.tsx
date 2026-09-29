"use client";

import { useCallback, useEffect, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { PageHeader } from "@/components/common/PageHeader";
import { CollectiveRecordEditor } from "@/components/collective/CollectiveRecordEditor";
import { MeetingEditor } from "@/components/collective/MeetingEditor";
import { organizationService, BranchItem, DepartmentItem } from "@/services/organizationService";
import { EvaluationPeriodDto, evaluationService } from "@/services/evaluationService";
import { CollectiveFormCatalog, CollectiveRecord, MeetingRecord, collectiveService } from "@/services/collectiveService";
import { ReportFileFormat, hd03FileName, reportService } from "@/services/reportService";
import { organizationSettingsService } from "@/services/organizationSettingsService";

const ROMAN = ["", "I", "II", "III", "IV"];
const FORM_NAMES: Record<string, string> = {
  M06: "Mẫu 06",
  M07: "Mẫu 07",
  M08: "Mẫu 08",
};

export default function CollectiveEvaluationsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const showError = toast.error;
  const showSuccess = toast.success;
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [periodId, setPeriodId] = useState("");
  const [branches, setBranches] = useState<BranchItem[]>([]);
  const [departments, setDepartments] = useState<DepartmentItem[]>([]);
  const [catalog, setCatalog] = useState<CollectiveFormCatalog | null>(null);
  const [records, setRecords] = useState<CollectiveRecord[]>([]);
  const [meetings, setMeetings] = useState<MeetingRecord[]>([]);
  const [editingRecord, setEditingRecord] = useState<CollectiveRecord | null>(null);
  const [editingMeeting, setEditingMeeting] = useState<MeetingRecord | null>(null);
  const [shortName, setShortName] = useState("");
  const [exporting, setExporting] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const canUseModule =
    hasPermission("evaluation.read") ||
    hasPermission("collective.manage") ||
    hasPermission("meeting.read") ||
    hasPermission("meeting.manage");
  const canManageCollective = hasPermission("collective.manage");
  const canManageMeeting = hasPermission("meeting.manage");

  useEffect(() => {
    if (!canUseModule) {
      setLoading(false);
      return;
    }
    Promise.all([
      evaluationService.getPeriods(),
      organizationService.getBranches(),
      organizationService.getDepartments().catch(() => [] as DepartmentItem[]),
      collectiveService.getCatalog().catch(() => null),
    ])
      .then(([loadedPeriods, loadedBranches, loadedDepartments, loadedCatalog]) => {
        setPeriods(loadedPeriods);
        setBranches(loadedBranches);
        setDepartments(loadedDepartments);
        setCatalog(loadedCatalog);
        const active = loadedPeriods.find((item) => item.isActive) || loadedPeriods[0];
        if (active) setPeriodId(active.id);
      })
      .catch((error: any) => showError(error?.message || "Không thể tải dữ liệu đánh giá tập thể."))
      .finally(() => setLoading(false));
    organizationSettingsService.getPublic().then((info) => setShortName(info.shortName)).catch(() => setShortName(""));
  }, [canUseModule, showError]);

  const reload = useCallback(async () => {
    if (!periodId) return;
    const [loadedRecords, loadedMeetings] = await Promise.all([
      collectiveService.getRecords(periodId).catch(() => [] as CollectiveRecord[]),
      collectiveService.getMeetings(periodId).catch(() => [] as MeetingRecord[]),
    ]);
    setRecords(loadedRecords);
    setMeetings(loadedMeetings);
  }, [periodId]);

  useEffect(() => {
    if (!periodId || !canUseModule) return;
    setEditingRecord(null);
    setEditingMeeting(null);
    reload().catch((error: any) => showError(error?.message || "Không thể tải hồ sơ tập thể."));
  }, [periodId, canUseModule, reload, showError]);

  const period = periods.find((p) => p.id === periodId);
  const periodLabel = period ? `Quy ${ROMAN[period.quarter] ?? period.quarter}-${period.year}` : "Ky bao cao";

  const run = async (key: string, work: () => Promise<void>, failure: string) => {
    setExporting(key);
    try {
      await work();
    } catch (error: any) {
      showError(error?.message || failure);
    } finally {
      setExporting(null);
    }
  };

  const exportRecord = (record: CollectiveRecord, format: ReportFileFormat) => {
    if (record.form !== "M07" && record.form !== "M08") return;
    const code = record.form === "M07" ? "07" : "08";
    return run(`${record.id}-${format}`, () => reportService.exportCollective(record.form as "M07" | "M08", record.id,
      hd03FileName(code, record.partyCellName || record.departmentName || shortName, periodLabel, ".docx"), format), "Không xuất được biểu mẫu.");
  };

  /** Mẫu 08 bản Excel — HD03 V.1: hồ sơ lập trên file Excel (trừ Mẫu 07, 09C, 12, 13, 16). */
  const exportRecordExcel = (record: CollectiveRecord) =>
    run(`${record.id}-xlsx`, () => reportService.exportForm08Excel(record.id,
      hd03FileName("08", record.partyCellName || record.departmentName || shortName, periodLabel, ".xlsx")), "Không xuất được Mẫu 08 (Excel).");

  const exportMeeting = (meeting: MeetingRecord, form: "12" | "13", format: ReportFileFormat) =>
    run(`${meeting.id}-${form}-${format}`, () => reportService.exportMeeting(meeting.id, form,
      hd03FileName(form, meeting.partyCellName || meeting.departmentName || shortName, periodLabel, ".docx"), format), `Không xuất được Mẫu ${form}.`);

  const exportButtons = (key: string, onExport: (format: ReportFileFormat) => void, label = "Word", onExcel?: () => void) => (
    <div className="btn-group btn-group-sm">
      <button type="button" className="btn btn-outline-primary" disabled={exporting !== null} onClick={() => onExport("original")}>
        <i className="bi bi-file-earmark-word me-1" />{exporting === `${key}-original` ? "Đang xuất…" : label}
      </button>
      {onExcel && (
        <button type="button" className="btn btn-outline-success" disabled={exporting !== null} onClick={onExcel}>
          <i className="bi bi-file-earmark-excel me-1" />{exporting === `${key}-xlsx` ? "Đang xuất…" : "Excel"}
        </button>
      )}
      <button type="button" className="btn btn-outline-secondary" disabled={exporting !== null} onClick={() => onExport("pdf")}>
        <i className="bi bi-file-earmark-pdf me-1" />{exporting === `${key}-pdf` ? "Đang xuất…" : "PDF"}
      </button>
    </div>
  );

  if (!canUseModule) {
    return <div className="page-wrapper collective-page"><div className="alert alert-warning">Tài khoản chưa được cấp quyền xem hồ sơ tập thể.</div></div>;
  }

  if (loading) {
    return <div className="page-wrapper collective-page"><div className="text-secondary">Đang tải hồ sơ tập thể...</div></div>;
  }

  return (
    <div className="page-wrapper collective-page">
      <PageHeader
        title="Đánh giá tập thể & Hội nghị"
        subTitle="Hồ sơ tự đánh giá của tập thể (Mẫu 07, 08) và biên bản hội nghị (Mẫu 12, 13) theo kỳ; xuất Word/PDF đúng biểu mẫu HD03."
        actions={
          <div className="collective-toolbar-controls">
            <select className="form-select form-select-sm" aria-label="Kỳ đánh giá" value={periodId} onChange={(event) => setPeriodId(event.target.value)}>
              {periods.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
          </div>
        }
      />

      <div className="page-body">
        <div className="row g-3">
          <div className="col-12 col-xl-7">
            <section className="card border-0 shadow-sm mb-3">
              <div className="card-header bg-white border-0 pt-4 px-4">
                <h2 className="h5 mb-1">Hồ sơ tập thể</h2>
                <div className="text-secondary small">Mẫu 07, 08 bắt buộc Quý III/2026 (Mẫu 06 chưa áp dụng). Xuất Word/PDF ngay trên từng hồ sơ.</div>
              </div>
              <div className="card-body px-4">
                {records.length === 0 ? <div className="text-secondary small">Chưa có hồ sơ tập thể trong kỳ.</div> : records.map((record) => (
                  <div key={record.id} className="d-flex flex-wrap justify-content-between align-items-center border-bottom py-2 gap-2">
                    <div>
                      <strong>{record.subjectName}</strong>
                      <div className="text-secondary small">
                        {FORM_NAMES[record.form] || record.form} · {record.partyCellName || record.departmentName || "Chưa gắn tổ chức"}
                        {record.form !== "M08" && ` · ${record.totalScore.toFixed(1)} / 100`}
                        {record.form === "M08" && ` · ${record.items.length} nhiệm vụ`}
                      </div>
                    </div>
                    <div className="d-flex gap-2">
                      {canManageCollective && (
                        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => setEditingRecord(record)}>
                          <i className="bi bi-pencil me-1" />Sửa
                        </button>
                      )}
                      {record.form === "M07" && exportButtons(record.id, (format) => exportRecord(record, format))}
                      {record.form === "M08" && exportButtons(record.id, (format) => exportRecord(record, format), "Word", () => exportRecordExcel(record))}
                    </div>
                  </div>
                ))}
              </div>
            </section>

            {canManageCollective && periodId && (
              <section className="card border-0 shadow-sm">
                <div className="card-header bg-white border-0 pt-4 px-4">
                  <h2 className="h6 mb-1">{editingRecord ? `Sửa hồ sơ: ${editingRecord.subjectName}` : "Lập hồ sơ tập thể"}</h2>
                  <div className="text-secondary small">Các mục nhập theo đúng biểu mẫu gốc; mục để trống giữ dòng chấm trên bản xuất.</div>
                </div>
                <div className="card-body px-4">
                  <CollectiveRecordEditor
                    periodId={periodId}
                    catalog={catalog}
                    branches={branches}
                    departments={departments}
                    editing={editingRecord}
                    showError={showError}
                    onCancel={() => setEditingRecord(null)}
                    onSaved={async (saved) => {
                      setEditingRecord(null);
                      await reload();
                      showSuccess(`Đã lưu hồ sơ ${FORM_NAMES[saved.form] || saved.form}.`);
                    }}
                  />
                </div>
              </section>
            )}
          </div>

          <div className="col-12 col-xl-5">
            <section className="card border-0 shadow-sm mb-3">
              <div className="card-body px-4">
                <h2 className="h6">Biên bản đã lập</h2>
                {meetings.length === 0 ? <div className="text-secondary small">Chưa có biên bản trong kỳ.</div> : meetings.map((item) => (
                  <div key={item.id} className="border-bottom py-2 d-flex flex-wrap justify-content-between align-items-center gap-2">
                    <div>
                      <strong>{item.formCode === "M13" ? "Mẫu 13 — Kiểm phiếu" : "Mẫu 12 — Hội nghị"}</strong>
                      <div className="text-secondary small">
                        {item.partyCellName || item.departmentName || "Cấp Công ty"} · {item.stage === "B4_DECISION" ? "Quyết định" : item.stage === "B3A_COLLECTIVE" ? "Đề xuất tập thể" : "—"} · {new Date(item.startedAt).toLocaleDateString("vi-VN")}
                      </div>
                    </div>
                    <div className="d-flex gap-2">
                      {canManageMeeting && (
                        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => setEditingMeeting(item)}>
                          <i className="bi bi-pencil me-1" />Sửa
                        </button>
                      )}
                      {item.formCode === "M12" && exportButtons(`${item.id}-12`, (format) => exportMeeting(item, "12", format), "Mẫu 12")}
                      {item.voteSummaries.length > 0 && exportButtons(`${item.id}-13`, (format) => exportMeeting(item, "13", format), "Mẫu 13")}
                    </div>
                  </div>
                ))}
              </div>
            </section>

            {canManageMeeting && periodId && (
              <section className="card border-0 shadow-sm">
                <div className="card-header bg-white border-0 pt-4 px-4">
                  <h2 className="h6 mb-1">{editingMeeting ? "Sửa biên bản" : "Lập biên bản hội nghị"}</h2>
                  <div className="text-secondary small">Mẫu 12 là biên bản hội nghị; Mẫu 13 (biên bản kiểm phiếu) xuất từ biên bản đã có kết quả kiểm phiếu — Tổ kiểm phiếu và số phiếu nhập ở đây.</div>
                </div>
                <div className="card-body px-4">
                  <MeetingEditor
                    periodId={periodId}
                    branches={branches}
                    departments={departments}
                    editing={editingMeeting}
                    showError={showError}
                    onCancel={() => setEditingMeeting(null)}
                    onSaved={async (saved) => {
                      setEditingMeeting(null);
                      await reload();
                      showSuccess(`Đã lưu biên bản ${saved.formCode === "M13" ? "Mẫu 13" : "Mẫu 12"}.`);
                    }}
                  />
                </div>
              </section>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
