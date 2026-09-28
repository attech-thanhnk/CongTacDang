"use client";

import { FormEvent, useEffect, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { PageHeader } from "@/components/common/PageHeader";
import { organizationService, BranchItem } from "@/services/organizationService";
import {
  CollectiveEvaluationRecordDto,
  EvaluationRecordDto,
  EvaluationMeetingDto,
  EvaluationMeetingVoteSummaryDto,
  EvaluationPeriodDto,
  SaveCollectiveEvaluationRequestDto,
  SaveEvaluationMeetingRequestDto,
  evaluationService,
} from "@/services/evaluationService";

const emptyCollectiveForm = {
  subjectName: "",
  strengths: "",
  limitations: "",
  causes: "",
  remediationPlan: "",
  generalCriteriaScore: 0,
  taskCriteriaScore: 0,
};

export default function CollectiveEvaluationsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const showError = toast.error;
  const showSuccess = toast.success;
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [periodId, setPeriodId] = useState("");
  const [branches, setBranches] = useState<BranchItem[]>([]);
  const [branchId, setBranchId] = useState("");
  const [form, setForm] = useState<"M06" | "M07" | "M08">("M07");
  const [records, setRecords] = useState<CollectiveEvaluationRecordDto[]>([]);
  const [meetings, setMeetings] = useState<EvaluationMeetingDto[]>([]);
  const [branchRecords, setBranchRecords] = useState<EvaluationRecordDto[]>([]);
  const [voteInputs, setVoteInputs] = useState<Record<string, Omit<EvaluationMeetingVoteSummaryDto, "recordId">>>({});
  const [collective, setCollective] = useState(emptyCollectiveForm);
  const [meeting, setMeeting] = useState({ location: "", chairName: "", secretaryName: "", minutesContent: "", outcomeContent: "" });
  const [invitedCount, setInvitedCount] = useState(0);
  const [presentCount, setPresentCount] = useState(0);
  const [meetingForm, setMeetingForm] = useState<"M12" | "M13">("M12");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const canUseModule =
    hasPermission("evaluation.read") ||
    hasPermission("collective.manage") ||
    hasPermission("meeting.read") ||
    hasPermission("meeting.manage");

  useEffect(() => {
    if (!canUseModule) {
      setLoading(false);
      return;
    }
    Promise.all([evaluationService.getPeriods(), organizationService.getBranches()])
      .then(([loadedPeriods, loadedBranches]) => {
        setPeriods(loadedPeriods);
        setBranches(loadedBranches);
        const active = loadedPeriods.find((item) => item.isActive) || loadedPeriods[0];
        if (active) setPeriodId(active.id);
      })
      .catch((error: any) => showError(error?.message || "Không thể tải dữ liệu đánh giá tập thể."))
      .finally(() => setLoading(false));
  }, [canUseModule, showError]);

  useEffect(() => {
    if (!periodId || !canUseModule) return;
    Promise.all([evaluationService.getCollectiveRecords(periodId), evaluationService.getMeetings(periodId)])
      .then(([loadedRecords, loadedMeetings]) => {
        setRecords(loadedRecords);
        setMeetings(loadedMeetings);
      })
      .catch((error: any) => showError(error?.message || "Không thể tải hồ sơ tập thể."));
  }, [periodId, canUseModule, showError]);

  useEffect(() => {
    if (!periodId || !branchId) {
      setBranchRecords([]);
      return;
    }
    evaluationService.getRecordsByBranch(periodId, branchId)
      .then((loadedRecords) => setBranchRecords(loadedRecords))
      .catch(() => setBranchRecords([]));
  }, [periodId, branchId]);

  const reload = async () => {
    if (!periodId) return;
    const [loadedRecords, loadedMeetings] = await Promise.all([
      evaluationService.getCollectiveRecords(periodId),
      evaluationService.getMeetings(periodId),
    ]);
    setRecords(loadedRecords);
    setMeetings(loadedMeetings);
  };

  const submitCollective = async (event: FormEvent) => {
    event.preventDefault();
    if (!periodId || !collective.subjectName.trim()) {
      showError("Cần chọn kỳ đánh giá và nhập tên tập thể/lĩnh vực.");
      return;
    }
    setSaving(true);
    const payload: SaveCollectiveEvaluationRequestDto = {
      periodId,
      form,
      partyCellId: branchId || undefined,
      subjectName: collective.subjectName,
      strengths: collective.strengths,
      limitations: collective.limitations,
      causes: collective.causes,
      previousRemediation: "",
      explanation: "",
      responsibilities: "",
      remediationPlan: collective.remediationPlan,
      generalCriteriaScore: Number(collective.generalCriteriaScore),
      taskCriteriaScore: Number(collective.taskCriteriaScore),
      selfProposedGrade: "HoanThanhTot",
      items: [],
    };
    try {
      await evaluationService.createCollectiveRecord(payload);
      setCollective(emptyCollectiveForm);
      await reload();
      showSuccess(`Đã lưu hồ sơ ${form}.`);
    } catch (error: any) {
      showError(error?.message || "Không thể lưu hồ sơ tập thể.");
    } finally {
      setSaving(false);
    }
  };

  const submitMeeting = async (event: FormEvent) => {
    event.preventDefault();
    if (!periodId || !branchId) {
      showError("Cần chọn kỳ đánh giá và Chi bộ cho biên bản.");
      return;
    }
    setSaving(true);
    const payload: SaveEvaluationMeetingRequestDto = {
      periodId,
      partyCellId: branchId,
      formCode: meetingForm,
      meetingType: "Hội nghị đánh giá, xếp loại cán bộ quý",
      location: meeting.location,
      startedAt: new Date().toISOString(),
      invitedCount,
      presentCount,
      absentCount: Math.max(invitedCount - presentCount, 0),
      absentReasons: "",
      chairName: meeting.chairName,
      secretaryName: meeting.secretaryName,
      minutesContent: meeting.minutesContent,
      outcomeContent: meeting.outcomeContent,
      voteCountingContent: "",
      voteSummaries: meetingForm === "M13"
        ? branchRecords.map((record) => ({
            recordId: record.id,
            votesExcellent: voteInputs[record.id]?.votesExcellent || 0,
            votesGood: voteInputs[record.id]?.votesGood || 0,
            votesSatisfactory: voteInputs[record.id]?.votesSatisfactory || 0,
            votesUnsatisfactory: voteInputs[record.id]?.votesUnsatisfactory || 0,
            invalidVotes: voteInputs[record.id]?.invalidVotes || 0,
            notes: voteInputs[record.id]?.notes || "",
          }))
        : [],
    };
    try {
      await evaluationService.createMeeting(payload);
      setMeeting({ location: "", chairName: "", secretaryName: "", minutesContent: "", outcomeContent: "" });
      await reload();
      showSuccess(`Đã lưu biên bản ${meetingForm}.`);
    } catch (error: any) {
      showError(error?.message || "Không thể lưu biên bản.");
    } finally {
      setSaving(false);
    }
  };

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
        subTitle="Quản lý hồ sơ M06-M08 và biên bản M12-M13 theo kỳ đánh giá."
        actions={
          <div className="collective-toolbar-controls">
            <select className="form-select form-select-sm" aria-label="Kỳ đánh giá" value={periodId} onChange={(event) => setPeriodId(event.target.value)}>
              {periods.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
            <select className="form-select form-select-sm" aria-label="Chi bộ" value={branchId} onChange={(event) => setBranchId(event.target.value)}>
              <option value="">Chọn Chi bộ</option>
              {branches.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
          </div>
        }
      />

      <div className="page-body">
        <div className="row g-3">
        <div className="col-12 col-xl-7">
          <section className="card border-0 shadow-sm h-100">
            <div className="card-header bg-white border-0 pt-4 px-4">
              <div className="d-flex justify-content-between align-items-center gap-2">
                <div><h2 className="h5 mb-1">Hồ sơ tập thể</h2><div className="text-secondary small">Tạo báo cáo tự đánh giá M06, M07 hoặc M08.</div></div>
                <select className="form-select form-select-sm w-auto" value={form} onChange={(event) => setForm(event.target.value as "M06" | "M07" | "M08")}>
                  <option value="M06">M06 - Tập thể/lĩnh vực</option>
                  <option value="M07">M07 - Đảng ủy/Chi bộ</option>
                  <option value="M08">M08 - Tổng hợp nhiệm vụ</option>
                </select>
                </div>
              </div>
            <div className="card-body px-4">
              <form onSubmit={submitCollective} className="row g-3">
                <div className="col-12"><label className="form-label">Tên tập thể/lĩnh vực</label><input className="form-control" value={collective.subjectName} onChange={(event) => setCollective({ ...collective, subjectName: event.target.value })} /></div>
                <div className="col-md-6"><label className="form-label">Điểm chung / 30</label><input type="number" min="0" max="30" step="0.5" className="form-control" value={collective.generalCriteriaScore} onChange={(event) => setCollective({ ...collective, generalCriteriaScore: Number(event.target.value) })} /></div>
                <div className="col-md-6"><label className="form-label">Điểm nhiệm vụ / 70</label><input type="number" min="0" max="70" step="0.5" className="form-control" value={collective.taskCriteriaScore} onChange={(event) => setCollective({ ...collective, taskCriteriaScore: Number(event.target.value) })} /></div>
                <div className="col-12"><label className="form-label">Ưu điểm, kết quả đạt được</label><textarea className="form-control" rows={3} value={collective.strengths} onChange={(event) => setCollective({ ...collective, strengths: event.target.value })} /></div>
                <div className="col-md-6"><label className="form-label">Hạn chế, khuyết điểm</label><textarea className="form-control" rows={3} value={collective.limitations} onChange={(event) => setCollective({ ...collective, limitations: event.target.value })} /></div>
                <div className="col-md-6"><label className="form-label">Nguyên nhân</label><textarea className="form-control" rows={3} value={collective.causes} onChange={(event) => setCollective({ ...collective, causes: event.target.value })} /></div>
                <div className="col-12"><label className="form-label">Phương hướng khắc phục</label><textarea className="form-control" rows={3} value={collective.remediationPlan} onChange={(event) => setCollective({ ...collective, remediationPlan: event.target.value })} /></div>
                <div className="col-12"><button className="btn btn-primary" disabled={saving}>{saving ? "Đang lưu..." : "Lưu hồ sơ tập thể"}</button></div>
              </form>
              <div className="mt-4 border-top pt-3">
                {records.length === 0 ? <div className="text-secondary small">Chưa có hồ sơ tập thể trong kỳ.</div> : records.map((record) => (
                  <div key={record.id} className="d-flex justify-content-between align-items-center border-bottom py-2 gap-3">
                    <div><strong>{record.subjectName}</strong><div className="text-secondary small">{record.form} · {record.partyCellName || record.departmentName || "Chưa gắn tổ chức"}</div></div>
                    <span className="badge text-bg-light">{record.totalScore.toFixed(1)} / 100</span>
                  </div>
                ))}
              </div>
            </div>
          </section>
        </div>

        <div className="col-12 col-xl-5">
          <section className="card border-0 shadow-sm mb-4">
            <div className="card-header bg-white border-0 pt-4 px-4"><h2 className="h5 mb-1">Biên bản hội nghị</h2><div className="text-secondary small">Mẫu 12 là biên bản họp; Mẫu 13 ghi tổng hợp kiểm phiếu.</div></div>
            <div className="card-body px-4">
              <form onSubmit={submitMeeting} className="row g-3">
                <div className="col-6"><label className="form-label">Biểu mẫu</label><select className="form-select" value={meetingForm} onChange={(event) => setMeetingForm(event.target.value as "M12" | "M13")}><option value="M12">M12 - Hội nghị</option><option value="M13">M13 - Kiểm phiếu</option></select></div>
                <div className="col-6"><label className="form-label">Địa điểm</label><input className="form-control" value={meeting.location} onChange={(event) => setMeeting({ ...meeting, location: event.target.value })} /></div>
                <div className="col-4"><label className="form-label">Triệu tập</label><input type="number" min="0" className="form-control" value={invitedCount} onChange={(event) => setInvitedCount(Number(event.target.value))} /></div>
                <div className="col-4"><label className="form-label">Có mặt</label><input type="number" min="0" className="form-control" value={presentCount} onChange={(event) => setPresentCount(Number(event.target.value))} /></div>
                <div className="col-4"><label className="form-label">Vắng</label><input className="form-control" value={Math.max(invitedCount - presentCount, 0)} readOnly /></div>
                <div className="col-md-6"><label className="form-label">Chủ trì</label><input className="form-control" value={meeting.chairName} onChange={(event) => setMeeting({ ...meeting, chairName: event.target.value })} /></div>
                <div className="col-md-6"><label className="form-label">Thư ký</label><input className="form-control" value={meeting.secretaryName} onChange={(event) => setMeeting({ ...meeting, secretaryName: event.target.value })} /></div>
                <div className="col-12"><label className="form-label">Nội dung biên bản</label><textarea className="form-control" rows={4} value={meeting.minutesContent} onChange={(event) => setMeeting({ ...meeting, minutesContent: event.target.value })} /></div>
                <div className="col-12"><label className="form-label">Kết quả hội nghị</label><textarea className="form-control" rows={3} value={meeting.outcomeContent} onChange={(event) => setMeeting({ ...meeting, outcomeContent: event.target.value })} /></div>
                {meetingForm === "M13" && <div className="col-12"><label className="form-label">Tổng hợp phiếu theo hồ sơ</label><div className="border rounded p-2" style={{ maxHeight: 220, overflowY: "auto" }}>
                  {branchRecords.length === 0 ? <div className="text-secondary small">Chưa tải được hồ sơ của Chi bộ đã chọn.</div> : branchRecords.map((record) => {
                    const vote = voteInputs[record.id] || { votesExcellent: 0, votesGood: 0, votesSatisfactory: 0, votesUnsatisfactory: 0, invalidVotes: 0, notes: "" };
                    const updateVote = (key: keyof Omit<EvaluationMeetingVoteSummaryDto, "recordId">, value: number | string) => setVoteInputs({ ...voteInputs, [record.id]: { ...vote, [key]: value } });
                    return <div key={record.id} className="border-bottom py-2"><div className="small fw-semibold mb-1">{record.fullName}</div><div className="row g-1"><div className="col"><input aria-label="Xuất sắc" type="number" min="0" className="form-control form-control-sm" placeholder="XS" value={vote.votesExcellent} onChange={(event) => updateVote("votesExcellent", Number(event.target.value))} /></div><div className="col"><input aria-label="Tốt" type="number" min="0" className="form-control form-control-sm" placeholder="Tốt" value={vote.votesGood} onChange={(event) => updateVote("votesGood", Number(event.target.value))} /></div><div className="col"><input aria-label="Đạt" type="number" min="0" className="form-control form-control-sm" placeholder="Đạt" value={vote.votesSatisfactory} onChange={(event) => updateVote("votesSatisfactory", Number(event.target.value))} /></div><div className="col"><input aria-label="Không đạt" type="number" min="0" className="form-control form-control-sm" placeholder="KĐ" value={vote.votesUnsatisfactory} onChange={(event) => updateVote("votesUnsatisfactory", Number(event.target.value))} /></div></div></div>;
                  })}
                </div></div>}
                <div className="col-12"><button className="btn btn-outline-primary" disabled={saving}>{saving ? "Đang lưu..." : "Lưu biên bản"}</button></div>
              </form>
            </div>
          </section>
          <section className="card border-0 shadow-sm"><div className="card-body px-4"><h2 className="h6">Biên bản đã lập</h2>{meetings.length === 0 ? <div className="text-secondary small">Chưa có biên bản trong kỳ.</div> : meetings.map((item) => <div key={item.id} className="border-bottom py-2"><strong>{item.formCode}</strong><div className="text-secondary small">{item.partyCellName || "Chi bộ"} · {item.location || "Chưa ghi địa điểm"}</div></div>)}</div></section>
       </div>
       </div>
       </div>
     </div>
  );
}
