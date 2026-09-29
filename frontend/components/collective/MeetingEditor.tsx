"use client";

import React, { FormEvent, useEffect, useState } from "react";
import { EvaluationMeetingVoteSummaryDto, EvaluationRecordDto, evaluationService } from "@/services/evaluationService";
import type { BranchItem, DepartmentItem } from "@/services/organizationService";
import { MeetingAttendee, MeetingRecord, SaveMeetingRecord, collectiveService } from "@/services/collectiveService";

interface Props {
  periodId: string;
  branches: BranchItem[];
  departments: DepartmentItem[];
  /** Biên bản đang sửa (null = lập mới). */
  editing: MeetingRecord | null;
  onSaved: (meeting: MeetingRecord) => void;
  onCancel: () => void;
  showError: (message: string) => void;
}

type Unit = "cell" | "department" | "company";
type Vote = Omit<EvaluationMeetingVoteSummaryDto, "recordId">;

/** ISO (UTC) → giá trị ô datetime-local theo giờ máy người dùng. */
const toLocalInput = (iso?: string | null) => {
  if (!iso) return "";
  const date = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};

const toIso = (local: string) => (local ? new Date(local).toISOString() : undefined);

const emptyVote: Vote = { votesExcellent: 0, votesGood: 0, votesSatisfactory: 0, votesUnsatisfactory: 0, invalidVotes: 0, notes: "" };

/** Lập/sửa biên bản hội nghị (Mẫu 12) hoặc biên bản kiểm phiếu (Mẫu 13) với đủ các mục của biểu mẫu gốc. */
export function MeetingEditor({ periodId, branches, departments, editing, onSaved, onCancel, showError }: Props) {
  const [formCode, setFormCode] = useState<"M12" | "M13">("M12");
  const [unit, setUnit] = useState<Unit>("cell");
  const [partyCellId, setPartyCellId] = useState("");
  const [departmentId, setDepartmentId] = useState("");
  const [stage, setStage] = useState<"" | "B3A_COLLECTIVE" | "B4_DECISION">("B4_DECISION");
  const [location, setLocation] = useState("");
  const [startedAt, setStartedAt] = useState(toLocalInput(new Date().toISOString()));
  const [endedAt, setEndedAt] = useState("");
  const [invited, setInvited] = useState(0);
  const [present, setPresent] = useState(0);
  const [absentReasons, setAbsentReasons] = useState("");
  const [chairName, setChairName] = useState("");
  const [chairTitle, setChairTitle] = useState("");
  const [secretaryName, setSecretaryName] = useState("");
  const [secretaryTitle, setSecretaryTitle] = useState("");
  const [workingRules, setWorkingRules] = useState("");
  const [reportingUnit, setReportingUnit] = useState("");
  const [attendees, setAttendees] = useState<MeetingAttendee[]>([]);
  const [minutes, setMinutes] = useState("");
  const [outcome, setOutcome] = useState("");
  const [records, setRecords] = useState<EvaluationRecordDto[]>([]);
  const [votes, setVotes] = useState<Record<string, Vote>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!editing) return;
    setFormCode(editing.formCode === "M13" ? "M13" : "M12");
    setUnit(editing.partyCellId ? "cell" : editing.departmentId ? "department" : "company");
    setPartyCellId(editing.partyCellId || "");
    setDepartmentId(editing.departmentId || "");
    setStage((editing.stage as "B3A_COLLECTIVE" | "B4_DECISION" | null) || "");
    setLocation(editing.location || "");
    setStartedAt(toLocalInput(editing.startedAt));
    setEndedAt(toLocalInput(editing.endedAt));
    setInvited(editing.invitedCount);
    setPresent(editing.presentCount);
    setAbsentReasons(editing.absentReasons || "");
    setChairName(editing.chairName || "");
    setSecretaryName(editing.secretaryName || "");
    setMinutes(editing.minutesContent || "");
    setOutcome(editing.outcomeContent || "");
    const details = editing.details || { attendees: [] };
    setChairTitle(details.chairTitle || "");
    setSecretaryTitle(details.secretaryTitle || "");
    setWorkingRules(details.workingRules || "");
    setReportingUnit(details.reportingUnit || "");
    setAttendees(details.attendees || []);
  }, [editing]);

  // Mẫu 13 lập mới: tải hồ sơ của đơn vị tổ chức hội nghị để ghi tổng hợp phiếu (máy chủ lọc theo phạm vi xem).
  useEffect(() => {
    if (editing || formCode !== "M13" || !periodId) {
      setRecords([]);
      return;
    }
    if ((unit === "cell" && !partyCellId) || (unit === "department" && !departmentId)) {
      setRecords([]);
      return;
    }
    const load = unit === "cell"
      ? evaluationService.getRecordsByBranch(periodId, partyCellId)
      : evaluationService.getRecordsByPeriod(periodId).then((list) =>
          unit === "department" ? list.filter((r) => r.departmentId === departmentId) : list);
    load.then(setRecords).catch(() => setRecords([]));
  }, [editing, formCode, periodId, unit, partyCellId, departmentId]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!editing && ((unit === "cell" && !partyCellId) || (unit === "department" && !departmentId))) {
      showError(unit === "department" ? "Hãy chọn Phòng / đơn vị tổ chức hội nghị." : "Hãy chọn Chi bộ / tổ chức Đảng tổ chức hội nghị.");
      return;
    }
    if (present > invited) {
      showError("Số có mặt không được vượt số triệu tập.");
      return;
    }
    setSaving(true);
    const payload: SaveMeetingRecord = {
      version: editing?.version,
      periodId,
      partyCellId: unit === "cell" ? partyCellId || undefined : undefined,
      departmentId: unit === "department" ? departmentId || undefined : undefined,
      stage: stage || undefined,
      formCode,
      meetingType: stage === "B3A_COLLECTIVE" ? "Hội nghị tập thể lãnh đạo, quản lý" : "Hội nghị đánh giá, xếp loại chất lượng cán bộ quý",
      location,
      startedAt: toIso(startedAt) || new Date().toISOString(),
      endedAt: toIso(endedAt),
      invitedCount: invited,
      presentCount: present,
      absentCount: Math.max(invited - present, 0),
      absentReasons,
      chairName,
      secretaryName,
      minutesContent: minutes,
      outcomeContent: outcome,
      voteCountingContent: editing?.voteCountingContent || "",
      details: {
        workingRules: workingRules || null,
        reportingUnit: reportingUnit || null,
        chairTitle: chairTitle || null,
        secretaryTitle: secretaryTitle || null,
        attendees: attendees.filter((a) => a.name.trim()),
      },
      voteSummaries: !editing && formCode === "M13"
        ? records.map((record) => ({ recordId: record.id, ...(votes[record.id] || emptyVote) }))
        : [],
    };
    try {
      const saved = editing ? await collectiveService.updateMeeting(editing.id, payload) : await collectiveService.createMeeting(payload);
      onSaved(saved);
    } catch (error: any) {
      showError(error?.message || "Không lưu được biên bản.");
    } finally {
      setSaving(false);
    }
  };

  const setAttendee = (index: number, patch: Partial<MeetingAttendee>) =>
    setAttendees(attendees.map((a, i) => (i === index ? { ...a, ...patch } : a)));

  return (
    <form onSubmit={submit} className="row g-2">
      <div className="col-6">
        <label className="form-label small mb-1">Biểu mẫu</label>
        <select className="form-select form-select-sm" value={formCode} disabled={!!editing} onChange={(e) => setFormCode(e.target.value as "M12" | "M13")}>
          <option value="M12">Mẫu 12 — Biên bản hội nghị</option>
          <option value="M13">Mẫu 13 — Biên bản kiểm phiếu</option>
        </select>
      </div>
      <div className="col-6">
        <label className="form-label small mb-1">Hội nghị</label>
        <select className="form-select form-select-sm" value={stage} disabled={!!editing} onChange={(e) => setStage(e.target.value as "" | "B3A_COLLECTIVE" | "B4_DECISION")}>
          <option value="B3A_COLLECTIVE">Tập thể lãnh đạo, quản lý (đề xuất)</option>
          <option value="B4_DECISION">Đảng ủy/Chi ủy cơ sở (quyết định)</option>
          <option value="">Không xác định</option>
        </select>
      </div>
      <div className="col-6">
        <label className="form-label small mb-1">Đơn vị tổ chức</label>
        <select className="form-select form-select-sm" value={unit} disabled={!!editing} onChange={(e) => setUnit(e.target.value as Unit)}>
          <option value="cell">Tổ chức Đảng</option>
          <option value="department">Phòng / đơn vị</option>
          <option value="company">Cấp Công ty</option>
        </select>
      </div>
      <div className="col-6">
        {unit === "cell" && (
          <>
            <label className="form-label small mb-1">Tổ chức Đảng</label>
            <select className="form-select form-select-sm" value={partyCellId} disabled={!!editing} onChange={(e) => setPartyCellId(e.target.value)}>
              <option value="">Chọn tổ chức Đảng</option>
              {branches.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
            </select>
          </>
        )}
        {unit === "department" && (
          <>
            <label className="form-label small mb-1">Phòng / đơn vị</label>
            <select className="form-select form-select-sm" value={departmentId} disabled={!!editing} onChange={(e) => setDepartmentId(e.target.value)}>
              <option value="">Chọn Phòng / đơn vị</option>
              {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </>
        )}
      </div>
      <div className="col-12">
        <label className="form-label small mb-1">Căn cứ Quy chế làm việc của</label>
        <input className="form-control form-control-sm" placeholder="… nhiệm kỳ …" value={workingRules} onChange={(e) => setWorkingRules(e.target.value)} />
      </div>
      <div className="col-6">
        <label className="form-label small mb-1">1. Bắt đầu</label>
        <input type="datetime-local" className="form-control form-control-sm" value={startedAt} onChange={(e) => setStartedAt(e.target.value)} />
      </div>
      <div className="col-6">
        <label className="form-label small mb-1">Kết thúc</label>
        <input type="datetime-local" className="form-control form-control-sm" value={endedAt} onChange={(e) => setEndedAt(e.target.value)} />
      </div>
      <div className="col-12">
        <label className="form-label small mb-1">2. Địa điểm</label>
        <input className="form-control form-control-sm" value={location} onChange={(e) => setLocation(e.target.value)} />
      </div>
      <div className="col-4">
        <label className="form-label small mb-1">Triệu tập</label>
        <input type="number" min={0} className="form-control form-control-sm" value={invited} onChange={(e) => setInvited(Number(e.target.value))} />
      </div>
      <div className="col-4">
        <label className="form-label small mb-1">Có mặt</label>
        <input type="number" min={0} className="form-control form-control-sm" value={present} onChange={(e) => setPresent(Number(e.target.value))} />
      </div>
      <div className="col-4">
        <label className="form-label small mb-1">Vắng mặt</label>
        <input className="form-control form-control-sm" readOnly value={Math.max(invited - present, 0)} />
      </div>
      <div className="col-12">
        <label className="form-label small mb-1">Lý do vắng mặt</label>
        <input className="form-control form-control-sm" value={absentReasons} onChange={(e) => setAbsentReasons(e.target.value)} />
      </div>
      <div className="col-12">
        <label className="form-label small mb-1">3.2. Cán bộ, đảng viên khác được cử tham dự (ghi chép, báo cáo, phục vụ hội nghị)</label>
        {attendees.map((attendee, index) => (
          <div key={index} className="d-flex gap-1 mb-1">
            <input className="form-control form-control-sm" placeholder="Họ và tên" value={attendee.name} onChange={(e) => setAttendee(index, { name: e.target.value })} />
            <input className="form-control form-control-sm" placeholder="Chức vụ Đảng, chính quyền" value={attendee.title} onChange={(e) => setAttendee(index, { title: e.target.value })} />
            <button type="button" className="btn btn-sm btn-outline-danger" aria-label="Bỏ người dự" onClick={() => setAttendees(attendees.filter((_, i) => i !== index))}>
              <i className="bi bi-x" />
            </button>
          </div>
        ))}
        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => setAttendees([...attendees, { name: "", title: "" }])}>
          <i className="bi bi-plus me-1" />Thêm người dự
        </button>
      </div>
      <div className="col-md-6">
        <label className="form-label small mb-1">4. Chủ trì hội nghị</label>
        <input className="form-control form-control-sm mb-1" placeholder="Họ và tên" value={chairName} onChange={(e) => setChairName(e.target.value)} />
        <input className="form-control form-control-sm" placeholder="Chức vụ Đảng, chính quyền" value={chairTitle} onChange={(e) => setChairTitle(e.target.value)} />
      </div>
      <div className="col-md-6">
        <label className="form-label small mb-1">5. Thư ký hội nghị</label>
        <input className="form-control form-control-sm mb-1" placeholder="Họ và tên" value={secretaryName} onChange={(e) => setSecretaryName(e.target.value)} />
        <input className="form-control form-control-sm" placeholder="Chức vụ Đảng, chính quyền" value={secretaryTitle} onChange={(e) => setSecretaryTitle(e.target.value)} />
      </div>
      <div className="col-12">
        <label className="form-label small mb-1">II. Cơ quan, đơn vị báo cáo tình hình thực hiện nhiệm vụ trọng tâm</label>
        <input className="form-control form-control-sm" placeholder="Để trống: tên đơn vị tổ chức hội nghị" value={reportingUnit} onChange={(e) => setReportingUnit(e.target.value)} />
      </div>
      <div className="col-12">
        <label className="form-label small mb-1">Diễn biến hội nghị</label>
        <textarea className="form-control form-control-sm" rows={3} value={minutes} onChange={(e) => setMinutes(e.target.value)} />
      </div>
      <div className="col-12">
        <label className="form-label small mb-1">Kết quả hội nghị</label>
        <textarea className="form-control form-control-sm" rows={2} value={outcome} onChange={(e) => setOutcome(e.target.value)} />
      </div>

      {formCode === "M13" && !editing && (
        <div className="col-12">
          <label className="form-label small mb-1">Tổng hợp phiếu theo hồ sơ (không lưu người bỏ phiếu)</label>
          <div className="border rounded p-2" style={{ maxHeight: 240, overflowY: "auto" }}>
            {records.length === 0 ? (
              <div className="text-secondary small">Chưa tải được hồ sơ của đơn vị đã chọn.</div>
            ) : records.map((record) => {
              const vote = votes[record.id] || emptyVote;
              const update = (key: keyof Vote, value: number | string) => setVotes({ ...votes, [record.id]: { ...vote, [key]: value } });
              return (
                <div key={record.id} className="border-bottom py-2">
                  <div className="small fw-semibold mb-1">{record.fullName}</div>
                  <div className="row g-1">
                    {([
                      ["votesExcellent", "Xuất sắc"],
                      ["votesGood", "Tốt"],
                      ["votesSatisfactory", "Hoàn thành"],
                      ["votesUnsatisfactory", "Không HT"],
                      ["invalidVotes", "Không hợp lệ"],
                    ] as [keyof Vote, string][]).map(([key, label]) => (
                      <div key={key} className="col">
                        <input aria-label={label} title={label} placeholder={label} type="number" min={0} className="form-control form-control-sm" value={vote[key] as number} onChange={(e) => update(key, Number(e.target.value))} />
                      </div>
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}
      {formCode === "M13" && editing && (
        <div className="col-12 small text-secondary">Kết quả kiểm phiếu đã ghi nhận cho hồ sơ không sửa ở đây.</div>
      )}

      <div className="col-12 d-flex gap-2 mt-2">
        <button className="btn btn-outline-primary btn-sm" disabled={saving}>{saving ? "Đang lưu..." : editing ? "Lưu thay đổi" : "Lưu biên bản"}</button>
        {editing && <button type="button" className="btn btn-outline-secondary btn-sm" onClick={onCancel}>Hủy sửa</button>}
      </div>
    </form>
  );
}

export default MeetingEditor;
