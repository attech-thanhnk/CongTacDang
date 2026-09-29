"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { useAuth } from "@/contexts/AuthContext";
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
import { CriteriaSetListItem, WeightFrame, criteriaService } from "@/services/criteriaService";

type Transition = "open" | "lock" | "unlock" | "close";

/**
 * Cấu hình kỳ: trạng thái kỳ, hồ sơ luồng (bước × chế độ × quyền thực hiện × thời hạn), bộ tiêu chí (chọn khi dự thảo, xem
 * ảnh chụp), kiểm tra kẹt luồng, danh sách người được đánh giá, hồ sơ luồng và khung tỷ trọng của từng người.
 */
export default function PeriodDetailPage() {
  const params = useParams<{ periodId: string }>();
  const periodId = params?.periodId;
  const { hasPermission } = useAuth();
  const canManage = hasPermission("period.manage");
  const { toast, confirm } = useToast();
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
  const [forceOpen, setForceOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);

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
      setSettings(JSON.parse(JSON.stringify(loaded.settings)));
      if (canManage) {
        setParticipants(await evaluationService.getParticipants(periodId));
        if (loaded.status === "Draft" || loaded.status === "Open" || loaded.status === "Locked") await checkReadiness();
      }
    } catch (err: any) {
      setError(err?.message || "Không tải được kỳ đánh giá.");
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

  if (error) return <div className="page-wrapper"><div className="alert alert-danger m-4">{error}</div></div>;
  if (!period || !settings) return <div className="page-wrapper"><div className="text-secondary p-4"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div></div>;

  const isDraft = period.status === "Draft";
  const editable = canManage && isDraft;
  const deadlinesEditable = canManage && period.status !== "Closed";

  const run = async (work: () => Promise<unknown>, success: string) => {
    setBusy(true);
    try {
      await work();
      toast.success(success);
      await load();
    } catch (err: any) {
      toast.error(err?.message || "Không thực hiện được thao tác.");
      if (err?.status === 409) await load();
    } finally {
      setBusy(false);
    }
  };

  const transition = (action: Transition, reason?: string) =>
    run(() => evaluationService.transitionPeriod(period.id, action, period.version, reason), "Đã cập nhật trạng thái kỳ.");

  /** Mở kỳ: còn cảnh báo kẹt luồng → máy chủ trả 409 kèm danh sách; hiện bảng và đề nghị mở bắt buộc (có lý do). */
  const openPeriod = async (force = false, reason?: string) => {
    setBusy(true);
    try {
      await evaluationService.transitionPeriod(period.id, "open", period.version, reason, force);
      toast.success("Đã mở kỳ đánh giá.");
      setForceOpen(false);
      await load();
    } catch (err: any) {
      const body = err instanceof ApiError ? err.data : null;
      if (err?.status === 409 && body?.code === "PERIOD_NOT_READY") {
        setReadiness(body.data as PeriodReadinessDto);
        toast.error(body.message || "Chưa mở được kỳ: còn cảnh báo kẹt luồng.");
        setForceOpen(true);
      } else {
        toast.error(err?.message || "Không mở được kỳ.");
        if (err?.status === 409) await load();
      }
    } finally {
      setBusy(false);
    }
  };

  const saveSettings = () =>
    run(() => evaluationService.updatePeriod(period.id, period.version, { settings }), "Đã lưu cấu hình kỳ.");

  const chooseCriteria = (criteriaSetId: string) =>
    run(() => evaluationService.updatePeriod(period.id, period.version, { criteriaSetId }), "Đã chọn bộ tiêu chí cho kỳ.");

  return (
    <div className="page-wrapper">
      <PageHeader
        title={period.name}
        subTitle={`Quý ${period.quarter}/${period.year} · ${period.totalRecords} người được đánh giá${period.statusReason ? ` · Lý do chuyển trạng thái gần nhất: ${period.statusReason}` : ""}`}
        badge={<span className="badge text-bg-primary">{period.statusDisplayName}</span>}
        actions={
          <div className="d-flex gap-2 flex-wrap">
            <Link href="/periods" className="btn btn-outline-secondary btn-sm"><i className="bi bi-arrow-left me-1" />Danh sách kỳ</Link>
            {canManage && period.status === "Draft" && (
              <button type="button" className="btn btn-success btn-sm" disabled={busy}
                onClick={() => confirm({ title: "Mở kỳ đánh giá", message: "Hệ thống kiểm tra kẹt luồng trước khi mở. Sau khi mở, chỉ sửa được thời hạn các bước; hồ sơ luồng, chế độ và quyền thực hiện bước bị khóa; bộ tiêu chí được chụp nguyên vào kỳ và không đổi được nữa.", confirmText: "Mở kỳ", onConfirm: () => openPeriod() })}>
                Mở kỳ
              </button>
            )}
            {canManage && period.status === "Open" && (
              <button type="button" className="btn btn-warning btn-sm" disabled={busy}
                onClick={() => confirm({ title: "Khóa dữ liệu", message: "Chủ hồ sơ sẽ không sửa được; chỉ các bước từ thẩm định trở đi được thao tác.", confirmText: "Khóa dữ liệu", onConfirm: () => transition("lock") })}>
                Khóa dữ liệu
              </button>
            )}
            {canManage && period.status === "Locked" && (
              <button type="button" className="btn btn-outline-warning btn-sm" disabled={busy} onClick={() => setUnlockOpen(true)}>Mở lại kỳ</button>
            )}
            {canManage && (period.status === "Open" || period.status === "Locked") && (
              <button type="button" className="btn btn-outline-danger btn-sm" disabled={busy}
                onClick={() => confirm({ title: "Đóng kỳ", message: "Chỉ đóng được khi mọi hồ sơ đã công bố. Sau khi đóng chỉ còn mở lại hồ sơ để đính chính.", confirmText: "Đóng kỳ", isDanger: true, onConfirm: () => transition("close") })}>
                Đóng kỳ
              </button>
            )}
          </div>
        }
      />

      <div className="page-body d-flex flex-column gap-3">
        <section className="card border-0 shadow-sm">
          <div className="card-body">
            <div className="d-flex justify-content-between align-items-center mb-2">
              <h2 className="h6 mb-0">Hồ sơ luồng — các bước theo nhóm đối tượng</h2>
              {canManage && period.status !== "Closed" && (
                <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={saveSettings}>Lưu cấu hình</button>
              )}
            </div>
            <div className="small text-secondary mb-2">
              Mỗi người được đánh giá đi theo một hồ sơ luồng (mặc định theo cấp quyết định, đổi được từng người).
              Cấu hình dựng sẵn theo Phụ lục III Hướng dẫn 03 là <strong>đề xuất chờ nghiệp vụ xác nhận</strong>.
              {!isDraft && " Kỳ đã mở: chỉ sửa được thời hạn các bước."}
            </div>
            <WorkflowProfilesEditor
              settings={settings}
              onChange={setSettings}
              editable={editable}
              deadlinesEditable={deadlinesEditable}
              permissionOptions={permissionOptions}
              savedCodes={period.settings.profiles.map((p) => p.code)}
            />

            <div className="form-check mt-2">
              <input className="form-check-input" type="checkbox" id="enforce" checked={settings.enforceDeadlines} disabled={!editable}
                onChange={(e) => setSettings({ ...settings, enforceDeadlines: e.target.checked })} />
              <label className="form-check-label small" htmlFor="enforce">Chặn hoàn thành bước khi đã quá thời hạn (mặc định chỉ cảnh báo)</label>
            </div>
          </div>
        </section>

        <CriteriaSection period={period} editable={editable} busy={busy} onChoose={chooseCriteria} />

        {canManage && period.status !== "Closed" && (
          <ReadinessPanel readiness={readiness} loading={checking} onCheck={checkReadiness} />
        )}

        {canManage && (
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

      <ReasonDialog
        isOpen={unlockOpen}
        title="Mở lại kỳ (Khóa dữ liệu → Đang mở)"
        description="Chủ hồ sơ sẽ sửa được hồ sơ trở lại. Lý do được lưu cùng kỳ."
        confirmText="Mở lại kỳ"
        busy={busy}
        onCancel={() => setUnlockOpen(false)}
        onConfirm={async (reason) => {
          setUnlockOpen(false);
          await transition("unlock", reason);
        }}
      />

      <ReasonDialog
        isOpen={forceOpen}
        title="Mở kỳ bắt buộc"
        description={`Còn ${readiness?.issues.length ?? 0} cảnh báo kẹt luồng (xem bảng "Kiểm tra kẹt luồng"). Chỉ mở bắt buộc khi chắc chắn sẽ bổ sung người thực hiện kịp thời; lý do được lưu cùng kỳ.`}
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
  const canAdd = period.status === "Draft" || period.status === "Open";
  const canChangeProfile = period.status !== "Closed";
  const [departmentId, setDepartmentId] = useState("");
  const [cellId, setCellId] = useState("");
  const [query, setQuery] = useState("");
  const [addProfile, setAddProfile] = useState("");
  const [candidates, setCandidates] = useState<ParticipantCandidateDto[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [editing, setEditing] = useState<PeriodParticipantDto | null>(null);
  const [profileTargets, setProfileTargets] = useState<PeriodParticipantDto[] | null>(null);
  const [checked, setChecked] = useState<string[]>([]);

  const search = async () => {
    try {
      setCandidates(await evaluationService.getCandidates(period.id, { departmentId, partyCellId: cellId, q: query }));
      setSelected([]);
    } catch {
      setCandidates([]);
    }
  };

  const addByUnit = () =>
    run(async () => {
      const result = await evaluationService.addParticipants(period.id, { departmentId, partyCellId: cellId, workflowProfileCode: addProfile });
      if (result.skipped.length > 0) alertSkipped(result.skipped);
    }, "Đã thêm người được đánh giá theo đơn vị.");

  const addSelected = () =>
    run(async () => {
      const result = await evaluationService.addParticipants(period.id, { memberIds: selected, workflowProfileCode: addProfile });
      if (result.skipped.length > 0) alertSkipped(result.skipped);
      setSelected([]);
      await search();
    }, `Đã thêm ${selected.length} người được đánh giá.`);

  const allChecked = participants.length > 0 && checked.length === participants.length;

  return (
    <section className="card border-0 shadow-sm">
      <div className="card-body">
        <div className="d-flex justify-content-between align-items-center mb-2">
          <h2 className="h6 mb-0">Người được đánh giá ({participants.length})</h2>
          <Link href="/imports" className="btn btn-outline-secondary btn-sm"><i className="bi bi-file-earmark-excel me-1" />Nhập từ Excel (loại "Người được đánh giá của kỳ", có cột Hồ sơ luồng)</Link>
        </div>

        {canAdd && (
          <div className="border rounded-3 p-2 mb-3">
            <div className="row g-2 align-items-end">
              <div className="col-md-3"><label className="form-label small mb-0">Phòng</label>
                <select className="form-select form-select-sm" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
                  <option value="">— Tất cả —</option>
                  {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
                </select>
              </div>
              <div className="col-md-3"><label className="form-label small mb-0">Chi bộ</label>
                <select className="form-select form-select-sm" value={cellId} onChange={(e) => setCellId(e.target.value)}>
                  <option value="">— Tất cả —</option>
                  {cells.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>
              <div className="col-md-3"><label className="form-label small mb-0">Tên / tài khoản</label><input className="form-control form-control-sm" value={query} onChange={(e) => setQuery(e.target.value)} /></div>
              <div className="col-md-3"><label className="form-label small mb-0">Hồ sơ luồng khi thêm</label>
                <select className="form-select form-select-sm" value={addProfile} onChange={(e) => setAddProfile(e.target.value)}>
                  <option value="">Mặc định theo cấp quyết định</option>
                  {profiles.map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}
                </select>
              </div>
              <div className="col-12 d-flex gap-2">
                <button type="button" className="btn btn-outline-primary btn-sm" onClick={search}>Tìm cán bộ</button>
                <button type="button" className="btn btn-outline-success btn-sm" disabled={busy || (!departmentId && !cellId)} onClick={addByUnit}>Thêm cả đơn vị</button>
              </div>
            </div>
            {candidates.length > 0 && (
              <div className="mt-2">
                <div style={{ maxHeight: 220, overflowY: "auto" }}>
                  {candidates.map((c) => (
                    <div className="form-check small" key={c.memberId}>
                      <input className="form-check-input" type="checkbox" id={`cand-${c.memberId}`} disabled={c.alreadyAdded} checked={selected.includes(c.memberId)}
                        onChange={(e) => setSelected(e.target.checked ? [...selected, c.memberId] : selected.filter((id) => id !== c.memberId))} />
                      <label className="form-check-label" htmlFor={`cand-${c.memberId}`}>
                        {c.fullName} ({c.username}) · {c.departmentName || "—"} · {c.partyCellName || "—"}{c.alreadyAdded ? " — đã có" : ""}
                      </label>
                    </div>
                  ))}
                </div>
                <button type="button" className="btn btn-success btn-sm mt-2" disabled={busy || selected.length === 0} onClick={addSelected}>Thêm {selected.length} người đã chọn</button>
              </div>
            )}
          </div>
        )}

        {canChangeProfile && checked.length > 0 && (
          <div className="alert alert-light border d-flex justify-content-between align-items-center py-2 small">
            <span>Đã chọn {checked.length} người.</span>
            <button type="button" className="btn btn-outline-primary btn-sm" disabled={busy}
              onClick={() => setProfileTargets(participants.filter((p) => checked.includes(p.recordId)))}>
              Đổi hồ sơ luồng hàng loạt
            </button>
          </div>
        )}

        <div className="table-responsive">
          <table className="table table-sm align-middle mb-0">
            <thead>
              <tr className="small text-secondary">
                {canChangeProfile && (
                  <th style={{ width: 28 }}>
                    <input type="checkbox" className="form-check-input" aria-label="Chọn tất cả" checked={allChecked}
                      onChange={(e) => setChecked(e.target.checked ? participants.map((p) => p.recordId) : [])} />
                  </th>
                )}
                <th>Cán bộ</th><th>Phòng</th><th>Chi bộ</th><th>Khung tỷ trọng</th><th>Cấp quyết định</th><th>Hồ sơ luồng</th><th>Trạng thái</th><th />
              </tr>
            </thead>
            <tbody>
              {participants.map((p) => (
                <tr key={p.recordId} className="small">
                  {canChangeProfile && (
                    <td>
                      <input type="checkbox" className="form-check-input" aria-label={`Chọn ${p.fullName}`} checked={checked.includes(p.recordId)}
                        onChange={(e) => setChecked(e.target.checked ? [...checked, p.recordId] : checked.filter((id) => id !== p.recordId))} />
                    </td>
                  )}
                  <td><Link href={`/evaluations/${p.recordId}`}>{p.fullName}</Link><div className="text-secondary">{p.username}</div></td>
                  <td>{p.departmentName || "—"}</td>
                  <td>{p.partyCellName || "—"}</td>
                  <td title={p.weightFrameName || undefined}>{p.weightFrameCode || "—"}{p.weightFrameCode && !p.weightFrameName && period.criteria?.selfScoreForm === "09A" && <i className="bi bi-exclamation-triangle text-danger ms-1" title="Khung không có trong bộ tiêu chí của kỳ" />}</td>
                  <td>{p.approvalAuthority === "CapTren" ? "Cấp trên" : "Cơ sở"}</td>
                  <td>{p.workflowProfileName || p.workflowProfileCode}</td>
                  <td>{p.statusDisplayName}</td>
                  <td className="text-end text-nowrap">
                    {canChangeProfile && <button type="button" className="btn btn-link btn-sm p-0 me-2" onClick={() => setProfileTargets([p])}>Đổi hồ sơ luồng</button>}
                    {canAdd && <button type="button" className="btn btn-link btn-sm p-0 me-2" onClick={() => setEditing(p)}>Sửa ảnh chụp</button>}
                    {canAdd && (
                      <button type="button" className="btn btn-link btn-sm p-0 text-danger" disabled={busy}
                        onClick={() => run(() => evaluationService.removeParticipant(period.id, p.recordId, p.version), "Đã bỏ khỏi danh sách.")}>
                        Bỏ
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

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
            }, "Đã sửa thông tin ảnh chụp.")
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
                if (result.skipped.length > 0) alertSkipped(result.skipped, `Đã đổi ${result.updated} hồ sơ. Một số hồ sơ không đổi được:`);
              }
              setProfileTargets(null);
              setChecked([]);
            }, "Đã đổi hồ sơ luồng.")
          }
        />
      )}
    </section>
  );
}

function alertSkipped(skipped: string[], title = "Một số người không được thêm:") {
  if (typeof window !== "undefined") window.alert(title + "\n" + skipped.join("\n"));
}

interface ProfileDialogProps {
  targets: PeriodParticipantDto[];
  profiles: WorkflowProfile[];
  busy: boolean;
  onCancel: () => void;
  onSave: (workflowProfileCode: string, reason: string) => void;
}

/** Đổi hồ sơ luồng (một hoặc nhiều người): bắt buộc lý do; máy chủ chỉ đổi hồ sơ chưa qua bước bị ảnh hưởng. */
function ProfileDialog({ targets, profiles, busy, onCancel, onSave }: ProfileDialogProps) {
  const [code, setCode] = useState(targets.length === 1 ? targets[0].workflowProfileCode : "");
  const [reason, setReason] = useState("");
  return (
    <div className="modal fade show d-block" tabIndex={-1} role="dialog" aria-modal="true" style={{ backgroundColor: "rgba(15, 23, 42, 0.55)", zIndex: 1080 }}>
      <div className="modal-dialog modal-dialog-centered">
        <div className="modal-content border-0 shadow-lg">
          <div className="modal-header">
            <h2 className="modal-title fs-6 fw-bold">Đổi hồ sơ luồng — {targets.length === 1 ? targets[0].fullName : `${targets.length} người`}</h2>
            <button type="button" className="btn-close" aria-label="Đóng" onClick={onCancel} />
          </div>
          <form onSubmit={(e) => { e.preventDefault(); onSave(code, reason.trim()); }}>
            <div className="modal-body d-flex flex-column gap-2">
              <div className="small text-secondary">
                Chỉ đổi được khi hồ sơ chưa qua bước mà hai hồ sơ luồng cấu hình khác nhau; bước đang chờ không còn áp dụng thì hồ sơ
                chuyển sang bước áp dụng kế tiếp. Mọi lần đổi được ghi lịch sử hồ sơ.
              </div>
              <div><label className="form-label small" htmlFor="profile-target">Hồ sơ luồng mới</label>
                <select id="profile-target" className="form-select form-select-sm" required value={code} onChange={(e) => setCode(e.target.value)}>
                  <option value="">— Chọn —</option>
                  {profiles.map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}
                </select>
              </div>
              <div><label className="form-label small" htmlFor="profile-reason">Lý do (bắt buộc)</label>
                <textarea id="profile-reason" className="form-control form-control-sm" rows={3} maxLength={2000} required value={reason} onChange={(e) => setReason(e.target.value)} />
              </div>
            </div>
            <div className="modal-footer">
              <button type="button" className="btn btn-outline-secondary btn-sm" onClick={onCancel}>Hủy</button>
              <button type="submit" className="btn btn-primary btn-sm" disabled={busy || !code || !reason.trim()}>Đổi hồ sơ luồng</button>
            </div>
          </form>
        </div>
      </div>
    </div>
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
    <div className="modal fade show d-block" tabIndex={-1} role="dialog" aria-modal="true" style={{ backgroundColor: "rgba(15, 23, 42, 0.55)", zIndex: 1080 }}>
      <div className="modal-dialog modal-dialog-centered">
        <div className="modal-content border-0 shadow-lg">
          <div className="modal-header"><h2 className="modal-title fs-6 fw-bold">Sửa ảnh chụp — {participant.fullName}</h2><button type="button" className="btn-close" aria-label="Đóng" onClick={onCancel} /></div>
          <form onSubmit={(e) => { e.preventDefault(); onSave({ departmentId: departmentId || null, partyCellId: cellId || null, weightFrameCode, approvalAuthority: authority, reason }); }}>
            <div className="modal-body row g-2">
              <div className="col-6"><label className="form-label small">Phòng</label>
                <select className="form-select form-select-sm" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}>
                  <option value="">— Không —</option>
                  {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
                </select>
              </div>
              <div className="col-6"><label className="form-label small">Chi bộ</label>
                <select className="form-select form-select-sm" value={cellId} onChange={(e) => setCellId(e.target.value)}>
                  <option value="">— Không —</option>
                  {cells.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>
              <div className="col-6"><label className="form-label small">Khung tỷ trọng A-B-C-D</label>
                <select className="form-select form-select-sm" value={weightFrameCode} onChange={(e) => setWeightFrameCode(e.target.value)}>
                  {!frames.some((f) => f.code === weightFrameCode) && <option value={weightFrameCode}>{weightFrameCode || "— Chưa có —"} (không có trong bộ)</option>}
                  {frames.map((f) => <option key={f.code} value={f.code}>{f.code} — {f.name}</option>)}
                </select>
                <div className="form-text">Danh sách khung theo bộ tiêu chí của kỳ.</div>
              </div>
              <div className="col-6"><label className="form-label small">Cấp quyết định</label>
                <select className="form-select form-select-sm" value={authority} onChange={(e) => setAuthority(e.target.value)}>
                  <option value="CoSo">Đảng ủy cơ sở</option>
                  <option value="CapTren">Cấp trên</option>
                </select>
                <div className="form-text">Không tự đổi hồ sơ luồng — dùng "Đổi hồ sơ luồng" nếu cần.</div>
              </div>
              <div className="col-12"><label className="form-label small">Lý do (bắt buộc)</label><textarea className="form-control form-control-sm" rows={3} required value={reason} onChange={(e) => setReason(e.target.value)} /></div>
            </div>
            <div className="modal-footer">
              <button type="button" className="btn btn-outline-secondary btn-sm" onClick={onCancel}>Hủy</button>
              <button type="submit" className="btn btn-primary btn-sm" disabled={busy || !reason.trim()}>Lưu</button>
            </div>
          </form>
        </div>
      </div>
    </div>
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
    <section className="card border-0 shadow-sm">
      <div className="card-body">
        <div className="d-flex justify-content-between align-items-center mb-2 flex-wrap gap-2">
          <h2 className="h6 mb-0">Bộ tiêu chí và thang điểm</h2>
          <Link href="/criteria" className="btn btn-link btn-sm p-0">Quản lý bộ tiêu chí</Link>
        </div>
        {criteria ? (
          <div className="small mb-2">
            <strong>{criteria.name}</strong> ({criteria.code}) · Mẫu tự chấm {criteria.selfScoreForm} ·{" "}
            {period.status === "Draft" ? "chọn lúc" : "chụp vào kỳ lúc"} {new Date(criteria.takenAt).toLocaleString("vi-VN")}
            <button type="button" className="btn btn-link btn-sm p-0 ms-2" onClick={() => setExpanded(!expanded)}>{expanded ? "Ẩn nội dung" : "Xem nội dung"}</button>
          </div>
        ) : (
          <div className="alert alert-warning small py-2">Kỳ chưa chọn bộ tiêu chí — chưa mở được kỳ.</div>
        )}
        {editable && (
          <div className="d-flex gap-2 align-items-end flex-wrap mb-2">
            <div style={{ minWidth: 320 }}>
              <label className="form-label small mb-0" htmlFor="period-criteria-set">Chọn bộ đã xuất bản</label>
              <select id="period-criteria-set" className="form-select form-select-sm" value={choice} onChange={(e) => setChoice(e.target.value)}>
                <option value="">— Chọn —</option>
                {sets.map((s) => <option key={s.id} value={s.id}>{s.name} — Mẫu {s.selfScoreForm} ({s.code})</option>)}
              </select>
            </div>
            <button type="button" className="btn btn-outline-primary btn-sm" disabled={busy || !choice || choice === period.criteriaSetId} onClick={() => onChoose(choice)}>
              Dùng bộ này
            </button>
            <div className="small text-secondary">Bộ 09A cần bước đăng ký sản phẩm ở mọi hồ sơ luồng.</div>
          </div>
        )}
        {criteria && expanded && <CriteriaSummary content={criteria.content} form={criteria.selfScoreForm} />}
      </div>
    </section>
  );
}
