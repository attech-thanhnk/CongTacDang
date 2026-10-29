"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { CardToolbar, TabsRow, StickyActionBar } from "@/components/common/CardToolbar";
import Link from "next/link";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { formatDate } from "@/lib/dateFormat";
import { useToast } from "@/contexts/ToastContext";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { Pagination } from "@/components/common/Pagination";
import { usePagination } from "@/components/common/usePagination";
import { Button } from "@/components/common/Button";
import { RowActions, RowAction } from "@/components/common/RowActions";
import { NoAccess } from "@/components/admin/NoAccess";
import { CollectiveRecordEditor } from "@/components/collective/CollectiveRecordEditor";
import { MeetingEditor } from "@/components/collective/MeetingEditor";
import { CollectiveTrackingTable } from "@/components/collective/CollectiveTrackingTable";
import { CollectiveAttachments } from "@/components/collective/CollectiveAttachments";
import styles from "@/components/collective/Collective.module.css";
import { organizationService, BranchItem, DepartmentItem } from "@/services/organizationService";
import { CatalogState, CollectiveFormCatalog, CollectiveRecord, CollectiveTracking, MeetingRecord, collectiveService } from "@/services/collectiveService";
import { fmt } from "@/services/criteriaService";
import { errorMessage } from "@/components/admin/adminUtils";
import { ReportFileFormat, hd03FileName, reportService } from "@/services/reportService";
import { organizationSettingsService } from "@/services/organizationSettingsService";

const ROMAN = ["", "I", "II", "III", "IV"];
const FORM_NAMES: Record<string, string> = {
  M06: "Mẫu 06",
  M07: "Mẫu 07",
  M08: "Mẫu 08",
};
const STAGE_NAMES: Record<string, string> = {
  B3A_COLLECTIVE: "Tập thể lãnh đạo đề xuất",
  B4_DECISION: "Cấp ủy quyết định",
};

type Tab = "records" | "tracking" | "meetings";
/** Form đang mở: `null` trong `item` là lập mới. */
type Editor<T> = { item: T | null } | null;

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
  const [catalogState, setCatalogState] = useState<CatalogState>("loading");
  const [records, setRecords] = useState<CollectiveRecord[]>([]);
  const [meetings, setMeetings] = useState<MeetingRecord[]>([]);
  const [tracking, setTracking] = useState<CollectiveTracking | null>(null);
  const [recordEditor, setRecordEditor] = useState<Editor<CollectiveRecord>>(null);
  const [meetingEditor, setMeetingEditor] = useState<Editor<MeetingRecord>>(null);
  const [tab, setTab] = useState<Tab>("records");
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
  const canReadRecords = hasPermission("evaluation.read") || canManageCollective;
  const canReadMeetings = hasPermission("meeting.read") || canManageMeeting;
  // Chỉ có một phần được xem thì mở thẳng phần đó.
  const activeTab: Tab = !canReadRecords ? "meetings" : !canReadMeetings ? "records" : tab;

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
      .catch((error: unknown) => showError(errorMessage(error, "Tải dữ liệu đánh giá tập thể không thành công.")))
      .finally(() => setLoading(false));
    organizationSettingsService.getPublic().then((info) => setShortName(info.shortName)).catch(() => setShortName(""));
  }, [canUseModule, showError]);

  useEffect(() => {
    if (!canReadRecords || !periodId) return;
    let cancelled = false;
    setCatalog(null);
    setCatalogState("loading");
    collectiveService
      .getCatalog(periodId)
      .then((loaded) => {
        if (cancelled) return;
        setCatalog(loaded);
        setCatalogState("ready");
      })
      .catch((error: unknown) => {
        if (cancelled) return;
        setCatalogState("error");
        showError(errorMessage(error, "Tải khung tiêu chí tập thể của kỳ không thành công."));
      });
    return () => {
      cancelled = true;
    };
  }, [canReadRecords, periodId, showError]);

  /** Kỳ mà dữ liệu đang hiển thị thuộc về — kết quả về muộn của kỳ cũ bị bỏ qua. */
  const activePeriod = useRef<string | null>(null);

  const reload = useCallback(async () => {
    if (!periodId) return;
    const requested = periodId;
    const [recordsResult, meetingsResult, trackingResult] = await Promise.allSettled([
      canReadRecords ? collectiveService.getRecords(requested) : Promise.resolve([] as CollectiveRecord[]),
      canReadMeetings ? collectiveService.getMeetings(requested) : Promise.resolve([] as MeetingRecord[]),
      canReadRecords ? collectiveService.getTracking(requested) : Promise.resolve(null),
    ]);
    if (activePeriod.current !== requested) return;
    if (recordsResult.status === "fulfilled") setRecords(recordsResult.value);
    else showError(errorMessage(recordsResult.reason, "Tải hồ sơ tập thể không thành công."));
    if (meetingsResult.status === "fulfilled") setMeetings(meetingsResult.value);
    else showError(errorMessage(meetingsResult.reason, "Tải biên bản hội nghị không thành công."));
    if (trackingResult.status === "fulfilled") setTracking(trackingResult.value);
    else showError(errorMessage(trackingResult.reason, "Tải theo dõi hồ sơ tập thể không thành công."));
  }, [periodId, canReadRecords, canReadMeetings, showError]);

  useEffect(() => {
    setRecordEditor(null);
    setMeetingEditor(null);
    setRecords([]);
    setMeetings([]);
    setTracking(null);
    if (!periodId || !canUseModule) {
      activePeriod.current = null;
      return;
    }
    activePeriod.current = periodId;
    reload();
    return () => {
      activePeriod.current = null;
    };
  }, [periodId, canUseModule, reload]);

  const periodLabel = period ? `Quy ${ROMAN[period.quarter] ?? period.quarter}-${period.year}` : "Ky bao cao";

  const run = async (key: string, work: () => Promise<void>, failure: string) => {
    setExporting(key);
    try {
      await work();
    } catch (error: unknown) {
      showError(errorMessage(error, failure));
    } finally {
      setExporting(null);
    }
  };

  const exportRecord = (record: CollectiveRecord, format: ReportFileFormat) => {
    if (record.form !== "M06" && record.form !== "M07" && record.form !== "M08") return;
    const code = record.form === "M06" ? "06" : record.form === "M07" ? "07" : "08";
    return run(`${record.id}-${format}`, () => reportService.exportCollective(record.form as "M06" | "M07" | "M08", record.id,
      hd03FileName(code, record.partyCellName || record.departmentName || shortName, periodLabel, ".docx"), format), `Tải Mẫu ${code} không thành công.`);
  };

  const exportRecordExcel = (record: CollectiveRecord) =>
    run(`${record.id}-xlsx`, () => reportService.exportForm08Excel(record.id,
      hd03FileName("08", record.partyCellName || record.departmentName || shortName, periodLabel, ".xlsx")), "Tải Mẫu 08 (Excel) không thành công.");

  const exportMeeting = (meeting: MeetingRecord, form: "11" | "12" | "13", format: ReportFileFormat) =>
    run(`${meeting.id}-${form}-${format}`, () => reportService.exportMeeting(meeting.id, form,
      hd03FileName(form, meeting.partyCellName || meeting.departmentName || shortName, periodLabel, ".docx"), format), `Tải Mẫu ${form} không thành công.`);

  /** Thao tác của một hồ sơ: Sửa hiện trực tiếp, các bản xuất vào menu ⋯ (người chỉ xem thì bản Word hiện trực tiếp). */
  const recordActions = (record: CollectiveRecord): { primary: RowAction[]; menu: RowAction[] } => {
    const exports: RowAction[] = [];
    if (record.form === "M06" || record.form === "M07" || record.form === "M08") {
      exports.push({ key: "word", label: "Tải Word", icon: "bi-download", onClick: () => exportRecord(record, "original") });
      if (record.form === "M08") exports.push({ key: "xlsx", label: "Tải Excel", icon: "bi-download", onClick: () => exportRecordExcel(record) });
      exports.push({ key: "pdf", label: "Tải PDF", icon: "bi-filetype-pdf", onClick: () => exportRecord(record, "pdf") });
    }
    if (canManageCollective) {
      return { primary: [{ key: "edit", label: "Sửa", icon: "bi-pencil", onClick: () => setRecordEditor({ item: record }) }], menu: exports };
    }
    return { primary: exports.slice(0, 1), menu: exports.slice(1) };
  };

  const meetingActions = (meeting: MeetingRecord): { primary: RowAction[]; menu: RowAction[] } => {
    const exports: RowAction[] = [];
    if (meeting.formCode === "M12") {
      exports.push({ key: "12w", label: "Tải Mẫu 12 (Word)", icon: "bi-download", onClick: () => exportMeeting(meeting, "12", "original") });
      exports.push({ key: "12p", label: "Tải Mẫu 12 (PDF)", icon: "bi-filetype-pdf", onClick: () => exportMeeting(meeting, "12", "pdf") });
      exports.push({ key: "11w", label: "Tải Mẫu 11 phiếu trắng (Word)", icon: "bi-download", onClick: () => exportMeeting(meeting, "11", "original") });
      exports.push({ key: "11p", label: "Tải Mẫu 11 phiếu trắng (PDF)", icon: "bi-filetype-pdf", onClick: () => exportMeeting(meeting, "11", "pdf") });
    }
    if (meeting.voteSummaries.length > 0) {
      exports.push({ key: "13w", label: "Tải Mẫu 13 (Word)", icon: "bi-download", onClick: () => exportMeeting(meeting, "13", "original") });
      exports.push({ key: "13p", label: "Tải Mẫu 13 (PDF)", icon: "bi-filetype-pdf", onClick: () => exportMeeting(meeting, "13", "pdf") });
    }
    if (canManageMeeting) {
      return { primary: [{ key: "edit", label: "Sửa", icon: "bi-pencil", onClick: () => setMeetingEditor({ item: meeting }) }], menu: exports };
    }
    return { primary: exports.slice(0, 1), menu: exports.slice(1) };
  };

  const recordPaging = usePagination(records, { storageKey: "collective.records", resetKeys: [periodId] });
  const meetingPaging = usePagination(meetings, { storageKey: "collective.meetings", resetKeys: [periodId] });

  if (authLoading) return null;
  if (!canUseModule) {
    return <NoAccess title="Đánh giá tập thể" permissionName="Xem hồ sơ tập thể / biên bản hội nghị" />;
  }

  const pageTitle = "Đánh giá tập thể";

  if (loading || periodLoading) {
    return (
      <div className="page-wrapper">
        <PageHeader title={pageTitle} />
        <div className="page-body">
          <div className="text-secondary loading-delay"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div>
        </div>
      </div>
    );
  }

  if (!periodId) {
    return (
      <div className="page-wrapper">
        <PageHeader title={pageTitle} />
        <div className="page-body">
          <section className="card">
            <EmptyState
              icon="bi-calendar-x"
              title="Chưa có kỳ đánh giá"
              description="Hồ sơ tập thể và biên bản hội nghị hiện ở đây khi có kỳ đánh giá."
              action={hasPermission("period.manage") ? <Link href="/periods" className="btn btn-primary">Đến trang Kỳ đánh giá</Link> : undefined}
            />
          </section>
        </div>
      </div>
    );
  }

  // Đang lập/sửa: cả trang dành cho form, tiêu đề nói rõ đang làm gì.
  if (recordEditor) {
    const item = recordEditor.item;
    return (
      <div className="page-wrapper">
        <PageHeader title={item ? `Sửa hồ sơ ${FORM_NAMES[item.form] || item.form}` : "Lập hồ sơ tập thể"} subTitle={item?.subjectName} />
        <div className="page-body">
          <section className="card">
            <div className="card-body">
              <CollectiveRecordEditor
                key={item?.id ?? "new"}
                periodId={periodId}
                catalog={catalog}
                catalogState={catalogState}
                branches={branches}
                departments={departments}
                editing={item}
                showError={showError}
                onCancel={() => setRecordEditor(null)}
                onSaved={async (saved) => {
                  setRecordEditor(null);
                  setTab("records");
                  await reload();
                  showSuccess(`Đã lưu hồ sơ ${FORM_NAMES[saved.form] || saved.form}.`);
                }}
              />
            </div>
          </section>
        </div>
      </div>
    );
  }

  if (meetingEditor) {
    const item = meetingEditor.item;
    return (
      <div className="page-wrapper">
        <PageHeader
          title={item ? `Sửa biên bản ${item.formCode === "M13" ? "Mẫu 13" : "Mẫu 12"}` : "Lập biên bản hội nghị"}
          subTitle={item ? `${item.partyCellName || item.departmentName || "Cấp Công ty"} · ${formatDate(item.startedAt)}` : undefined}
        />
        <div className="page-body">
          <section className="card">
            <div className="card-body">
              <MeetingEditor
                key={item?.id ?? "new"}
                periodId={periodId}
                branches={branches}
                departments={departments}
                editing={item}
                showError={showError}
                onCancel={() => setMeetingEditor(null)}
                onSaved={async (saved) => {
                  setMeetingEditor(null);
                  setTab("meetings");
                  await reload();
                  showSuccess(`Đã lưu biên bản ${saved.formCode === "M13" ? "Mẫu 13" : "Mẫu 12"}.`);
                }}
              />
            </div>
          </section>
        </div>
      </div>
    );
  }

  const headerAction =
    activeTab === "tracking"
      ? undefined
      : activeTab === "records"
      ? canManageCollective && <Button icon="bi-plus-lg" onClick={() => setRecordEditor({ item: null })}>Lập hồ sơ</Button>
      : canManageMeeting && <Button icon="bi-plus-lg" onClick={() => setMeetingEditor({ item: null })}>Lập biên bản</Button>;

  const showTabs = canReadRecords && canReadMeetings;

  return (
    <div className="page-wrapper">
      <PageHeader title={pageTitle} />

      <div className="page-body">
        <div className="d-flex flex-column gap-3">
          {(showTabs || headerAction) && (
            <TabsRow actions={headerAction || undefined}>
            {showTabs ? <ul className="nav nav-tabs" role="tablist">
              {([
                ["records", "Hồ sơ tập thể", records.length],
                ...(canReadRecords ? [["tracking", "Theo dõi", tracking?.rows.length ?? 0]] : []),
                ["meetings", "Biên bản hội nghị", meetings.length],
              ] as [Tab, string, number][]).map(([key, label, count]) => (
                <li className="nav-item" key={key} role="presentation">
                  <button type="button" role="tab" aria-selected={activeTab === key} className={`nav-link ${activeTab === key ? "active" : ""}`} onClick={() => setTab(key)}>
                    {label} <span className="text-secondary fw-medium num ms-1">{count}</span>
                  </button>
                </li>
              ))}
            </ul> : <span />}
            </TabsRow>
          )}

          {activeTab === "records" && (
            <section className="card" aria-label="Hồ sơ tập thể">
              {records.length === 0 ? (
                <EmptyState
                  compact
                  icon="bi-people"
                  title="Chưa có hồ sơ tập thể trong kỳ"
                  description={canManageCollective ? "Lập hồ sơ tự đánh giá của tập thể (Mẫu 07) hoặc tổng hợp nhiệm vụ của đơn vị (Mẫu 08)." : undefined}
                />
              ) : (
                <ul className="list-group list-group-flush">
                  {recordPaging.pageItems.map((record) => {
                    const unitName = record.partyCellName || record.departmentName;
                    const actions = recordActions(record);
                    return (
                      <li key={record.id} className="list-group-item px-4 py-3">
                        <div className={styles.listItem}>
                          <div className={styles.listMain}>
                            <div className={styles.listTitle}>{record.subjectName}</div>
                            <div className={styles.listMeta}>
                              <span className="tag t-info">{FORM_NAMES[record.form] || record.form}</span>
                              {/* Tên tập thể thường trùng tên tổ chức: chỉ ghi khi khác. */}
                              {unitName && unitName !== record.subjectName && <span>{unitName}</span>}
                              {!unitName && <span>Chưa gắn tổ chức</span>}
                              {record.form !== "M08" && (
                                <span className={styles.dot}>
                                  <span className="num">{fmt(record.totalScore)}{catalog ? ` / ${fmt(catalog.generalMaxScore + catalog.taskMaxScore)}` : ""}</span> điểm
                                </span>
                              )}
                              {record.form === "M08" && <span className={styles.dot}><span className="num">{record.items.length}</span> nhiệm vụ</span>}
                            </div>
                          </div>
                          <RowActions primary={actions.primary} menu={actions.menu} itemLabel={record.subjectName} disabled={exporting !== null} />
                        </div>
                        <CollectiveAttachments recordId={record.id} formCode={record.form} editable={record.canAttach ?? false} />
                      </li>
                    );
                  })}
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
          )}

          {activeTab === "tracking" && (
            <section className="card" aria-label="Theo dõi hồ sơ tập thể">
              <CollectiveTrackingTable tracking={tracking} />
            </section>
          )}

          {activeTab === "meetings" && (
            <section className="card" aria-label="Biên bản hội nghị">
              {meetings.length === 0 ? (
                <EmptyState
                  compact
                  icon="bi-journal-text"
                  title="Chưa có biên bản trong kỳ"
                  description={canManageMeeting ? "Lập biên bản hội nghị (Mẫu 12) hoặc biên bản kiểm phiếu (Mẫu 13)." : undefined}
                />
              ) : (
                <ul className="list-group list-group-flush">
                  {meetingPaging.pageItems.map((item) => {
                    const actions = meetingActions(item);
                    return (
                      <li key={item.id} className="list-group-item px-4 py-3">
                        <div className={styles.listItem}>
                          <div className={styles.listMain}>
                            <div className={styles.listTitle}>{item.partyCellName || item.departmentName || "Cấp Công ty"}</div>
                            <div className={styles.listMeta}>
                              <span className="tag t-info">{item.formCode === "M13" ? "Mẫu 13" : "Mẫu 12"}</span>
                              <span>{item.stage ? STAGE_NAMES[item.stage] ?? item.stage : "Hội nghị"}</span>
                              <span className={`${styles.dot} num`}>{formatDate(item.startedAt)}</span>
                              {item.voteSummaries.length > 0 && <span className={styles.dot}>Có kết quả kiểm phiếu</span>}
                            </div>
                          </div>
                          <RowActions primary={actions.primary} menu={actions.menu} itemLabel="biên bản" disabled={exporting !== null} />
                        </div>
                      </li>
                    );
                  })}
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
          )}
        </div>
      </div>
    </div>
  );
}
