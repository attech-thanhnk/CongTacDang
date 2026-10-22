"use client";

import { useCallback, useEffect, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { formatDate } from "@/lib/dateFormat";
import { useToast } from "@/contexts/ToastContext";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { Pagination } from "@/components/common/Pagination";
import { usePagination } from "@/components/common/usePagination";
import { NoAccess } from "@/components/admin/NoAccess";
import { CollectiveRecordEditor } from "@/components/collective/CollectiveRecordEditor";
import { MeetingEditor } from "@/components/collective/MeetingEditor";
import { CollectiveAttachments } from "@/components/collective/CollectiveAttachments";
import { usePermissionScope } from "@/services/authService";
import { organizationService, BranchItem, DepartmentItem } from "@/services/organizationService";
import { CollectiveFormCatalog, CollectiveRecord, MeetingRecord, collectiveService } from "@/services/collectiveService";
import { ReportFileFormat, hd03FileName, reportService } from "@/services/reportService";
import { organizationSettingsService } from "@/services/organizationSettingsService";

const ROMAN = ["", "I", "II", "III", "IV"];
const FORM_NAMES: Record<string, string> = {
  M06: "Mẫu 06",
  M07: "Mẫu 07",
  M08: "Mẫu 08",
};

/** Hồ sơ tập thể (Mẫu 06/07/08) và biên bản hội nghị (Mẫu 12/13) của kỳ đang chọn ở thanh bên. */
export default function CollectiveEvaluationsPage() {
  const { hasPermission, loading: authLoading } = useAuth();
  const { periodId, period, loading: periodLoading } = usePeriod();
  const { toast } = useToast();
  const showError = toast.error;
  const showSuccess = toast.success;
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
  // Phạm vi collective.manage (máy chủ đã gộp đơn vị con): đính kèm phụ lục khi hồ sơ thuộc phạm vi và kỳ chưa đóng.
  const collectiveScope = usePermissionScope("collective.manage");
  const canAttach = (record: CollectiveRecord) =>
    period?.status !== "Closed" &&
    !!collectiveScope &&
    (collectiveScope.isGlobal ||
      (!!record.partyCellId && collectiveScope.partyCellIds.includes(record.partyCellId)) ||
      (!!record.departmentId && collectiveScope.departmentIds.includes(record.departmentId)));

  useEffect(() => {
    if (!canUseModule) {
      setLoading(false);
      return;
    }
    Promise.all([
      organizationService.getBranches(),
      organizationService.getDepartments().catch(() => [] as DepartmentItem[]),
    ])
      .then(([loadedBranches, loadedDepartments]) => {
        setBranches(loadedBranches);
        setDepartments(loadedDepartments);
      })
      .catch((error: any) => showError(error?.message || "Không thể tải dữ liệu đánh giá tập thể."))
      .finally(() => setLoading(false));
    organizationSettingsService.getPublic().then((info) => setShortName(info.shortName)).catch(() => setShortName(""));
  }, [canUseModule, showError]);

  useEffect(() => {
    if (!canUseModule || !periodId) return;
    let cancelled = false;
    setCatalog(null);
    collectiveService
      .getCatalog(periodId)
      .catch(() => null)
      .then((loaded) => {
        if (!cancelled) setCatalog(loaded);
      });
    return () => {
      cancelled = true;
    };
  }, [canUseModule, periodId]);

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
    setEditingRecord(null);
    setEditingMeeting(null);
    if (!periodId || !canUseModule) {
      setRecords([]);
      setMeetings([]);
      return;
    }
    reload().catch((error: any) => showError(error?.message || "Không thể tải hồ sơ tập thể."));
  }, [periodId, canUseModule, reload, showError]);

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

  const recordPaging = usePagination(records, { storageKey: "collective.records", resetKeys: [periodId] });
  const meetingPaging = usePagination(meetings, { storageKey: "collective.meetings", resetKeys: [periodId] });

  if (authLoading) return null;
  if (!canUseModule) {
    return <NoAccess title="Đánh giá tập thể & Hội nghị" permissionName="Xem hồ sơ tập thể / biên bản hội nghị" />;
  }

  const title = <PageHeader title="Đánh giá tập thể & Hội nghị" />;

  if (loading || periodLoading) {
    return (
      <div className="page-wrapper">
        {title}
        <div className="page-body">
          <div className="text-secondary loading-delay"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
        </div>
      </div>
    );
  }

  if (!periodId) {
    return (
      <div className="page-wrapper">
        {title}
        <div className="page-body">
          <section className="card"><EmptyState icon="bi-calendar-x" title="Chưa có kỳ đánh giá" /></section>
        </div>
      </div>
    );
  }

  return (
    <div className="page-wrapper">
      {title}

      <div className="page-body">
        <div className="row g-4">
          <div className="col-12 col-xl-7 d-flex flex-column gap-4">
            <section className="card">
              <div className="card-header">
                <h2 className="mb-0">Hồ sơ tập thể <span className="text-secondary fw-medium num">{records.length}</span></h2>
              </div>
              {records.length === 0 ? (
                <div className="card-body text-secondary">Chưa có hồ sơ tập thể trong kỳ.</div>
              ) : (
                <ul className="list-group list-group-flush">
                  {recordPaging.pageItems.map((record) => (
                    <li key={record.id} className="list-group-item d-flex flex-wrap justify-content-between align-items-center gap-2 px-4 py-3">
                      <div className="min-w-0">
                        <div className="fw-semibold">{record.subjectName}</div>
                        <div className="text-secondary small">
                          <span className="tag t-info me-2">{FORM_NAMES[record.form] || record.form}</span>
                          {record.partyCellName || record.departmentName || "Chưa gắn tổ chức"}
                          {record.form !== "M08" && <> · <span className="num">{record.totalScore.toFixed(1)} / 100</span></>}
                          {record.form === "M08" && <> · <span className="num">{record.items.length}</span> nhiệm vụ</>}
                        </div>
                      </div>
                      <div className="d-flex flex-wrap gap-2">
                        {canManageCollective && (
                          <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => setEditingRecord(record)}>
                            <i className="bi bi-pencil me-1" />Sửa
                          </button>
                        )}
                        {record.form === "M07" && exportButtons(record.id, (format) => exportRecord(record, format))}
                        {record.form === "M08" && exportButtons(record.id, (format) => exportRecord(record, format), "Word", () => exportRecordExcel(record))}
                      </div>
                      <div className="w-100">
                        <CollectiveAttachments recordId={record.id} formCode={record.form} editable={canAttach(record)} />
                      </div>
                    </li>
                  ))}
                </ul>
              )}
              <Pagination
                page={recordPaging.page}
                pageSize={recordPaging.pageSize}
                total={recordPaging.total}
                onPageChange={recordPaging.setPage}
                onPageSizeChange={recordPaging.setPageSize}
              />
            </section>

            {canManageCollective && (
              <section className="card">
                <div className="card-header">
                  <h2 className="mb-0">{editingRecord ? `Sửa hồ sơ: ${editingRecord.subjectName}` : "Lập hồ sơ tập thể"}</h2>
                </div>
                <div className="card-body">
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

          <div className="col-12 col-xl-5 d-flex flex-column gap-4">
            <section className="card">
              <div className="card-header">
                <h2 className="mb-0">Biên bản đã lập <span className="text-secondary fw-medium num">{meetings.length}</span></h2>
              </div>
              {meetings.length === 0 ? (
                <div className="card-body text-secondary">Chưa có biên bản trong kỳ.</div>
              ) : (
                <ul className="list-group list-group-flush">
                  {meetingPaging.pageItems.map((item) => (
                    <li key={item.id} className="list-group-item d-flex flex-wrap justify-content-between align-items-center gap-2 px-4 py-3">
                      <div className="min-w-0">
                        <div className="fw-semibold">{item.formCode === "M13" ? "Mẫu 13 — Kiểm phiếu" : "Mẫu 12 — Hội nghị"}</div>
                        <div className="text-secondary small">
                          {item.partyCellName || item.departmentName || "Cấp Công ty"} · {item.stage === "B4_DECISION" ? "Quyết định" : item.stage === "B3A_COLLECTIVE" ? "Đề xuất tập thể" : "—"} · <span className="num">{formatDate(item.startedAt)}</span>
                        </div>
                      </div>
                      <div className="d-flex flex-wrap gap-2">
                        {canManageMeeting && (
                          <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => setEditingMeeting(item)}>
                            <i className="bi bi-pencil me-1" />Sửa
                          </button>
                        )}
                        {item.formCode === "M12" && exportButtons(`${item.id}-12`, (format) => exportMeeting(item, "12", format), "Mẫu 12")}
                        {item.voteSummaries.length > 0 && exportButtons(`${item.id}-13`, (format) => exportMeeting(item, "13", format), "Mẫu 13")}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
              <Pagination
                page={meetingPaging.page}
                pageSize={meetingPaging.pageSize}
                total={meetingPaging.total}
                onPageChange={meetingPaging.setPage}
                onPageSizeChange={meetingPaging.setPageSize}
              />
            </section>

            {canManageMeeting && (
              <section className="card">
                <div className="card-header">
                  <h2 className="mb-0">{editingMeeting ? "Sửa biên bản" : "Lập biên bản hội nghị"}</h2>
                </div>
                <div className="card-body">
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
