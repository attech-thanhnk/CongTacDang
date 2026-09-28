"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { ReasonDialog } from "@/components/evaluations/ReasonDialog";
import { organizationService, BranchItem, DepartmentItem } from "@/services/organizationService";
import {
  EvaluationParameters,
  EvaluationPeriodDto,
  MANDATORY_STEPS,
  ParticipantCandidateDto,
  PeriodParticipantDto,
  PeriodSettings,
  STEP_NAMES,
  STEP_ORDER,
  evaluationService,
} from "@/services/evaluationService";

const JOB_GROUPS: { value: string; label: string }[] = [
  { value: "Khung1_QuanLyDangDoanThe", label: "Khung 1 — Quản lý, Đảng, đoàn thể" },
  { value: "Khung2_AnToanKyThuat", label: "Khung 2 — An toàn, kỹ thuật" },
  { value: "Khung3_DuAnDauTu", label: "Khung 3 — Dự án, đầu tư, tài chính" },
  { value: "Khung4_KhcnChuyenDoiSo", label: "Khung 4 — KHCN, chuyển đổi số" },
];

const PARAMETER_FIELDS: { key: keyof EvaluationParameters; label: string; step?: number }[] = [
  { key: "minTasks", label: "Số sản phẩm tối thiểu" },
  { key: "maxTasks", label: "Số sản phẩm tối đa" },
  { key: "totalTaskWeight", label: "Tổng trọng số sản phẩm", step: 0.5 },
  { key: "generalCriterionMaxScore", label: "Điểm tối đa mỗi tiêu chí chung", step: 0.5 },
  { key: "excellentMinScore", label: "Ngưỡng gợi ý Xuất sắc", step: 0.5 },
  { key: "goodMinScore", label: "Ngưỡng gợi ý Hoàn thành tốt", step: 0.5 },
  { key: "satisfactoryMinScore", label: "Ngưỡng gợi ý Hoàn thành", step: 0.5 },
  { key: "excellentQuotaRatio", label: "Trần tỷ lệ Xuất sắc (0–1)", step: 0.01 },
];

type Transition = "open" | "lock" | "unlock" | "close";

/** Cấu hình kỳ: trạng thái kỳ, bước/thời hạn/tham số, danh sách người được đánh giá. */
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
  const [busy, setBusy] = useState(false);
  const [unlockOpen, setUnlockOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!periodId) return;
    try {
      const loaded = await evaluationService.getPeriod(periodId);
      setPeriod(loaded);
      setSettings(JSON.parse(JSON.stringify(loaded.settings)));
      if (canManage) setParticipants(await evaluationService.getParticipants(periodId));
    } catch (err: any) {
      setError(err?.message || "Không tải được kỳ đánh giá.");
    }
  }, [periodId, canManage]);

  useEffect(() => {
    load();
    organizationService.getDepartments().then(setDepartments).catch(() => setDepartments([]));
    organizationService.getBranches().then(setCells).catch(() => setCells([]));
  }, [load]);

  if (error) return <div className="page-wrapper"><div className="alert alert-danger m-4">{error}</div></div>;
  if (!period || !settings) return <div className="page-wrapper"><div className="text-secondary p-4"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div></div>;

  const isDraft = period.status === "Draft";
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

  const saveSettings = () =>
    run(() => evaluationService.updatePeriod(period.id, period.version, { settings }), "Đã lưu cấu hình kỳ.");

  const setStep = (step: string, changes: Partial<{ enabled: boolean; deadline: string | null }>) =>
    setSettings({ ...settings, steps: { ...settings.steps, [step]: { ...settings.steps[step], ...changes } } });

  const setParameter = (key: keyof EvaluationParameters, value: number) =>
    setSettings({ ...settings, parameters: { ...settings.parameters, [key]: value } });

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
                onClick={() => confirm({ title: "Mở kỳ đánh giá", message: "Sau khi mở, chỉ sửa được thời hạn các bước. Bật/tắt bước, mẫu tự chấm và tham số sẽ bị khóa.", confirmText: "Mở kỳ", onConfirm: () => transition("open") })}>
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
              <h2 className="h6 mb-0">Các bước và thời hạn</h2>
              {canManage && period.status !== "Closed" && (
                <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={saveSettings}>Lưu cấu hình</button>
              )}
            </div>
            {!isDraft && <div className="small text-secondary mb-2">Kỳ đã mở: chỉ sửa được thời hạn các bước.</div>}
            <table className="table table-sm align-middle">
              <thead><tr className="small text-secondary"><th>#</th><th>Bước</th><th>Áp dụng</th><th>Thời hạn</th></tr></thead>
              <tbody>
                {STEP_ORDER.map((step, index) => {
                  const setting = settings.steps[step] || { enabled: true };
                  const mandatory = MANDATORY_STEPS.includes(step);
                  return (
                    <tr key={step} className="small" style={{ opacity: setting.enabled ? 1 : 0.6 }}>
                      <td>{index + 1}</td>
                      <td>{STEP_NAMES[step]}{mandatory && <span className="text-secondary"> (bắt buộc)</span>}</td>
                      <td>
                        <input type="checkbox" className="form-check-input" checked={setting.enabled} disabled={!canManage || !isDraft || mandatory}
                          onChange={(e) => setStep(step, { enabled: e.target.checked })} aria-label={`Áp dụng ${STEP_NAMES[step]}`} />
                      </td>
                      <td style={{ maxWidth: 180 }}>
                        <input type="date" className="form-control form-control-sm" value={setting.deadline || ""} disabled={!deadlinesEditable || !setting.enabled}
                          onChange={(e) => setStep(step, { deadline: e.target.value || null })} />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            <div className="row g-3">
              <div className="col-md-6">
                <div className="small fw-semibold mb-1">Mẫu tự chấm</div>
                {[
                  { value: "09A", label: "09A — có Mẫu 01/02 (chấm A-B-C-D theo sản phẩm)" },
                  { value: "09B", label: "09B — chấm trực tiếp 6 trục (Quý III/2026)" },
                ].map((option) => (
                  <div className="form-check" key={option.value}>
                    <input className="form-check-input" type="radio" name="selfScoreForm" id={`form-${option.value}`} checked={settings.selfScoreForm === option.value}
                      disabled={!canManage || !isDraft} onChange={() => setSettings({ ...settings, selfScoreForm: option.value })} />
                    <label className="form-check-label small" htmlFor={`form-${option.value}`}>{option.label}</label>
                  </div>
                ))}
              </div>
              <div className="col-md-6">
                <div className="form-check">
                  <input className="form-check-input" type="checkbox" id="enforce" checked={settings.enforceDeadlines} disabled={!canManage || !isDraft}
                    onChange={(e) => setSettings({ ...settings, enforceDeadlines: e.target.checked })} />
                  <label className="form-check-label small" htmlFor="enforce">Chặn hoàn thành bước khi đã quá thời hạn (mặc định chỉ cảnh báo)</label>
                </div>
              </div>
            </div>
            <h3 className="h6 mt-3">Tham số</h3>
            <div className="row g-2">
              {PARAMETER_FIELDS.map((field) => (
                <div className="col-6 col-md-3" key={field.key}>
                  <label className="form-label small mb-0">{field.label}</label>
                  <input type="number" step={field.step ?? 1} className="form-control form-control-sm" value={settings.parameters[field.key] as number}
                    disabled={!canManage || !isDraft} onChange={(e) => setParameter(field.key, Number(e.target.value))} />
                </div>
              ))}
            </div>
            <div className="small text-secondary mt-2">
              Tỷ trọng A-B-C-D theo khung:{" "}
              {Object.entries(settings.parameters.jobGroupWeights).map(([group, w]) => `${group.split("_")[0]} ${w.a}/${w.b}/${w.c}/${w.d}`).join(" · ")}
              {" · "}Điểm tối đa 6 trục (09B): {settings.parameters.axisMaxScores.join("/")}
            </div>
          </div>
        </section>

        {canManage && (
          <ParticipantsSection
            period={period}
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
    </div>
  );
}

interface ParticipantsProps {
  period: EvaluationPeriodDto;
  participants: PeriodParticipantDto[];
  departments: DepartmentItem[];
  cells: BranchItem[];
  busy: boolean;
  run: (work: () => Promise<unknown>, success: string) => Promise<void>;
}

function ParticipantsSection({ period, participants, departments, cells, busy, run }: ParticipantsProps) {
  const canAdd = period.status === "Draft" || period.status === "Open";
  const [departmentId, setDepartmentId] = useState("");
  const [cellId, setCellId] = useState("");
  const [query, setQuery] = useState("");
  const [candidates, setCandidates] = useState<ParticipantCandidateDto[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [editing, setEditing] = useState<PeriodParticipantDto | null>(null);

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
      const result = await evaluationService.addParticipants(period.id, { departmentId, partyCellId: cellId });
      if (result.skipped.length > 0) alertSkipped(result.skipped);
    }, "Đã thêm người được đánh giá theo đơn vị.");

  const addSelected = () =>
    run(async () => {
      const result = await evaluationService.addParticipants(period.id, { memberIds: selected });
      if (result.skipped.length > 0) alertSkipped(result.skipped);
      setSelected([]);
      await search();
    }, `Đã thêm ${selected.length} người được đánh giá.`);

  return (
    <section className="card border-0 shadow-sm">
      <div className="card-body">
        <div className="d-flex justify-content-between align-items-center mb-2">
          <h2 className="h6 mb-0">Người được đánh giá ({participants.length})</h2>
          <Link href="/imports" className="btn btn-outline-secondary btn-sm"><i className="bi bi-file-earmark-excel me-1" />Nhập từ Excel (loại "Người được đánh giá của kỳ")</Link>
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
              <div className="col-md-3 d-flex gap-2">
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

        <div className="table-responsive">
          <table className="table table-sm align-middle mb-0">
            <thead><tr className="small text-secondary"><th>Cán bộ</th><th>Phòng</th><th>Chi bộ</th><th>Khung</th><th>Cấp quyết định</th><th>Trạng thái</th><th /></tr></thead>
            <tbody>
              {participants.map((p) => (
                <tr key={p.recordId} className="small">
                  <td><Link href={`/evaluations/${p.recordId}`}>{p.fullName}</Link><div className="text-secondary">{p.username}</div></td>
                  <td>{p.departmentName || "—"}</td>
                  <td>{p.partyCellName || "—"}</td>
                  <td>{p.jobGroup.split("_")[0]}</td>
                  <td>{p.approvalAuthority === "CapTren" ? "Cấp trên" : "Cơ sở"}</td>
                  <td>{p.statusDisplayName}</td>
                  <td className="text-end text-nowrap">
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
    </section>
  );
}

function alertSkipped(skipped: string[]) {
  if (typeof window !== "undefined") window.alert("Một số người không được thêm:\n" + skipped.join("\n"));
}

interface SnapshotProps {
  participant: PeriodParticipantDto;
  departments: DepartmentItem[];
  cells: BranchItem[];
  busy: boolean;
  onCancel: () => void;
  onSave: (payload: { departmentId: string | null; partyCellId: string | null; jobGroup: string; approvalAuthority: string; reason: string }) => void;
}

function SnapshotDialog({ participant, departments, cells, busy, onCancel, onSave }: SnapshotProps) {
  const [departmentId, setDepartmentId] = useState(participant.departmentId || "");
  const [cellId, setCellId] = useState(participant.partyCellId || "");
  const [jobGroup, setJobGroup] = useState(participant.jobGroup);
  const [authority, setAuthority] = useState(participant.approvalAuthority);
  const [reason, setReason] = useState("");
  return (
    <div className="modal fade show d-block" tabIndex={-1} role="dialog" aria-modal="true" style={{ backgroundColor: "rgba(15, 23, 42, 0.55)", zIndex: 1080 }}>
      <div className="modal-dialog modal-dialog-centered">
        <div className="modal-content border-0 shadow-lg">
          <div className="modal-header"><h2 className="modal-title fs-6 fw-bold">Sửa ảnh chụp — {participant.fullName}</h2><button type="button" className="btn-close" aria-label="Đóng" onClick={onCancel} /></div>
          <form onSubmit={(e) => { e.preventDefault(); onSave({ departmentId: departmentId || null, partyCellId: cellId || null, jobGroup, approvalAuthority: authority, reason }); }}>
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
              <div className="col-6"><label className="form-label small">Khung chức danh</label>
                <select className="form-select form-select-sm" value={jobGroup} onChange={(e) => setJobGroup(e.target.value)}>
                  {JOB_GROUPS.map((g) => <option key={g.value} value={g.value}>{g.label}</option>)}
                </select>
              </div>
              <div className="col-6"><label className="form-label small">Cấp quyết định</label>
                <select className="form-select form-select-sm" value={authority} onChange={(e) => setAuthority(e.target.value)}>
                  <option value="CoSo">Đảng ủy cơ sở</option>
                  <option value="CapTren">Cấp trên</option>
                </select>
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
