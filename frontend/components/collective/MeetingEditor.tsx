"use client";

import React, { FormEvent, useEffect, useState } from "react";
import { EvaluationMeetingVoteSummaryDto, EvaluationRecordDto, evaluationService } from "@/services/evaluationService";
import type { BranchItem, DepartmentItem } from "@/services/organizationService";
import { MeetingAttendee, MeetingRecord, SaveMeetingRecord, collectiveService } from "@/services/collectiveService";
import { usePermissionScope, withinScope } from "@/services/authService";
import { errorMessage } from "@/components/admin/adminUtils";
import { Button } from "@/components/common/Button";
import { useToast } from "@/contexts/ToastContext";
import styles from "./Collective.module.css";

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
type Stage = "" | "B3A_COLLECTIVE" | "B4_DECISION";
type Vote = Omit<EvaluationMeetingVoteSummaryDto, "recordId">;

const UNIT_LABELS: Record<Unit, string> = { cell: "Tổ chức Đảng", department: "Phòng / đơn vị", company: "Cấp Công ty" };
const STAGE_LABELS: [Stage, string][] = [
  ["B3A_COLLECTIVE", "Tập thể lãnh đạo, quản lý (đề xuất)"],
  ["B4_DECISION", "Đảng ủy/Chi ủy cơ sở (quyết định)"],
  ["", "Không xác định"],
];

const VOTE_FIELDS: [keyof Vote, string][] = [
  ["votesExcellent", "Xuất sắc"],
  ["votesGood", "Tốt"],
  ["votesSatisfactory", "Hoàn thành"],
  ["votesUnsatisfactory", "Không HT"],
  ["votesNotRated", "Chưa ĐG"],
  ["invalidVotes", "Không hợp lệ"],
];

/** Giờ họp nhập và hiển thị theo giờ Việt Nam (như giờ in trên biên bản), không phụ thuộc múi giờ của máy. */
const VN_OFFSET = "+07:00";

/** ISO (UTC) → giá trị ô datetime-local theo giờ Việt Nam. */
const toLocalInput = (iso?: string | null) => {
  if (!iso) return "";
  const date = new Date(new Date(iso).getTime() + 7 * 60 * 60 * 1000);
  return date.toISOString().slice(0, 16);
};

const toIso = (local: string) => (local ? new Date(`${local}:00${VN_OFFSET}`).toISOString() : undefined);

const emptyVote: Vote = { votesExcellent: 0, votesGood: 0, votesSatisfactory: 0, votesUnsatisfactory: 0, votesNotRated: 0, invalidVotes: 0, notes: "" };

const totalOf = (vote: Vote) =>
  vote.votesExcellent + vote.votesGood + vote.votesSatisfactory + vote.votesUnsatisfactory + vote.votesNotRated + vote.invalidVotes;

/** Ô số phiếu: trống = chưa ghi (null). */
const toCount = (value: string): number | null => (value === "" ? null : Math.max(0, Number(value) || 0));

/** Lập/sửa biên bản hội nghị (Mẫu 12) hoặc biên bản kiểm phiếu (Mẫu 13) với đủ các mục của biểu mẫu gốc. */
export function MeetingEditor({ periodId, branches, departments, editing, onSaved, onCancel, showError }: Props) {
  const { confirm } = useToast();
  const [formCode, setFormCode] = useState<"M12" | "M13">("M12");
  const [unit, setUnit] = useState<Unit>("cell");
  const [partyCellId, setPartyCellId] = useState("");
  const [departmentId, setDepartmentId] = useState("");
  const [stage, setStage] = useState<Stage>("B4_DECISION");
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
  const [committee, setCommittee] = useState<MeetingAttendee[]>([]);
  const [ballots, setBallots] = useState<{ issued: number | null; collected: number | null; valid: number | null; invalid: number | null }>(
    { issued: null, collected: null, valid: null, invalid: null });
  const [minutes, setMinutes] = useState("");
  const [outcome, setOutcome] = useState("");
  const [records, setRecords] = useState<EvaluationRecordDto[]>([]);
  const [votes, setVotes] = useState<Record<string, Vote>>({});
  const [saving, setSaving] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  /** Nội dung lúc mở, để chỉ bật Lưu khi có thay đổi. */
  const [baseline, setBaseline] = useState<string | null>(null);
  const [baselineTick, setBaselineTick] = useState(0);

  // Chỉ cho chọn nơi được lập biên bản. Hội nghị quyết định là hội nghị cấp ủy: tổ chức Đảng hoặc cấp Công ty.
  const scope = usePermissionScope("meeting.manage");
  const cellOptions = withinScope(branches, scope, "partyCell");
  const departmentOptions = withinScope(departments, scope, "department");
  const unitsFor = (value: Stage): Unit[] => [
    ...(cellOptions.length > 0 ? (["cell"] as Unit[]) : []),
    ...(departmentOptions.length > 0 && value !== "B4_DECISION" ? (["department"] as Unit[]) : []),
    ...(scope?.isGlobal ? (["company"] as Unit[]) : []),
  ];
  const stageOptions = STAGE_LABELS.filter(([value]) => unitsFor(value).length > 0);
  const unitOptions = unitsFor(stage);

  useEffect(() => {
    if (editing || !scope) return;
    if (!stageOptions.some(([value]) => value === stage) && stageOptions.length > 0) {
      setStage(stageOptions[0][0]);
      return;
    }
    if (!unitOptions.includes(unit) && unitOptions.length > 0)
      setUnit(stage === "B3A_COLLECTIVE" && unitOptions.includes("department") ? "department" : unitOptions[0]);
    if (cellOptions.length === 1 && !partyCellId) setPartyCellId(cellOptions[0].id);
    if (departmentOptions.length === 1 && !departmentId) setDepartmentId(departmentOptions[0].id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editing, scope, stage, unit, cellOptions.length, departmentOptions.length]);

  useEffect(() => {
    if (!editing) {
      setBaselineTick((tick) => tick + 1);
      return;
    }
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
    const details = editing.details || { attendees: [], countingCommittee: [] };
    setChairTitle(details.chairTitle || "");
    setSecretaryTitle(details.secretaryTitle || "");
    setWorkingRules(details.workingRules || "");
    setReportingUnit(details.reportingUnit || "");
    setAttendees(details.attendees || []);
    setCommittee(details.countingCommittee || []);
    setBallots({
      issued: details.ballotsIssued ?? null,
      collected: details.ballotsCollected ?? null,
      valid: details.ballotsValid ?? null,
      invalid: details.ballotsInvalid ?? null,
    });
    setBaselineTick((tick) => tick + 1);
  }, [editing]);

  // Ô chọn đơn vị/biểu mẫu tự điền khi lập mới nên không tính là thay đổi.
  const snapshot = JSON.stringify({
    location, startedAt, endedAt, invited, present, absentReasons, chairName, chairTitle, secretaryName, secretaryTitle,
    workingRules, reportingUnit, attendees, committee, ballots, minutes, outcome, votes,
  });
  useEffect(() => {
    if (baselineTick > 0) setBaseline(snapshot);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [baselineTick]);
  const dirty = baseline !== null && snapshot !== baseline;

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

  const unitMissing = !editing && ((unit === "cell" && !partyCellId) || (unit === "department" && !departmentId));
  const presentTooMany = present > invited;
  const ballotsMismatch = ballots.collected != null && ballots.valid != null && ballots.invalid != null
    && ballots.valid + ballots.invalid !== ballots.collected;

  const cancel = () => {
    if (!dirty) {
      onCancel();
      return;
    }
    confirm({
      title: "Bỏ nội dung chưa lưu",
      message: "Nội dung biên bản vừa nhập chưa được lưu. Đóng và bỏ các thay đổi này?",
      confirmText: "Bỏ thay đổi",
      isDanger: true,
      onConfirm: onCancel,
    });
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitted(true);
    // Lỗi từng ô hiện ngay dưới ô.
    if (unitMissing || presentTooMany || ballotsMismatch) return;
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
        countingCommittee: committee.filter((a) => a.name.trim()),
        ballotsIssued: ballots.issued,
        ballotsCollected: ballots.collected,
        ballotsValid: ballots.valid,
        ballotsInvalid: ballots.invalid,
      },
      // Chỉ gửi hồ sơ đã nhập số phiếu (hồ sơ để trống không vào biên bản kiểm phiếu).
      voteSummaries: !editing && formCode === "M13"
        ? records.filter((record) => votes[record.id] && totalOf(votes[record.id]) > 0)
            .map((record) => ({ recordId: record.id, ...votes[record.id] }))
        : [],
    };
    try {
      const saved = editing ? await collectiveService.updateMeeting(editing.id, payload) : await collectiveService.createMeeting(payload);
      onSaved(saved);
    } catch (error: unknown) {
      showError(errorMessage(error, "Lưu biên bản không thành công."));
    } finally {
      setSaving(false);
    }
  };

  const setAttendee = (index: number, patch: Partial<MeetingAttendee>) =>
    setAttendees(attendees.map((a, i) => (i === index ? { ...a, ...patch } : a)));
  const setMember = (index: number, patch: Partial<MeetingAttendee>) =>
    setCommittee(committee.map((a, i) => (i === index ? { ...a, ...patch } : a)));

  if (!editing && scope && stageOptions.length === 0)
    return <div className="text-secondary small">Bạn chưa được giao lập biên bản hội nghị ở đơn vị nào.</div>;

  const removeButton = (label: string, onClick: () => void) => (
    <button type="button" className={`btn btn-sm btn-outline-danger ${styles.iconBtn}`} aria-label={label} title={label} onClick={onClick}>
      <i className="bi bi-x-lg" aria-hidden="true" />
    </button>
  );

  return (
    <form onSubmit={submit} noValidate>
      <section className={styles.section}>
        <h3 className={styles.legend}>Thông tin hội nghị</h3>
        <div className="row g-2">
          <div className="col-md-6">
            <label className="form-label" htmlFor="mt-form">Biểu mẫu</label>
            <select id="mt-form" className="form-select form-select-sm" value={formCode} disabled={!!editing} onChange={(e) => setFormCode(e.target.value as "M12" | "M13")}>
              <option value="M12">Mẫu 12 — Biên bản hội nghị</option>
              <option value="M13">Mẫu 13 — Biên bản kiểm phiếu</option>
            </select>
          </div>
          <div className="col-md-6">
            <label className="form-label" htmlFor="mt-stage">Hội nghị</label>
            <select id="mt-stage" className="form-select form-select-sm" value={stage} disabled={!!editing} onChange={(e) => setStage(e.target.value as Stage)}>
              {(editing ? STAGE_LABELS : stageOptions).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
            </select>
          </div>
          <div className="col-md-6">
            <label className="form-label" htmlFor="mt-unit">Đơn vị tổ chức</label>
            <select id="mt-unit" className="form-select form-select-sm" value={unit} disabled={!!editing} onChange={(e) => setUnit(e.target.value as Unit)}>
              {(editing ? (Object.keys(UNIT_LABELS) as Unit[]) : unitOptions).map((value) => <option key={value} value={value}>{UNIT_LABELS[value]}</option>)}
            </select>
          </div>
          {unit !== "company" && (
            <div className="col-md-6">
              <label className="form-label" htmlFor="mt-org">{unit === "cell" ? "Tổ chức Đảng" : "Phòng / đơn vị"}</label>
              {unit === "cell" ? (
                <select id="mt-org" className={`form-select form-select-sm${submitted && unitMissing ? " is-invalid" : ""}`} value={partyCellId} disabled={!!editing} onChange={(e) => setPartyCellId(e.target.value)}>
                  <option value="">Chọn tổ chức Đảng</option>
                  {(editing ? branches : cellOptions).map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
                </select>
              ) : (
                <select id="mt-org" className={`form-select form-select-sm${submitted && unitMissing ? " is-invalid" : ""}`} value={departmentId} disabled={!!editing} onChange={(e) => setDepartmentId(e.target.value)}>
                  <option value="">Chọn Phòng / đơn vị</option>
                  {(editing ? departments : departmentOptions).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
                </select>
              )}
              {submitted && unitMissing && <div className="invalid-feedback">{unit === "cell" ? "Hãy chọn tổ chức Đảng." : "Hãy chọn Phòng / đơn vị."}</div>}
            </div>
          )}
          <div className="col-12">
            <label className="form-label" htmlFor="mt-rules">Căn cứ Quy chế làm việc của</label>
            <input id="mt-rules" className="form-control form-control-sm" placeholder="… nhiệm kỳ …" value={workingRules} onChange={(e) => setWorkingRules(e.target.value)} />
          </div>
          <div className="col-md-6">
            <label className="form-label" htmlFor="mt-start">Bắt đầu</label>
            <input id="mt-start" type="datetime-local" className="form-control form-control-sm" value={startedAt} onChange={(e) => setStartedAt(e.target.value)} />
          </div>
          <div className="col-md-6">
            <label className="form-label" htmlFor="mt-end">Kết thúc</label>
            <input id="mt-end" type="datetime-local" className="form-control form-control-sm" value={endedAt} onChange={(e) => setEndedAt(e.target.value)} />
          </div>
          <div className="col-12">
            <label className="form-label" htmlFor="mt-location">Địa điểm</label>
            <input id="mt-location" className="form-control form-control-sm" value={location} onChange={(e) => setLocation(e.target.value)} />
          </div>
        </div>
      </section>

      <section className={styles.section}>
        <h3 className={styles.legend}>Thành phần</h3>
        <div className="row g-2">
          <div className="col-4">
            <label className="form-label" htmlFor="mt-invited">Triệu tập</label>
            <input id="mt-invited" type="number" min={0} inputMode="numeric" className="form-control form-control-sm" value={invited} onChange={(e) => setInvited(Number(e.target.value))} />
          </div>
          <div className="col-4">
            <label className="form-label" htmlFor="mt-present">Có mặt</label>
            <input id="mt-present" type="number" min={0} inputMode="numeric" className={`form-control form-control-sm${presentTooMany ? " is-invalid" : ""}`} value={present} onChange={(e) => setPresent(Number(e.target.value))} />
            {presentTooMany && <div className="invalid-feedback">Vượt số triệu tập.</div>}
          </div>
          <div className="col-4">
            <label className="form-label" htmlFor="mt-absent">Vắng mặt</label>
            <input id="mt-absent" className="form-control form-control-sm" readOnly value={Math.max(invited - present, 0)} />
          </div>
          <div className="col-12">
            <label className="form-label" htmlFor="mt-absent-reasons">Lý do vắng mặt</label>
            <input id="mt-absent-reasons" className="form-control form-control-sm" value={absentReasons} onChange={(e) => setAbsentReasons(e.target.value)} />
          </div>
          <div className="col-md-6">
            <label className="form-label" htmlFor="mt-chair">Chủ trì hội nghị</label>
            <input id="mt-chair" className="form-control form-control-sm mb-1" placeholder="Họ và tên" value={chairName} onChange={(e) => setChairName(e.target.value)} />
            <input className="form-control form-control-sm" placeholder="Chức vụ Đảng, chính quyền" aria-label="Chức vụ chủ trì" value={chairTitle} onChange={(e) => setChairTitle(e.target.value)} />
          </div>
          <div className="col-md-6">
            <label className="form-label" htmlFor="mt-secretary">Thư ký hội nghị</label>
            <input id="mt-secretary" className="form-control form-control-sm mb-1" placeholder="Họ và tên" value={secretaryName} onChange={(e) => setSecretaryName(e.target.value)} />
            <input className="form-control form-control-sm" placeholder="Chức vụ Đảng, chính quyền" aria-label="Chức vụ thư ký" value={secretaryTitle} onChange={(e) => setSecretaryTitle(e.target.value)} />
          </div>
          <div className="col-12">
            <div className="form-label">Cán bộ, đảng viên khác được cử tham dự</div>
            {attendees.map((attendee, index) => (
              <div key={index} className={styles.repeatRow}>
                <input className="form-control form-control-sm" placeholder="Họ và tên" aria-label="Họ và tên người dự" value={attendee.name} onChange={(e) => setAttendee(index, { name: e.target.value })} />
                <input className="form-control form-control-sm" placeholder="Chức vụ Đảng, chính quyền" aria-label="Chức vụ người dự" value={attendee.title} onChange={(e) => setAttendee(index, { title: e.target.value })} />
                {removeButton("Bỏ người dự", () => setAttendees(attendees.filter((_, i) => i !== index)))}
              </div>
            ))}
            <Button type="button" size="sm" variant="outline-secondary" icon="bi-plus-lg" onClick={() => setAttendees([...attendees, { name: "", title: "" }])}>
              Thêm người dự
            </Button>
          </div>
        </div>
      </section>

      <section className={styles.section}>
        <h3 className={styles.legend}>Nội dung</h3>
        <div className="row g-2">
          <div className="col-12">
            <label className="form-label" htmlFor="mt-reporting">Cơ quan, đơn vị báo cáo tình hình thực hiện nhiệm vụ trọng tâm</label>
            <input id="mt-reporting" className="form-control form-control-sm" placeholder="Để trống: tên đơn vị tổ chức hội nghị" value={reportingUnit} onChange={(e) => setReportingUnit(e.target.value)} />
          </div>
          <div className="col-12">
            <label className="form-label" htmlFor="mt-minutes">Diễn biến hội nghị</label>
            <textarea id="mt-minutes" className="form-control form-control-sm" rows={3} value={minutes} onChange={(e) => setMinutes(e.target.value)} />
          </div>
          <div className="col-12">
            <label className="form-label" htmlFor="mt-outcome">Kết quả hội nghị</label>
            <textarea id="mt-outcome" className="form-control form-control-sm" rows={2} value={outcome} onChange={(e) => setOutcome(e.target.value)} />
          </div>
        </div>
      </section>

      <section className={styles.section}>
        <h3 className={styles.legend}>Kiểm phiếu</h3>
        <div className="row g-2">
          <div className="col-12">
            <div className="form-label">Tổ kiểm phiếu <span className="text-secondary fw-normal">(người đầu tiên là Tổ trưởng)</span></div>
            {committee.map((member, index) => (
              <div key={index} className={styles.repeatRow}>
                <span className={styles.repeatRole}>{index === 0 ? "Tổ trưởng" : "Thành viên"}</span>
                <input className="form-control form-control-sm" placeholder="Họ và tên" aria-label="Họ và tên thành viên Tổ kiểm phiếu" value={member.name} onChange={(e) => setMember(index, { name: e.target.value })} />
                <input className="form-control form-control-sm" placeholder="Chức vụ Đảng, chính quyền" aria-label="Chức vụ thành viên Tổ kiểm phiếu" value={member.title} onChange={(e) => setMember(index, { title: e.target.value })} />
                {removeButton("Bỏ thành viên Tổ kiểm phiếu", () => setCommittee(committee.filter((_, i) => i !== index)))}
              </div>
            ))}
            <Button type="button" size="sm" variant="outline-secondary" icon="bi-plus-lg" onClick={() => setCommittee([...committee, { name: "", title: "" }])}>
              Thêm thành viên
            </Button>
          </div>
          {([
            ["issued", "Phiếu phát ra"],
            ["collected", "Phiếu thu về"],
            ["valid", "Phiếu hợp lệ"],
            ["invalid", "Không hợp lệ"],
          ] as [keyof typeof ballots, string][]).map(([key, label]) => (
            <div className="col-6 col-md-3" key={key}>
              <label className="form-label" htmlFor={`mt-ballots-${key}`}>{label}</label>
              <input id={`mt-ballots-${key}`} type="number" min={0} inputMode="numeric" placeholder="Chưa ghi"
                className={`form-control form-control-sm${ballotsMismatch && key !== "issued" ? " is-invalid" : ""}`}
                value={ballots[key] ?? ""}
                onChange={(e) => setBallots({ ...ballots, [key]: toCount(e.target.value) })} />
            </div>
          ))}
          {ballotsMismatch && <div className="col-12 small text-danger">Phiếu hợp lệ cộng phiếu không hợp lệ phải bằng phiếu thu về.</div>}

          {formCode === "M13" && !editing && (
            <div className="col-12">
              <div className="form-label">Số phiếu theo từng hồ sơ</div>
              {records.length === 0 ? (
                <div className="text-secondary small">Chọn đơn vị tổ chức hội nghị để hiện danh sách hồ sơ.</div>
              ) : (
                <div className={styles.voteList}>
                  {records.map((record) => {
                    const vote = votes[record.id] || emptyVote;
                    const update = (key: keyof Vote, value: number | string) => setVotes({ ...votes, [record.id]: { ...vote, [key]: value } });
                    return (
                      <div key={record.id} className={styles.voteRow}>
                        <div className="small fw-semibold mb-1">{record.fullName}</div>
                        <div className={styles.voteGrid}>
                          {VOTE_FIELDS.map(([key, label]) => (
                            <div key={key}>
                              <label htmlFor={`vote-${record.id}-${key}`} title={label}>{label}</label>
                              <input id={`vote-${record.id}-${key}`} type="number" min={0} inputMode="numeric" className="form-control form-control-sm"
                                value={vote[key] as number} onChange={(e) => update(key, Number(e.target.value))} />
                            </div>
                          ))}
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          )}
        </div>
      </section>

      <div className={styles.footer}>
        <Button type="button" variant="outline-secondary" onClick={cancel} disabled={saving}>Hủy</Button>
        <Button
          type="submit"
          variant="primary"
          icon="bi-check-lg"
          loading={saving}
          loadingText="Đang lưu..."
          disabled={editing ? !dirty : false}
          title={editing && !dirty ? "Chưa có thay đổi" : undefined}
        >
          Lưu
        </Button>
      </div>
    </form>
  );
}

export default MeetingEditor;
