"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { RowActions } from "@/components/common/RowActions";
import { HeaderMenu } from "@/components/common/HeaderMenu";
import styles from "@/components/periods/periods.module.css";
import { useParams } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { AdminModal } from "@/components/admin/AdminModal";
import { formatDateTime } from "@/lib/dateFormat";
import { useToast } from "@/contexts/ToastContext";
import { ReasonDialog } from "@/components/evaluations/ReasonDialog";
import { WorkflowProfilesEditor } from "@/components/evaluations/WorkflowProfilesEditor";
import { ReadinessPanel } from "@/components/evaluations/ReadinessPanel";
import { organizationService, BranchItem, DepartmentItem } from "@/services/organizationService";
import { ApiError } from "@/services/apiClient";
import {
  EvaluationPeriodDto,
  ParticipantCandidateDto,
  PeriodParticipantDto,
  PeriodReadinessDto,
  PeriodSettings,
  StepPermissionOptionDto,
  WorkflowProfile,
  evaluationService,
} from "@/services/evaluationService";
import { CriteriaSummary } from "@/components/evaluations/CriteriaSummary";
import { PeriodStatusBadge } from "@/components/periods/PeriodStatusBadge";
import { PeriodProgressSection } from "@/components/periods/PeriodProgressSection";
import { RecordStatusBadge } from "@/components/common/StatusBadge";
import { Pagination } from "@/components/common/Pagination";
import { usePagination } from "@/components/common/usePagination";
import { CriteriaSetListItem, WeightFrame, criteriaService } from "@/services/criteriaService";
import { reportService, withQuery } from "@/services/reportService";
import { errorMessage, errorStatus } from "@/components/admin/adminUtils";

type Transition = "open" | "lock" | "unlock" | "close";
type PeriodTab = "progress" | "profiles" | "criteria" | "readiness" | "participants";

/** Cấu hình kỳ; diện quyết định hiển thị với tên "Diện quyết định". */
export default function PeriodDetailPage() {
  const params = useParams<{ periodId: string }>();
  const periodId = params?.periodId;
  const { hasPermission } = useAuth();
  const canManage = hasPermission("period.manage");
  const { toast, confirm } = useToast();
  const { reload: reloadPeriods } = usePeriod();
  const [period, setPeriod] = useState<EvaluationPeriodDto | null>(null);
  const [settings, setSettings] = useState<PeriodSettings | null>(null);
  const [participants, setParticipants] = useState<PeriodParticipantDto[]>([]);
  const [departments, setDepartments] = useState<DepartmentItem[]>([]);
  const [cells, setCells] = useState<BranchItem[]>([]);
  const [permissionOptions, setPermissionOptions] = useState<StepPermissionOptionDto[]>([]);
  const [readiness, setReadiness] = useState<PeriodReadinessDto | null>(null);
  const [checking, setChecking] = useState(false);
  const [busy, setBusy] = useState(false);
  const [unlockOpen, setUnlockOpen] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [forceOpen, setForceOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<PeriodTab>(canManage ? "participants" : "criteria");
  // Cấu hình diện quyết định đang sửa dở: nạp lại kỳ (sau thêm người, đổi trạng thái…) không ghi đè lên.
  const dirtyRef = useRef(false);
  // Lần nạp đầu: kỳ Đang mở / Khóa dữ liệu thì mở sẵn tab Tiến độ.
  const initialTabRef = useRef(false);
  const [dirty, setDirty] = useState(false);
  const setDirtyState = (value: boolean) => {
    dirtyRef.current = value;
    setDirty(value);
  };

  const checkReadiness = useCallback(async () => {
    if (!periodId || !canManage) return;
    setChecking(true);
    try {
      setReadiness(await evaluationService.getReadiness(periodId));
    } catch {
      setReadiness(null);
    } finally {
      setChecking(false);
    }
  }, [periodId, canManage]);

  const load = useCallback(async () => {
    if (!periodId) return;
    try {
      const loaded = await evaluationService.getPeriod(periodId);
      setPeriod(loaded);
      if (!initialTabRef.current) {
        initialTabRef.current = true;
        if (canManage && (loaded.status === "Open" || loaded.status === "Locked")) setTab("progress");
      }
      if (!dirtyRef.current) setSettings(JSON.parse(JSON.stringify(loaded.settings)));
      if (canManage) {
        setParticipants(await evaluationService.getParticipants(periodId));
        if (loaded.status === "Draft" || loaded.status === "Open" || loaded.status === "Locked") await checkReadiness();
      }
    } catch (err: unknown) {
      setError(errorMessage(err, "Tải kỳ đánh giá không thành công."));
    }
  }, [periodId, canManage, checkReadiness]);

  useEffect(() => {
    load();
    organizationService.getDepartments().then(setDepartments).catch(() => setDepartments([]));
    organizationService.getBranches().then(setCells).catch(() => setCells([]));
  }, [load]);

  useEffect(() => {
    if (canManage) evaluationService.getStepPermissions().then(setPermissionOptions).catch(() => setPermissionOptions([]));
  }, [canManage]);

  // Kỳ không còn Đang mở / Khóa dữ liệu (đã đóng) khi đang xem Tiến độ → về tab mặc định.
  useEffect(() => {
    if (tab === "progress" && period && !(canManage && (period.status === "Open" || period.status === "Locked")))
      setTab(canManage ? "participants" : "criteria");
  }, [tab, period, canManage]);

  if (error) return <div className="page-wrapper"><div className="alert alert-danger m-4">{error}</div></div>;
  if (!period || !settings) return <div className="page-wrapper"><div className="text-secondary p-4 loading-delay"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div></div>;

  const isDraft = period.status === "Draft";
  const editable = canManage && isDraft;
  const deadlinesEditable = canManage && period.status !== "Closed";
  const showReadiness = canManage && period.status !== "Closed";
  const stepIssues = readiness?.issues.filter((i) => i.permission) ?? [];
  const frameIssues = readiness?.issues.filter((i) => !i.permission) ?? [];
  const showProgress = canManage && (period.status === "Open" || period.status === "Locked");
  const overdueCount = participants.filter((p) => (p.daysOverdue ?? 0) > 0).length;
  // Mở kỳ dùng cấu hình đã lưu: thay đổi đang sửa dở phải lưu hoặc bỏ trước.
  const openBlocker = !period.criteria
    ? "Chưa chọn bộ tiêu chí"
    : participants.length === 0
      ? "Chưa có người được đánh giá"
      : dirty
        ? "Diện quyết định có thay đổi chưa lưu"
        : null;
  const unpublishedCount = participants.filter((p) => p.status !== "Published").length;
  const tabs: { key: PeriodTab; label: string; count?: number; warn?: boolean }[] = [
    ...(showProgress
      ? [{ key: "progress" as PeriodTab, label: "Tiến độ", count: overdueCount > 0 ? overdueCount : undefined, warn: true }]
      : []),
    ...(canManage ? [{ key: "participants" as PeriodTab, label: "Người được đánh giá", count: participants.length > 0 ? participants.length : undefined }] : []),
    ...(showReadiness
      ? [{ key: "readiness" as PeriodTab, label: "Người thực hiện các bước", count: stepIssues.length > 0 ? stepIssues.length : undefined, warn: true }]
      : []),
    { key: "criteria", label: "Bộ tiêu chí" },
    { key: "profiles", label: dirty ? "Diện quyết định •" : "Diện quyết định" },
  ];

  // Bảng tổng hợp xếp loại cán bộ của kỳ (Excel, theo phạm vi xuất báo cáo được giao).
  const downloadRatingSummary = async () => {
    setDownloading(true);
    try {
      const quarter = ["", "I", "II", "III", "IV"][period.quarter] ?? String(period.quarter);
      await reportService.downloadReport(
        withQuery("/reports/internal/rating-summary", { periodId: period.id }),
        `BangTongHop_XepLoaiCanBo_{SHORT_NAME}_Quy ${quarter}-${period.year}.xlsx`,
      );
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Tải bảng tổng hợp xếp loại không thành công."));
    } finally {
      setDownloading(false);
    }
  };

  const run = async (work: () => Promise<unknown>, success: string) => {
    setBusy(true);
    try {
      await work();
      toast.success(success);
      await load();
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Thao tác không thành công."));
      if (errorStatus(err) === 409) await load();
    } finally {
      setBusy(false);
    }
  };

  // Đổi trạng thái kỳ thì nạp lại ô chọn kỳ ở thanh bên.
  const transition = (action: Transition, reason: string | undefined, success: string) =>
    run(async () => {
      await evaluationService.transitionPeriod(period.id, action, period.version, reason);
      reloadPeriods().catch(() => undefined);
    }, success);

  /** Mở kỳ: còn cảnh báo kẹt luồng → máy chủ trả 409 kèm danh sách; hiện bảng và đề nghị mở bắt buộc (có lý do). */
  const openPeriod = async (force = false, reason?: string) => {
    setBusy(true);
    try {
      await evaluationService.transitionPeriod(period.id, "open", period.version, reason, force);
      toast.success("Đã mở kỳ đánh giá.");
      setForceOpen(false);
      reloadPeriods().catch(() => undefined);
      await load();
    } catch (err: unknown) {
      const body = err instanceof ApiError ? err.data : null;
      if (errorStatus(err) === 409 && body?.code === "PERIOD_NOT_READY") {
        setReadiness(body.data as PeriodReadinessDto);
        setTab("readiness");
        toast.error(body.message || "Chưa mở được kỳ: còn bước chưa có người thực hiện.");
        setForceOpen(true);
      } else {
        toast.error(errorMessage(err, "Mở kỳ không thành công."));
        if (errorStatus(err) === 409) await load();
      }
    } finally {
      setBusy(false);
    }
  };

  const saveSettings = () =>
    run(async () => {
      await evaluationService.updatePeriod(period.id, period.version, { settings });
      setDirtyState(false);
    }, "Đã lưu cấu hình kỳ.");

  // Tính lại lưu ngay cả phần đang sửa nên hỏi lại trước.
  const applyRules = () =>
    confirm({
      title: "Tính lại thời hạn",
      message: "Ngày hạn của các bước có quy tắc sẽ được tính lại (ngày đã sửa tay bị thay) và lưu ngay cùng các thay đổi đang sửa.",
      confirmText: "Tính lại",
      isDanger: false,
      onConfirm: () =>
        run(async () => {
          await evaluationService.updatePeriod(period.id, period.version, { settings, applyDeadlineRules: true });
          setDirtyState(false);
        }, "Đã tính lại thời hạn."),
    });

  // Bỏ phần đang sửa, quay về cấu hình đã lưu.
  const discardSettings = () => {
    setSettings(JSON.parse(JSON.stringify(period.settings)));
    setDirtyState(false);
  };

  const editSettings = (next: PeriodSettings) => {
    setSettings(next);
    setDirtyState(true);
  };

  // Đổi bộ tiêu chí thì máy chủ đặt lại bước đăng ký sản phẩm của mọi diện quyết định — lưu phần đang sửa trước.
  const chooseCriteria = (criteriaSetId: string) => {
    if (dirtyRef.current) {
      toast.error("Diện quyết định đang có thay đổi chưa lưu. Hãy lưu cấu hình trước khi đổi bộ tiêu chí.");
      return;
    }
    return run(() => evaluationService.updatePeriod(period.id, period.version, { criteriaSetId }), "Đã chọn bộ tiêu chí cho kỳ.");
  };

  // Chỉ xét diện quyết định đang có người dùng; đếm theo bước (không cộng dồn qua các diện quyết định).
  const usedProfileCodes = new Set(participants.map((p) => p.workflowProfileCode));
  const deadlineProfiles = period.settings.profiles.filter((p) => usedProfileCodes.size === 0 || usedProfileCodes.has(p.code));
  // Công bố không có hạn cố định (làm sau khi có kết quả phê duyệt) nên không đếm là thiếu.
  const missingDeadlineSteps = new Set(
    deadlineProfiles.flatMap((profile) =>
      Object.entries(profile.steps).filter(([code, step]) => code !== "B5_PUBLISH" && step.mode !== "Off" && !step.deadline).map(([code]) => code)),
  );
  const missingDeadlines = missingDeadlineSteps.size;

  const checklist: { ok: boolean; text: string; action: string; go: () => void }[] = [
    {
      ok: !!period.criteria,
      text: period.criteria ? `Bộ tiêu chí: ${period.criteria.name}` : "Chưa chọn bộ tiêu chí",
      action: "Chọn bộ tiêu chí",
      go: () => setTab("criteria"),
    },
    {
      ok: participants.length > 0,
      text: participants.length > 0 ? "Đã có danh sách người được đánh giá" : "Chưa có người được đánh giá",
      action: "Thêm người",
      go: () => setTab("participants"),
    },
    // Kết luận từ máy chủ chưa về thì chưa hiện dấu xanh.
    ...((period.criteria?.content.weightFrames.length ?? 0) > 0 && participants.length > 0 && readiness
      ? [{
          ok: frameIssues.length === 0,
          text: frameIssues.length === 0 ? "Mọi người đã có khung tỷ trọng" : `${frameIssues.length} người chưa có khung tỷ trọng hợp lệ`,
          action: "Sửa thông tin",
          go: () => setTab("participants"),
        }]
      : []),
    // Máy chủ không bắt buộc thời hạn khi mở kỳ, nhưng thiếu hạn thì không theo dõi được quá hạn.
    {
      ok: missingDeadlines === 0,
      text: missingDeadlines === 0 ? "Các bước đã có thời hạn" : `${missingDeadlines} bước chưa có thời hạn`,
      action: "Đặt thời hạn",
      go: () => setTab("profiles"),
    },
    ...(participants.length > 0 && readiness
      ? [{
          ok: stepIssues.length === 0,
          text: stepIssues.length === 0 ? "Mọi bước đều có người thực hiện" : `${stepIssues.length} bước chưa có người thực hiện`,
          action: "Xem chi tiết",
          go: () => setTab("readiness"),
        }]
      : []),
  ];

  return (
    <div className="page-wrapper">
      <PageHeader
        title={period.name}
        badge={
          <span title={period.statusReason ? `Lý do: ${period.statusReason}` : undefined}>
            <PeriodStatusBadge status={period.status} label={period.statusDisplayName} />
          </span>
        }
        actions={
          <>
            {hasPermission("report.export") && !isDraft && (
              <button type="button" className="btn btn-outline-secondary" disabled={downloading} onClick={downloadRatingSummary}>
                <i className="bi bi-download me-1" />
                {downloading ? "Đang tải..." : "Bảng tổng hợp xếp loại"}
              </button>
            )}
            {canManage && isDraft && (
              <button type="button" className="btn btn-primary" disabled={busy || openBlocker !== null}
                title={openBlocker ?? undefined}
                onClick={() => confirm({ title: "Mở kỳ đánh giá", message: "Sau khi mở kỳ chỉ sửa được thời hạn các bước; không đổi được diện quyết định và bộ tiêu chí.", confirmText: "Mở kỳ", isDanger: false, onConfirm: () => openPeriod() })}>
                <i className="bi bi-play-fill me-1" />Mở kỳ
              </button>
            )}
            {canManage && !isDraft && period.status !== "Closed" && (
              <HeaderMenu
                  itemLabel={period.name}
                  disabled={busy}
                  actions={[
                    { key: "unlock", label: "Mở lại kỳ", icon: "bi-unlock", hidden: period.status !== "Locked", onClick: () => setUnlockOpen(true) },
                    {
                      key: "lock", label: "Khóa dữ liệu", icon: "bi-lock", danger: true, hidden: period.status !== "Open",
                      onClick: () => confirm({
                        title: "Khóa dữ liệu kỳ",
                        message: "Chủ hồ sơ sẽ không sửa được hồ sơ nữa; chỉ các bước từ thẩm định trở đi tiếp tục. Muốn mở lại phải ghi lý do.",
                        confirmText: "Khóa dữ liệu",
                        isDanger: true,
                        onConfirm: () => transition("lock", undefined, "Đã khóa dữ liệu kỳ."),
                      }),
                    },
                    {
                      key: "close", label: "Đóng kỳ", icon: "bi-x-octagon", danger: true,
                      disabled: unpublishedCount > 0,
                      title: unpublishedCount > 0 ? `Còn ${unpublishedCount} hồ sơ chưa công bố` : undefined,
                      onClick: () => confirm({
                        title: "Đóng kỳ",
                        message: "Sau khi đóng, kỳ chỉ còn để xem: không đổi được thời hạn, người được đánh giá hay trạng thái kỳ. Muốn sửa kết quả phải mở lại từng hồ sơ để đính chính.",
                        confirmText: "Đóng kỳ",
                        isDanger: true,
                        onConfirm: () => transition("close", undefined, "Đã đóng kỳ."),
                      }),
                    },
                  ]}
                />
            )}
          </>
        }
      />

      <div className="page-body">
        <div className="d-flex flex-column gap-3">
        {canManage && isDraft && (
          <section className="card">
            <div className="card-header"><h2 className="mb-0">Chuẩn bị mở kỳ</h2></div>
            <ul className="list-group list-group-flush">
              {checklist.map((item) => (
                <li key={item.action} className="list-group-item d-flex align-items-center gap-2">
                  <i className={`bi ${item.ok ? "bi-check-circle-fill text-success" : "bi-exclamation-circle-fill text-warning"}`} />
                  <span className="flex-grow-1">{item.text}</span>
                  {!item.ok && <button type="button" className="btn btn-outline-primary btn-sm" onClick={item.go}>{item.action}</button>}
                </li>
              ))}
              {(readiness?.dataReview ?? []).map((item) => (
                <li key={item.kind} className="list-group-item">
                  <details>
                    <summary className="d-flex align-items-center gap-2" style={{ cursor: "pointer" }}>
                      <i className="bi bi-info-circle-fill text-info" />
                      <span className="flex-grow-1">Kiểm tra dữ liệu: {item.title}</span>
                      <span className="small text-secondary">Xem</span>
                    </summary>
                    <ul className="small mt-2 mb-1">
                      {item.names.map((name) => <li key={name}>{name}</li>)}
                    </ul>
                  </details>
                </li>
              ))}
            </ul>
          </section>
        )}
        <ul className={`nav nav-tabs ${styles.tabs}`} role="tablist">
          {tabs.map((t) => (
            <li className="nav-item" key={t.key} role="presentation">
              <button
                type="button"
                role="tab"
                aria-selected={tab === t.key}
                className={`nav-link ${tab === t.key ? "active" : ""}`}
                onClick={() => setTab(t.key)}
              >
                {t.label}
                {t.count != null && (
                  <span className={t.warn ? "tag t-bad ms-2 num" : "text-secondary fw-medium ms-2 num"}>{t.count}</span>
                )}
              </button>
            </li>
          ))}
        </ul>

        {tab === "progress" && showProgress && <PeriodProgressSection participants={participants} />}

        {tab === "profiles" && (
        <section className="card">
          <div className="card-body">
            <WorkflowProfilesEditor
              settings={settings}
              onChange={editSettings}
              editable={editable}
              deadlinesEditable={deadlinesEditable}
              permissionOptions={permissionOptions}
              savedCodes={period.settings.profiles.map((p) => p.code)}
              onApplyRules={applyRules}
            />

            <div className="form-check mt-2">
              <input className="form-check-input" type="checkbox" id="enforce" checked={settings.enforceDeadlines} disabled={!editable}
                onChange={(e) => editSettings({ ...settings, enforceDeadlines: e.target.checked })} />
              <label className="form-check-label" htmlFor="enforce">Chặn hoàn thành bước khi đã quá thời hạn</label>
            </div>
          </div>
          {canManage && period.status !== "Closed" && (
            <div className={`card-footer ${styles.formFooter}`}>
              {dirty && <span className="text-secondary small me-auto">Có thay đổi chưa lưu</span>}
              <button type="button" className="btn btn-outline-secondary" disabled={busy || !dirty} onClick={discardSettings}>Hủy</button>
              <button type="button" className="btn btn-primary" disabled={busy || !dirty} onClick={saveSettings}>Lưu</button>
            </div>
          )}
        </section>
        )}

        {tab === "criteria" && <CriteriaSection period={period} editable={editable} busy={busy} onChoose={chooseCriteria} />}

        {tab === "readiness" && showReadiness && (
          <ReadinessPanel readiness={readiness} loading={checking} onCheck={checkReadiness} />
        )}

        {tab === "participants" && canManage && (
          <ParticipantsSection
            period={period}
            profiles={period.settings.profiles}
            participants={participants}
            departments={departments}
            cells={cells}
            busy={busy}
            run={run}
          />
        )}
        </div>
      </div>

      <ReasonDialog
        isOpen={unlockOpen}
        title="Mở lại kỳ"
        description="Chủ hồ sơ sẽ sửa được hồ sơ trở lại."
        confirmText="Mở lại kỳ"
        busy={busy}
        onCancel={() => setUnlockOpen(false)}
        onConfirm={async (reason) => {
          setUnlockOpen(false);
          await transition("unlock", reason, "Đã mở lại kỳ.");
        }}
      />

      <ReasonDialog
        isOpen={forceOpen}
        title="Mở kỳ bắt buộc"
        description={`Còn ${readiness?.issues.length ?? 0} bước chưa có người thực hiện. Chỉ mở bắt buộc khi sẽ bổ sung người thực hiện kịp thời.`}
        confirmText="Mở kỳ bắt buộc"
        busy={busy}
        onCancel={() => setForceOpen(false)}
        onConfirm={(reason) => openPeriod(true, reason)}
      />
    </div>
  );
}

interface ParticipantsProps {
  period: EvaluationPeriodDto;
  profiles: WorkflowProfile[];
  participants: PeriodParticipantDto[];
  departments: DepartmentItem[];
  cells: BranchItem[];
  busy: boolean;
  run: (work: () => Promise<unknown>, success: string) => Promise<void>;
}

function ParticipantsSection({ period, profiles, participants, departments, cells, busy, run }: ParticipantsProps) {
  const { confirm } = useToast();
  const canAdd = period.status === "Draft" || period.status === "Open";
  const canChangeProfile = period.status !== "Closed";
  const [adding, setAdding] = useState(period.status === "Draft" && participants.length === 0);
  const [departmentId, setDepartmentId] = useState("");
  const [cellId, setCellId] = useState("");
  const [query, setQuery] = useState("");
  const [addProfile, setAddProfile] = useState("");
  const [candidates, setCandidates] = useState<ParticipantCandidateDto[] | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [editing, setEditing] = useState<PeriodParticipantDto | null>(null);
  const [profileTargets, setProfileTargets] = useState<PeriodParticipantDto[] | null>(null);
  const [checked, setChecked] = useState<string[]>([]);
  // Danh sách người không thêm/đổi được cần đọc kỹ nên hiện trong hộp thoại thay vì toast.
  const [skipped, setSkipped] = useState<{ title: string; items: string[] } | null>(null);

  const search = async () => {
    try {
      setCandidates(await evaluationService.getCandidates(period.id, { departmentId, partyCellId: cellId, q: query }));
      setSelected([]);
    } catch {
      setCandidates([]);
    }
  };

  // Tìm ngay khi mở hoặc đổi bộ lọc; gõ tên thì chờ một chút cho đỡ gọi liên tục.
  useEffect(() => {
    if (!canAdd || !adding) return;
    const timer = setTimeout(() => { search(); }, query ? 300 : 0);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canAdd, adding, departmentId, cellId, query]);

  const selectable = (candidates ?? []).filter((c) => !c.alreadyAdded);
  const allSelectable = selectable.length > 0 && selected.length === selectable.length;
  // Người chưa có trong kỳ lên trước, người đã có xuống cuối.
  const orderedCandidates = [...(candidates ?? [])].sort((x, y) => Number(x.alreadyAdded) - Number(y.alreadyAdded));

  const addByUnit = () =>
    run(async () => {
      const result = await evaluationService.addParticipants(period.id, { departmentId, partyCellId: cellId, workflowProfileCode: addProfile });
      if (result.skipped.length > 0) setSkipped({ title: `Đã thêm ${result.added} người. Một số người không được thêm:`, items: result.skipped });
    }, "Đã thêm người được đánh giá theo đơn vị.");

  const addSelected = () =>
    run(async () => {
      const result = await evaluationService.addParticipants(period.id, { memberIds: selected, workflowProfileCode: addProfile });
      if (result.skipped.length > 0) setSkipped({ title: `Đã thêm ${result.added} người. Một số người không được thêm:`, items: result.skipped });
      setSelected([]);
      await search();
    }, `Đã thêm ${selected.length} người được đánh giá.`);

  const remove = (p: PeriodParticipantDto) =>
    confirm({
      title: "Bỏ khỏi kỳ",
      message: `Bỏ ${p.fullName} khỏi danh sách người được đánh giá? Hồ sơ đánh giá của người này trong kỳ sẽ bị xóa.`,
      confirmText: "Bỏ khỏi kỳ",
      isDanger: true,
      onConfirm: () => run(() => evaluationService.removeParticipant(period.id, p.recordId, p.version), "Đã bỏ khỏi danh sách."),
    });

  const allChecked = participants.length > 0 && checked.length === participants.length;
  const paging = usePagination(participants, { storageKey: "period.participants" });
  const showToolbar = (canAdd && !adding) || (canChangeProfile && checked.length > 0);

  return (
    <section className="card">
      {showToolbar && (
        <div className={`card-body ${styles.toolbar}`}>
          {canChangeProfile && checked.length > 0 ? (
            <>
              <span className="text-secondary">Đã chọn <span className="num">{checked.length}</span> người</span>
              <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy}
                onClick={() => setProfileTargets(participants.filter((p) => checked.includes(p.recordId)))}>
                Đổi diện quyết định
              </button>
              <button type="button" className="btn btn-link btn-sm" onClick={() => setChecked([])}>Bỏ chọn</button>
            </>
          ) : null}
          {canAdd && !adding && (
            <div className={styles.toolbarEnd}>
              <button type="button" className="btn btn-primary btn-sm" aria-expanded={false} onClick={() => setAdding(true)}>
                <i className="bi bi-plus-lg me-1" />Thêm người
              </button>
            </div>
          )}
        </div>
      )}

      {canAdd && adding && (
        <div className={`card-body ${styles.addPanel}`}>
          <div className="d-flex justify-content-between align-items-center mb-2">
            <h2 className="h6 mb-0">Thêm người được đánh giá</h2>
            <button type="button" className="btn-close" aria-label="Đóng thêm người" title="Đóng" onClick={() => setAdding(false)} />
          </div>
          <div className="row g-2 align-items-end">
            <div className="col-12 col-md-4"><label className="form-label" htmlFor="add-dept">Đơn vị</label>
              <select id="add-dept" className="form-select form-select-sm" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
                <option value="">Tất cả</option>
                {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
            </div>
            <div className="col-6 col-md-4"><label className="form-label" htmlFor="add-cell">Chi bộ</label>
              <select id="add-cell" className="form-select form-select-sm" value={cellId} onChange={(e) => setCellId(e.target.value)}>
                <option value="">Tất cả</option>
                {cells.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
            <div className="col-6 col-md-4"><label className="form-label" htmlFor="add-q">Tên / tài khoản</label>
              <input id="add-q" className="form-control form-control-sm" value={query} onChange={(e) => setQuery(e.target.value)} />
            </div>
          </div>
          <div className="row g-2 align-items-end mt-1">
            <div className="col-12 col-md-4"><label className="form-label" htmlFor="add-profile">Diện quyết định</label>
              <select id="add-profile" className="form-select form-select-sm" value={addProfile} onChange={(e) => setAddProfile(e.target.value)}>
                <option value="">Theo cấp quyết định</option>
                {profiles.map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}
              </select>
            </div>
            <div className="col-12 col-md-8 d-flex flex-wrap gap-2 justify-content-md-end">
              <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy || (!departmentId && !cellId)} onClick={addByUnit}>
                Thêm cả đơn vị
              </button>
            </div>
          </div>
          {candidates !== null && (
            <div className="mt-3">
              {candidates.length === 0 ? (
                <div className="text-secondary small">Không tìm thấy cán bộ phù hợp.</div>
              ) : (
                <>
                  <div className="d-flex justify-content-between align-items-center mb-2 gap-2">
                    <span className="text-secondary small">
                      <span className="num">{candidates.length}</span> cán bộ
                      {selected.length > 0 && <> · đã chọn <span className="num">{selected.length}</span></>}
                    </span>
                    <button type="button" className="btn btn-primary btn-sm" disabled={busy || selected.length === 0} onClick={addSelected}>
                      <i className="bi bi-plus-lg me-1" />Thêm <span className="num">{selected.length}</span> người
                    </button>
                  </div>
                  <div className={styles.candidates}>
                    <table className="table table-sm align-middle mb-0">
                      <thead>
                        <tr>
                          <th style={{ width: 28 }}>
                            <input type="checkbox" className="form-check-input" aria-label="Chọn tất cả cán bộ" disabled={selectable.length === 0}
                              checked={allSelectable} onChange={(e) => setSelected(e.target.checked ? selectable.map((c) => c.memberId) : [])} />
                          </th>
                          <th>Cán bộ</th>
                          <th>Chức vụ</th>
                          <th>Đơn vị</th>
                          <th>Chi bộ</th>
                        </tr>
                      </thead>
                      <tbody>
                        {orderedCandidates.map((c) => (
                          <tr key={c.memberId} className={c.alreadyAdded ? "text-secondary" : undefined}>
                            <td>
                              <input className="form-check-input" type="checkbox" id={`cand-${c.memberId}`} aria-label={c.fullName} disabled={c.alreadyAdded}
                                checked={selected.includes(c.memberId)}
                                onChange={(e) => setSelected(e.target.checked ? [...selected, c.memberId] : selected.filter((id) => id !== c.memberId))} />
                            </td>
                            <td className="text-nowrap">
                              <label htmlFor={`cand-${c.memberId}`} className="mb-0">{c.fullName}</label>
                              {c.alreadyAdded && <span className="tag t-wait ms-2">Đã có</span>}
                              <div className="small text-secondary">{c.username}</div>
                            </td>
                            <td>{c.positionName || "—"}</td>
                            <td>{c.departmentName || "—"}</td>
                            <td>{c.partyCellName || "—"}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </>
              )}
            </div>
          )}
        </div>
      )}

      {participants.length === 0 ? (
        <div className="card-body text-secondary border-top">Chưa có người được đánh giá.</div>
      ) : (
        <>
        <div className="table-responsive">
          <table className={`table table-hover align-middle mb-0 ${styles.compactTable}`}>
            <thead>
              <tr>
                {canChangeProfile && (
                  <th style={{ width: 28 }}>
                    <input type="checkbox" className="form-check-input" aria-label="Chọn tất cả" checked={allChecked}
                      onChange={(e) => setChecked(e.target.checked ? participants.map((p) => p.recordId) : [])} />
                  </th>
                )}
                <th>Cán bộ</th>
                <th className="d-none d-md-table-cell">Đơn vị</th>
                <th className="d-none d-lg-table-cell" title="Khung tỷ trọng">Khung</th>
                <th className="d-none d-lg-table-cell">Cấp quyết định</th>
                <th className="d-none d-md-table-cell">Diện quyết định</th>
                <th className="d-none d-md-table-cell">Trạng thái</th>
                {canChangeProfile && <th className="text-end">Thao tác</th>}
              </tr>
            </thead>
            <tbody>
              {paging.pageItems.map((p) => (
                <tr key={p.recordId}>
                  {canChangeProfile && (
                    <td>
                      <input type="checkbox" className="form-check-input" aria-label={`Chọn ${p.fullName}`} checked={checked.includes(p.recordId)}
                        onChange={(e) => setChecked(e.target.checked ? [...checked, p.recordId] : checked.filter((id) => id !== p.recordId))} />
                    </td>
                  )}
                  <td className={styles.nameCell}><Link href={`/evaluations/${p.recordId}`} className="fw-semibold">{p.fullName}</Link><div className="small text-secondary">{p.username}</div>
                    <div className="d-md-none mt-1"><RecordStatusBadge status={p.status} label={p.statusDisplayName} /></div>
                  </td>
                  <td className="d-none d-md-table-cell">{p.departmentName || "—"}<div className={styles.unitSub}>{p.partyCellName || "—"}</div></td>
                  <td className="d-none d-lg-table-cell" title={p.weightFrameName || undefined}>{p.weightFrameCode || "—"}{p.weightFrameCode && !p.weightFrameName && period.criteria && <i className="bi bi-exclamation-triangle text-danger ms-1" title="Khung không có trong bộ tiêu chí của kỳ" />}</td>
                  <td className="d-none d-lg-table-cell text-nowrap">{p.approvalAuthority === "CapTren" ? "Cấp trên" : "Cơ sở"}</td>
                  <td className="d-none d-md-table-cell">{p.workflowProfileName || p.workflowProfileCode}</td>
                  <td className="d-none d-md-table-cell"><RecordStatusBadge status={p.status} label={p.statusDisplayName} /></td>
                  {canChangeProfile && (
                    <td className="text-end">
                      <RowActions
                        itemLabel={p.fullName}
                        disabled={busy}
                        primary={canAdd
                          ? [{ key: "edit", label: "Sửa", icon: "bi-pencil", title: "Sửa thông tin", onClick: () => setEditing(p) }]
                          : [{ key: "profile", label: "Đổi diện quyết định", icon: "bi-diagram-3", onClick: () => setProfileTargets([p]) }]}
                        menu={canAdd ? [
                          { key: "profile", label: "Đổi diện quyết định", icon: "bi-diagram-3", onClick: () => setProfileTargets([p]) },
                          { key: "remove", label: "Bỏ khỏi kỳ", icon: "bi-trash", danger: true, onClick: () => remove(p) },
                        ] : []}
                      />
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Pagination
          page={paging.page}
          pageSize={paging.pageSize}
          total={paging.total}
          onPageChange={paging.setPage}
          onPageSizeChange={paging.setPageSize}
        />
        </>
      )}

      {editing && (
        <SnapshotDialog
          participant={editing}
          frames={period.criteria?.content.weightFrames ?? []}
          departments={departments}
          cells={cells}
          busy={busy}
          onCancel={() => setEditing(null)}
          onSave={(payload) =>
            run(async () => {
              await evaluationService.updateSnapshot(period.id, editing.recordId, { version: editing.version, ...payload });
              setEditing(null);
            }, "Đã sửa thông tin.")
          }
        />
      )}

      {profileTargets && (
        <ProfileDialog
          targets={profileTargets}
          profiles={profiles}
          busy={busy}
          onCancel={() => setProfileTargets(null)}
          onSave={(workflowProfileCode, reason) =>
            run(async () => {
              if (profileTargets.length === 1) {
                const target = profileTargets[0];
                await evaluationService.changeProfile(period.id, target.recordId, { version: target.version, workflowProfileCode, reason });
              } else {
                const result = await evaluationService.bulkChangeProfile(period.id, {
                  items: profileTargets.map((t) => ({ recordId: t.recordId, version: t.version })),
                  workflowProfileCode,
                  reason,
                });
                if (result.skipped.length > 0) setSkipped({ title: `Đã đổi ${result.updated} hồ sơ. Một số hồ sơ không đổi được:`, items: result.skipped });
              }
              setProfileTargets(null);
              setChecked([]);
            }, "Đã đổi diện quyết định.")
          }
        />
      )}

      {skipped && (
        <AdminModal
          title="Chưa xử lý hết"
          onClose={() => setSkipped(null)}
          footer={<button type="button" className="btn btn-primary btn-sm" onClick={() => setSkipped(null)}>Đóng</button>}
        >
          <p className="mb-2">{skipped.title}</p>
          <ul className="small mb-0">
            {skipped.items.map((item) => <li key={item}>{item}</li>)}
          </ul>
        </AdminModal>
      )}
    </section>
  );
}

interface ProfileDialogProps {
  targets: PeriodParticipantDto[];
  profiles: WorkflowProfile[];
  busy: boolean;
  onCancel: () => void;
  onSave: (workflowProfileCode: string, reason: string) => void;
}

/** Đổi diện quyết định (một hoặc nhiều người): bắt buộc lý do; máy chủ chỉ đổi hồ sơ chưa qua bước bị ảnh hưởng. */
function ProfileDialog({ targets, profiles, busy, onCancel, onSave }: ProfileDialogProps) {
  const [code, setCode] = useState(targets.length === 1 ? targets[0].workflowProfileCode : "");
  const [reason, setReason] = useState("");
  return (
    <AdminModal
      title={`Đổi diện quyết định — ${targets.length === 1 ? targets[0].fullName : `${targets.length} người`}`}
      onClose={onCancel}
      footer={
        <>
          <button type="button" className="btn btn-outline-secondary btn-sm" onClick={onCancel}>Hủy</button>
          <button type="submit" form="profile-dialog-form" className="btn btn-primary btn-sm" disabled={busy || !code || !reason.trim()}>Đổi diện quyết định</button>
        </>
      }
    >
      <form id="profile-dialog-form" className="d-flex flex-column gap-3" onSubmit={(e) => { e.preventDefault(); onSave(code, reason.trim()); }}>
        <div><label className="form-label" htmlFor="profile-target">Diện quyết định mới</label>
          <select id="profile-target" className="form-select" required value={code} onChange={(e) => setCode(e.target.value)}>
            <option value="">— Chọn —</option>
            {profiles.map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}
          </select>
        </div>
        <div><label className="form-label" htmlFor="profile-reason">Lý do (bắt buộc)</label>
          <textarea id="profile-reason" className="form-control" rows={3} maxLength={2000} required value={reason} onChange={(e) => setReason(e.target.value)} />
        </div>
      </form>
    </AdminModal>
  );
}

interface SnapshotProps {
  participant: PeriodParticipantDto;
  departments: DepartmentItem[];
  cells: BranchItem[];
  busy: boolean;
  onCancel: () => void;
  frames: WeightFrame[];
  onSave: (payload: { departmentId: string | null; partyCellId: string | null; weightFrameCode: string; approvalAuthority: string; reason: string }) => void;
}

function SnapshotDialog({ participant, frames, departments, cells, busy, onCancel, onSave }: SnapshotProps) {
  const [departmentId, setDepartmentId] = useState(participant.departmentId || "");
  const [cellId, setCellId] = useState(participant.partyCellId || "");
  const [weightFrameCode, setWeightFrameCode] = useState(participant.weightFrameCode);
  const [authority, setAuthority] = useState(participant.approvalAuthority);
  const [reason, setReason] = useState("");
  return (
    <AdminModal
      title={`Sửa thông tin — ${participant.fullName}`}
      onClose={onCancel}
      footer={
        <>
          <button type="button" className="btn btn-outline-secondary btn-sm" onClick={onCancel}>Hủy</button>
          <button type="submit" form="snapshot-dialog-form" className="btn btn-primary btn-sm" disabled={busy || !reason.trim()}>Lưu</button>
        </>
      }
    >
      <form id="snapshot-dialog-form" className="row g-3" onSubmit={(e) => { e.preventDefault(); onSave({ departmentId: departmentId || null, partyCellId: cellId || null, weightFrameCode, approvalAuthority: authority, reason }); }}>
        <div className="col-6"><label className="form-label" htmlFor="snap-dept">Phòng</label>
          <select id="snap-dept" className="form-select" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
            <option value="">— Không —</option>
            {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        </div>
        <div className="col-6"><label className="form-label" htmlFor="snap-cell">Chi bộ</label>
          <select id="snap-cell" className="form-select" value={cellId} onChange={(e) => setCellId(e.target.value)}>
            <option value="">— Không —</option>
            {cells.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </div>
        <div className="col-6"><label className="form-label" htmlFor="snap-frame">Khung tỷ trọng A-B-C-D</label>
          <select id="snap-frame" className="form-select" value={weightFrameCode} disabled={!!participant.weightFrameLocked} onChange={(e) => setWeightFrameCode(e.target.value)}>
            {!frames.some((f) => f.code === weightFrameCode) && <option value={weightFrameCode}>{weightFrameCode || "— Chưa có —"} (không có trong bộ)</option>}
            {frames.map((f) => <option key={f.code} value={f.code}>{f.code} — {f.name}</option>)}
          </select>
          {participant.weightFrameLocked && (
            <div className="form-text">Danh mục đã duyệt — trả hồ sơ về bước tự chấm trước khi đổi khung tỷ trọng.</div>
          )}
        </div>
        <div className="col-6"><label className="form-label" htmlFor="snap-authority">Cấp quyết định</label>
          <select id="snap-authority" className="form-select" value={authority} onChange={(e) => setAuthority(e.target.value)}>
            <option value="CoSo">Đảng ủy cơ sở</option>
            <option value="CapTren">Cấp trên</option>
          </select>
        </div>
        <div className="col-12"><label className="form-label" htmlFor="snap-reason">Lý do (bắt buộc)</label><textarea id="snap-reason" className="form-control" rows={3} required value={reason} onChange={(e) => setReason(e.target.value)} /></div>
      </form>
    </AdminModal>
  );
}

interface CriteriaSectionProps {
  period: EvaluationPeriodDto;
  editable: boolean;
  busy: boolean;
  onChoose: (criteriaSetId: string) => void;
}

/** Bộ tiêu chí của kỳ: chọn bộ đã xuất bản khi kỳ còn dự thảo; xem ảnh chụp (bất biến sau khi mở kỳ). */
function CriteriaSection({ period, editable, busy, onChoose }: CriteriaSectionProps) {
  const [sets, setSets] = useState<CriteriaSetListItem[]>([]);
  const [choice, setChoice] = useState(period.criteriaSetId || "");
  const [expanded, setExpanded] = useState(false);
  const criteria = period.criteria;

  useEffect(() => {
    if (editable) criteriaService.list().then((list) => setSets(list.filter((s) => s.status === "Published"))).catch(() => setSets([]));
  }, [editable]);

  useEffect(() => setChoice(period.criteriaSetId || ""), [period.criteriaSetId]);

  return (
    <section className="card">
      <div className="card-body">
        {criteria ? (
          <div className={`d-flex flex-wrap align-items-center gap-2 ${editable || expanded ? "mb-3" : ""}`}>
            <strong>{criteria.name}</strong>
            <span className="text-secondary">{criteria.code}</span>
            <span className="text-secondary small num">
              {period.status === "Draft" ? "Chọn lúc" : "Chụp vào kỳ lúc"} {formatDateTime(criteria.takenAt)}
            </span>
            <span className="ms-auto d-flex align-items-center gap-2">
              <button type="button" className="btn btn-outline-secondary btn-sm" aria-expanded={expanded} onClick={() => setExpanded(!expanded)}>
                <i className={`bi ${expanded ? "bi-chevron-up" : "bi-chevron-down"} me-1`} />{expanded ? "Ẩn nội dung" : "Xem nội dung"}
              </button>
              <Link href="/criteria" className="btn btn-outline-secondary btn-sm">Quản lý bộ tiêu chí</Link>
            </span>
          </div>
        ) : (
          <div className="alert alert-warning py-2">Kỳ chưa chọn bộ tiêu chí — chưa mở được kỳ. <Link href="/criteria">Quản lý bộ tiêu chí</Link></div>
        )}
        {editable && (
          <div className="d-flex gap-2 align-items-end flex-wrap mb-2">
            <div className="flex-grow-1" style={{ maxWidth: 420 }}>
              <label className="form-label" htmlFor="period-criteria-set">Chọn bộ đã xuất bản</label>
              <select id="period-criteria-set" className="form-select form-select-sm" value={choice} onChange={(e) => setChoice(e.target.value)}>
                <option value="">— Chọn —</option>
                {sets.map((s) => <option key={s.id} value={s.id}>{s.name} ({s.code})</option>)}
              </select>
            </div>
            <button type="button" className="btn btn-outline-primary btn-sm" disabled={busy || !choice || choice === period.criteriaSetId} onClick={() => onChoose(choice)}>
              Dùng bộ này
            </button>
          </div>
        )}
        {criteria && expanded && <CriteriaSummary content={criteria.content} />}
      </div>
    </section>
  );
}
