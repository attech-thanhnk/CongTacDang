"use client";

import React, { useState, useEffect, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import {
  evaluationService,
  EvaluationPeriodDto,
  CreatePeriodDto,
  EvaluationRecordDto,
  TaskInputDto,
  BranchQuotaCheckDto,
} from "@/services/evaluationService";
import { useAuth } from "@/contexts/AuthContext";
import { EvaluationPdfModal } from "@/components/evaluations/EvaluationPdfModal";
import { PrintTemplateType } from "@/components/evaluations/EvaluationPrintTemplate";
import { DocumentViewerModal } from "@/components/attachments/DocumentViewerModal";
import { FileUploadModal } from "@/components/attachments/FileUploadModal";
import { Button } from "@/components/common";
import { useToast } from "@/contexts/ToastContext";
import { reportService } from "@/services/reportService";
import { auditService, EvaluationRecordHistoryDto } from "@/services/auditService";

// Modular Step Components
import { EvaluationPeriodHeader } from "@/components/evaluations/EvaluationPeriodHeader";
import { EvaluationStepNav } from "@/components/evaluations/EvaluationStepNav";
import { Step1RegisterTasks } from "@/components/evaluations/Step1RegisterTasks";
import { Step2SelfScore } from "@/components/evaluations/Step2SelfScore";
import { Step3BranchReview, BranchMeetingVoteState } from "@/components/evaluations/Step3BranchReview";
import { Step4Appraisal } from "@/components/evaluations/Step4Appraisal";
import { Step5Approval } from "@/components/evaluations/Step5Approval";
import { EvaluationHistoryModal } from "@/components/evaluations/EvaluationHistoryModal";

function EvaluationsContent() {
  const { user, hasPermission, hasRole } = useAuth();
  const { toast } = useToast();
  const router = useRouter();
  const searchParams = useSearchParams();
  const stepParam = searchParams.get("step");
  const periodParam = searchParams.get("periodId");
  const isAdmin = hasRole("QUAN_TRI_HE_THONG") || hasPermission("roles.manage");

  // State Chung & Kỳ đánh giá
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [selectedPeriodId, setSelectedPeriodId] = useState<string>("");
  const [activeStep, setActiveStep] = useState<number>(() => {
    const p = stepParam ? parseInt(stepParam, 10) : 1;
    if (p >= 1 && p <= 5) return p;
    return 1;
  });
  const [loading, setLoading] = useState<boolean>(true);
  const [actionLoading, setActionLoading] = useState<boolean>(false);

  // Hồ sơ cá nhân của Cán bộ đang đăng nhập
  const [myRecord, setMyRecord] = useState<EvaluationRecordDto | null>(null);

  // Form Bước 1: Đăng ký nhiệm vụ (Mẫu 01)
  const [registerTasks, setRegisterTasks] = useState<TaskInputDto[]>([]);

  // Form Bước 2: Tự chấm điểm (Mẫu 02 & 09)
  const [generalScores, setGeneralScores] = useState<number[]>([5.0, 5.0, 5.0, 5.0, 5.0, 5.0]);
  const [taskScoreRatios, setTaskScoreRatios] = useState<{
    [taskId: string]: { a: number; b: number; c: number; d: number; exceed: boolean; attachmentId?: string | null };
  }>({});
  const [selfProposedGrade, setSelfProposedGrade] = useState<string>("HoanThanhTot");

  // State Bước 3: Chi bộ đánh giá & Biên bản kiểm phiếu (Mẫu 11 & 13)
  const [branchRecords, setBranchRecords] = useState<EvaluationRecordDto[]>([]);
  const [meetingVotes, setMeetingVotes] = useState<{ [recordId: string]: BranchMeetingVoteState }>({});
  const [totalVoters, setTotalVoters] = useState<number>(0);

  // State Bước 4: Thẩm định & Trần 20% (Mẫu 03 & 15)
  const [allRecords, setAllRecords] = useState<EvaluationRecordDto[]>([]);
  const [branchQuotas, setBranchQuotas] = useState<BranchQuotaCheckDto[]>([]);
  const [selectedAppraisalRecord, setSelectedAppraisalRecord] = useState<EvaluationRecordDto | null>(null);
  const [appraisalScoreInput, setAppraisalScoreInput] = useState<number>(0);
  const [appraisalCommentInput, setAppraisalCommentInput] = useState<string>("");
  const [appraisalGradeInput, setAppraisalGradeInput] = useState<string>("HoanThanhTot");

  // State Bước 5: Chuẩn y của BTV Đảng ủy (Mẫu 07, 08, 14)
  const [selectedApprovalRecord, setSelectedApprovalRecord] = useState<EvaluationRecordDto | null>(null);
  const [finalScoreInput, setFinalScoreInput] = useState<number>(0);
  const [finalGradeInput, setFinalGradeInput] = useState<string>("HoanThanhTot");

  // State In / Xuất PDF chuẩn thể thức văn bản Đảng
  const [pdfModalOpen, setPdfModalOpen] = useState<boolean>(false);
  const [pdfTemplateType, setPdfTemplateType] = useState<PrintTemplateType>("individual");
  const [pdfRecord, setPdfRecord] = useState<EvaluationRecordDto | null>(null);

  // State Xem tệp minh chứng trực tiếp (Inline Document Viewer Modal)
  const [viewerOpen, setViewerOpen] = useState<boolean>(false);
  const [viewerAttachmentId, setViewerAttachmentId] = useState<string | null>(null);
  const [viewerFileName, setViewerFileName] = useState<string | null>(null);

  // State Upload Modal (Kéo thả đính kèm tệp minh chứng thực tế)
  const [uploadModalOpen, setUploadModalOpen] = useState<boolean>(false);
  const [uploadTargetTaskIndex, setUploadTargetTaskIndex] = useState<number | null>(null);
  const [uploadTargetTaskId, setUploadTargetTaskId] = useState<string | null>(null);
  const [uploadCurrentAttId, setUploadCurrentAttId] = useState<string | null>(null);
  const [uploadTaskTitle, setUploadTaskTitle] = useState<string>("");

  // Modal lịch sử chuyển trạng thái hồ sơ đánh giá
  const [historyModalOpen, setHistoryModalOpen] = useState<boolean>(false);
  const [historyRecord, setHistoryRecord] = useState<EvaluationRecordDto | null>(null);
  const [recordHistory, setRecordHistory] = useState<EvaluationRecordHistoryDto[]>([]);
  const [historyLoading, setHistoryLoading] = useState<boolean>(false);

  const openDocumentViewer = (id: string, name?: string | null) => {
    setViewerAttachmentId(id);
    setViewerFileName(name || "Tệp minh chứng");
    setViewerOpen(true);
  };

  const handleOpenUploadForStep1 = (taskIndex: number, currentAttId?: string | null) => {
    setUploadTargetTaskIndex(taskIndex);
    setUploadTargetTaskId(null);
    setUploadCurrentAttId(currentAttId || null);
    setUploadTaskTitle(registerTasks[taskIndex]?.taskName || `Nhiệm vụ ${taskIndex + 1}`);
    setUploadModalOpen(true);
  };

  const handleOpenUploadForStep2 = (taskId: string, currentAttId?: string | null) => {
    setUploadTargetTaskIndex(null);
    setUploadTargetTaskId(taskId);
    setUploadCurrentAttId(currentAttId || null);
    const t = myRecord?.tasks?.find((x) => x.id === taskId);
    setUploadTaskTitle(t?.taskName || "Nhiệm vụ chuyên môn");
    setUploadModalOpen(true);
  };

  const handleUploadSuccess = (att: any) => {
    if (uploadTargetTaskIndex !== null && uploadTargetTaskIndex >= 0) {
      const updated = [...registerTasks];
      updated[uploadTargetTaskIndex].attachmentId = att.id;
      updated[uploadTargetTaskIndex].attachmentFileName = att.fileName;
      setRegisterTasks(updated);
      setUploadTargetTaskIndex(null);
    } else if (uploadTargetTaskId && myRecord) {
      const prevRatio = taskScoreRatios[uploadTargetTaskId] || { a: 1, b: 1, c: 1, d: 1, exceed: false };
      setTaskScoreRatios({
        ...taskScoreRatios,
        [uploadTargetTaskId]: {
          ...prevRatio,
          attachmentId: att.id,
        },
      });
      const updatedTasks = myRecord.tasks.map((t) =>
        t.id === uploadTargetTaskId
          ? { ...t, attachmentId: att.id, attachmentFileName: att.fileName, attachmentOriginalName: att.fileName }
          : t
      );
      setMyRecord({ ...myRecord, tasks: updatedTasks });
      setUploadTargetTaskId(null);
    }
  };

  /** Mở timeline và tải lịch sử chuyển trạng thái của hồ sơ được chọn. */
  const openRecordHistory = async (record: EvaluationRecordDto) => {
    setHistoryRecord(record);
    setRecordHistory([]);
    setHistoryModalOpen(true);
    setHistoryLoading(true);
    try {
      const history = await auditService.getRecordHistory(record.id);
      setRecordHistory(history);
    } catch (err: any) {
      toast.error(err?.message || "Không thể tải lịch sử hồ sơ.");
    } finally {
      setHistoryLoading(false);
    }
  };

  const getStep1RecordForPdf = (): EvaluationRecordDto => {
    if (myRecord) {
      return {
        ...myRecord,
        tasks: registerTasks.map((t, idx) => ({
          id: (myRecord.tasks && myRecord.tasks[idx]?.id) || String(idx),
          recordId: myRecord.id,
          taskOrder: idx + 1,
          taskName: t.taskName,
          targetOutput: t.targetOutput,
          weight: t.weight,
          deadline: t.deadline,
          criteriaA_Ratio: 1,
          criteriaB_Ratio: 1,
          criteriaC_Ratio: 1,
          criteriaD_Ratio: 1,
          selfScore: t.weight,
          isExceedStandard: false,
        })),
      };
    }
    return {
      id: "temp-step1",
      periodId: selectedPeriodId,
      periodName: periods.find((p) => p.id === selectedPeriodId)?.name || "",
      memberId: user?.id || "",
      fullName: user?.fullName || "",
      partyRole: "Đảng viên",
      partyCellName: (user as any)?.partyCellName || (user as any)?.departmentName || "",
      departmentName: (user as any)?.departmentName || "",
      positionTitle: (user as any)?.positionTitle || "",
      jobGroup: "Chuyên môn",
      generalScores: [5, 5, 5, 5, 5, 5],
      generalCriteriaScore: 30,
      status: "Draft",
      statusDisplayName: "Bản nháp",
      tasks: registerTasks.map((t, idx) => ({
        id: String(idx),
        recordId: "temp-step1",
        taskOrder: idx + 1,
        taskName: t.taskName,
        targetOutput: t.targetOutput,
        weight: t.weight,
        deadline: t.deadline,
        criteriaA_Ratio: 1,
        criteriaB_Ratio: 1,
        criteriaC_Ratio: 1,
        criteriaD_Ratio: 1,
        selfScore: t.weight,
        isExceedStandard: false,
      })),
      tasksScore: registerTasks.reduce((sum, t) => sum + (t.weight || 0), 0),
      totalSelfScore: 30 + registerTasks.reduce((sum, t) => sum + (t.weight || 0), 0),
      selfProposedGrade: "HoanThanhTot",
      partyCellComment: "",
      partyCellProposedGrade: "",
      votesExcellent: 0,
      votesGood: 0,
      votesSatisfactory: 0,
      votesUnsatisfactory: 0,
      totalVoters: 0,
      appraisalComment: "",
      appraisalProposedGrade: "",
      finalScore: 0,
      finalGrade: "",
    } as unknown as EvaluationRecordDto;
  };

  const openEvaluationPdf = (rec: EvaluationRecordDto, templateType?: PrintTemplateType) => {
    setPdfRecord(rec);
    if (templateType) {
      setPdfTemplateType(templateType);
    }
    setPdfModalOpen(true);
  };

  const openIndividualPdf = (rec: EvaluationRecordDto) => {
    openEvaluationPdf(rec, "individual");
  };

  const handleSelectStep = (step: number) => {
    setActiveStep(step);
    const params = new URLSearchParams(searchParams ? searchParams.toString() : "");
    params.set("step", String(step));
    router.replace(`/evaluations?${params.toString()}`, { scroll: false });
  };

  // Lắng nghe thay đổi từ URL parameter ?step=
  useEffect(() => {
    if (stepParam) {
      const p = parseInt(stepParam, 10);
      if (p >= 1 && p <= 5 && p !== activeStep) {
        setActiveStep(p);
      }
    }
  }, [stepParam]);

  // Tải dữ liệu ban đầu
  useEffect(() => {
    if (!user) return;
    loadInitialData();
  }, [user]);

  // Đảm bảo an toàn: Nếu người dùng không có quyền truy cập bước hiện tại -> tự động chuyển về bước hợp lệ
  useEffect(() => {
    if (isAdmin) {
      // Quản trị viên chỉ xem bước 3, 4, 5 (không có hồ sơ cá nhân bước 1, 2)
      if (activeStep === 1 || activeStep === 2) {
        handleSelectStep(4);
      }
      return;
    }
    if (activeStep === 3 && !(hasPermission("evaluations.branch_vote") || hasPermission("evaluations.branch_review"))) {
      handleSelectStep(1);
    } else if (activeStep === 4 && !hasPermission("evaluations.appraise")) {
      handleSelectStep(1);
    } else if (activeStep === 5 && !hasPermission("evaluations.approve")) {
      handleSelectStep(1);
    }
  }, [activeStep, hasPermission, isAdmin]);

  const loadInitialData = async () => {
    if (!user) {
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const pList = await evaluationService.getPeriods();
      setPeriods(pList);
      const defaultPeriod =
        (periodParam ? pList.find((p) => p.id === periodParam) : null) ||
        pList.find((p) => p.isActive) ||
        pList[0];
      if (defaultPeriod) {
        setSelectedPeriodId(defaultPeriod.id);
        await loadPeriodData(defaultPeriod.id);
      }
    } catch (err: any) {
      toast.error(err.message || "Không thể tải danh sách kỳ đánh giá.");
    } finally {
      setLoading(false);
    }
  };

  const loadPeriodData = async (periodId: string) => {
    try {
      if (user) {
        const myRec = await evaluationService.getMyRecord(periodId);
        setMyRecord(myRec);

        if (myRec && myRec.tasks && myRec.tasks.length > 0) {
          setRegisterTasks(
            myRec.tasks.map((t) => ({
              taskName: t.taskName,
              targetOutput: t.targetOutput,
              weight: t.weight,
              deadline: t.deadline ? t.deadline.substring(0, 10) : "",
              attachmentId: t.attachmentId,
              attachmentFileName: t.attachmentFileName || t.attachmentOriginalName,
            }))
          );

          const initialRatios: {
            [taskId: string]: { a: number; b: number; c: number; d: number; exceed: boolean; attachmentId?: string | null };
          } = {};
          myRec.tasks.forEach((t) => {
            initialRatios[t.id] = {
              a: t.criteriaA_Ratio ?? 1.0,
              b: t.criteriaB_Ratio ?? 1.0,
              c: t.criteriaC_Ratio ?? 1.0,
              d: t.criteriaD_Ratio ?? 1.0,
              exceed: t.isExceedStandard ?? false,
              attachmentId: t.attachmentId,
            };
          });
          setTaskScoreRatios(initialRatios);
        }

        if (myRec?.generalScores && myRec.generalScores.length === 6) {
          setGeneralScores(myRec.generalScores);
        }
        if (myRec?.selfProposedGrade) {
          setSelfProposedGrade(myRec.selfProposedGrade);
        }
      }

      if (hasPermission("evaluations.branch_vote") || hasPermission("evaluations.branch_review") || isAdmin) {
        const bRecs = await evaluationService.getBranchRecords(periodId);
        setBranchRecords(bRecs);
        const initialVotes: { [recordId: string]: BranchMeetingVoteState } = {};
        let maxVoters = 0;
        bRecs.forEach((r) => {
          initialVotes[r.id] = {
            votesExcellent: r.votesExcellent || 0,
            votesGood: r.votesGood || 0,
            votesSatisfactory: r.votesSatisfactory || 0,
            votesUnsatisfactory: r.votesUnsatisfactory || 0,
            proposedGrade: r.partyCellProposedGrade || "HoanThanhTot",
            comment: r.partyCellComment || "",
          };
          if ((r.totalVoters || 0) > maxVoters) {
            maxVoters = r.totalVoters || 0;
          }
        });
        setMeetingVotes(initialVotes);
        if (maxVoters > 0) setTotalVoters(maxVoters);
      }

      if (hasPermission("evaluations.appraise") || hasPermission("evaluations.approve") || isAdmin) {
        const aRecs = await evaluationService.getAllRecords(periodId);
        setAllRecords(aRecs);
        const qList = await evaluationService.checkBranchQuotas(periodId);
        setBranchQuotas(qList);

        if (aRecs.length > 0) {
          setSelectedAppraisalRecord(aRecs[0]);
          setAppraisalScoreInput(aRecs[0].appraisalScore ?? aRecs[0].totalSelfScore ?? 0);
          setAppraisalCommentInput(aRecs[0].appraisalComment || "");
          setAppraisalGradeInput(aRecs[0].appraisalProposedGrade || "HoanThanhTot");

          setSelectedApprovalRecord(aRecs[0]);
          setFinalScoreInput(aRecs[0].finalScore ?? aRecs[0].appraisalScore ?? aRecs[0].totalSelfScore ?? 0);
          setFinalGradeInput(aRecs[0].finalGrade || "HoanThanhTot");
        }
      }
    } catch (err: any) {
      toast.error(err.message || "Không thể tải dữ liệu của kỳ này.");
    }
  };

  const handlePeriodChange = async (periodId: string) => {
    setSelectedPeriodId(periodId);
    setLoading(true);
    await loadPeriodData(periodId);
    setLoading(false);
  };

  // Nộp Bước 1: Đăng ký sản phẩm, công việc chuyên môn
  const handleSubmitStep1 = async (tasksOverride?: TaskInputDto[]) => {
    if (!selectedPeriodId) {
      toast.warning("Vui lòng chọn kỳ đánh giá.");
      return;
    }
    setActionLoading(true);
    try {
      const sourceTasks = tasksOverride ?? registerTasks;
      const formattedTasks = sourceTasks.map((t, idx) => ({
        ...t,
        taskOrder: idx + 1,
        weight: Number(t.weight) || 0,
        deadline: t.deadline && !isNaN(Date.parse(t.deadline))
          ? new Date(t.deadline).toISOString()
          : new Date().toISOString(),
      }));

      const rec = await evaluationService.registerTasks({
        periodId: selectedPeriodId,
        tasks: formattedTasks,
      });
      setMyRecord(rec);
      toast.success("Đã đăng ký danh mục sản phẩm, công việc chuyên môn thành công!");
      await loadPeriodData(selectedPeriodId);
      setActiveStep(2);
    } catch (err: any) {
      toast.error(err.message || "Đăng ký công việc thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  // Nộp Bước 2 (Tự chấm điểm cá nhân)
  const handleSubmitStep2 = async () => {
    if (!myRecord) return;
    setActionLoading(true);
    try {
      const taskScores = (myRecord.tasks || []).map((t) => {
        const ratio = taskScoreRatios[t.id] || { a: 1, b: 1, c: 1, d: 1, exceed: false };
        return {
          taskId: t.id,
          criteriaA_Ratio: ratio.a,
          criteriaB_Ratio: ratio.b,
          criteriaC_Ratio: ratio.c,
          criteriaD_Ratio: ratio.d,
          isExceedStandard: ratio.exceed,
          attachmentId: ratio.attachmentId || t.attachmentId,
        };
      });

      const updated = await evaluationService.submitSelfScore({
        recordId: myRecord.id,
        generalScores,
        taskScores,
        selfProposedGrade,
      });

      setMyRecord(updated);
      toast.success("Đã lưu kết quả tự chấm điểm cá nhân thành công!");
      await loadPeriodData(selectedPeriodId);
    } catch (err: any) {
      toast.error(err.message || "Lưu tự chấm điểm thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  const handleChangeMemberVote = (
    recordId: string,
    field: keyof BranchMeetingVoteState,
    value: any
  ) => {
    setMeetingVotes((prev) => ({
      ...prev,
      [recordId]: {
        ...(prev[recordId] || {
          votesExcellent: 0,
          votesGood: 0,
          votesSatisfactory: 0,
          votesUnsatisfactory: 0,
          proposedGrade: "HoanThanhTot",
          comment: "",
        }),
        [field]: value,
      },
    }));
  };

  // Nộp Bước 3: Toàn bộ Biên bản kiểm phiếu Chi bộ
  const handleSubmitStep3Meeting = async () => {
    if (!branchRecords.length) return;
    if (!totalVoters || totalVoters <= 0) {
      toast.warning("Vui lòng nhập Tổng số đảng viên dự họp (Cử tri) hợp lệ lớn hơn 0.");
      return;
    }
    setActionLoading(true);
    try {
      const partyCellId = branchRecords[0]?.partyCellId || "";
      const memberVotes = branchRecords.map((r) => {
        const v = meetingVotes[r.id] || {
          votesExcellent: r.votesExcellent || 0,
          votesGood: r.votesGood || 0,
          votesSatisfactory: r.votesSatisfactory || 0,
          votesUnsatisfactory: r.votesUnsatisfactory || 0,
          proposedGrade: r.partyCellProposedGrade || "HoanThanhTot",
          comment: r.partyCellComment || "",
        };
        return {
          recordId: r.id,
          comment: v.comment,
          proposedGrade: v.proposedGrade,
          votesExcellent: v.votesExcellent,
          votesGood: v.votesGood,
          votesSatisfactory: v.votesSatisfactory,
          votesUnsatisfactory: v.votesUnsatisfactory,
        };
      });

      await evaluationService.submitBranchMeeting({
        periodId: selectedPeriodId,
        partyCellId,
        totalVoters,
        memberVotes,
      });

      toast.success("Đã lưu kết quả Biên bản kiểm phiếu Chi bộ thành công!");
      await loadPeriodData(selectedPeriodId);
    } catch (err: any) {
      toast.error(err.message || "Lưu kết quả kiểm phiếu Chi bộ thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  // Nộp Bước 4: Thẩm định hồ sơ
  const handleSubmitStep4 = async () => {
    if (!selectedAppraisalRecord) return;
    setActionLoading(true);
    try {
      await evaluationService.submitAppraisal({
        recordId: selectedAppraisalRecord.id,
        appraisalScore: appraisalScoreInput,
        comment: appraisalCommentInput,
        proposedGrade: appraisalGradeInput,
      });
      toast.success(`Đã ghi nhận kết quả thẩm định cho đồng chí ${selectedAppraisalRecord.fullName}!`);
      await loadPeriodData(selectedPeriodId);
    } catch (err: any) {
      toast.error(err.message || "Thẩm định thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  // Nộp Bước 5: Chuẩn y & Phê duyệt xếp loại
  const handleSubmitStep5 = async () => {
    if (!selectedApprovalRecord) return;
    setActionLoading(true);
    try {
      await evaluationService.approveEvaluation({
        recordId: selectedApprovalRecord.id,
        finalScore: finalScoreInput,
        finalGrade: finalGradeInput,
      });
      toast.success(`Đã chuẩn y xếp loại chính thức cho đồng chí ${selectedApprovalRecord.fullName}!`);
      await loadPeriodData(selectedPeriodId);
    } catch (err: any) {
      toast.error(err.message || "Chuẩn y xếp loại thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  // Khởi tạo kỳ đánh giá mới
  const handleCreatePeriod = async (dto: CreatePeriodDto, setAsActive: boolean) => {
    setActionLoading(true);
    try {
      const created = await evaluationService.createPeriod(dto);
      if (setAsActive && created.id) {
        await evaluationService.setActivePeriod(created.id);
      }
      toast.success(`Đã khởi tạo thành công kỳ đánh giá ${created.name}!`);
      const updatedPeriods = await evaluationService.getPeriods();
      setPeriods(updatedPeriods);
      setSelectedPeriodId(created.id);
      await loadPeriodData(created.id);
    } catch (err: any) {
      toast.error(err.message || "Khởi tạo kỳ đánh giá thất bại.");
      throw err;
    } finally {
      setActionLoading(false);
    }
  };

  // Kích hoạt kỳ đánh giá làm kỳ hiện hành
  const handleSetActivePeriod = async (periodId: string) => {
    setActionLoading(true);
    try {
      await evaluationService.setActivePeriod(periodId);
      toast.success("Đã kích hoạt kỳ đánh giá làm kỳ hiện hành!");
      const updatedPeriods = await evaluationService.getPeriods();
      setPeriods(updatedPeriods);
      setSelectedPeriodId(periodId);
      await loadPeriodData(periodId);
    } catch (err: any) {
      toast.error(err.message || "Kích hoạt kỳ đánh giá thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  // Handlers xuất biểu mẫu chuẩn Đảng (.docx / .xlsx)
  const handleExportMau01Docx = async () => {
    if (!myRecord?.id) {
      toast.warning("Chưa có dữ liệu hồ sơ cá nhân để xuất Mẫu 01.");
      return;
    }
    try {
      toast.info("Đang tạo tệp Mẫu 01 (.docx) chuẩn thể thức...");
      await reportService.exportMau01Docx(myRecord.id, myRecord.fullName);
      toast.success("Tải Mẫu 01 (.docx) thành công.");
    } catch (err: any) {
      toast.error("Không thể tải Mẫu 01: " + (err?.message || "Lỗi kết xuất"));
    }
  };

  const handleExportMau02Docx = async () => {
    if (!myRecord?.id) {
      toast.warning("Chưa có dữ liệu hồ sơ cá nhân để xuất Mẫu 02.");
      return;
    }
    try {
      toast.info("Đang tạo tệp Mẫu 02 (.docx) chuẩn thể thức...");
      await reportService.exportMau02Docx(myRecord.id, myRecord.fullName);
      toast.success("Tải Mẫu 02 (.docx) thành công.");
    } catch (err: any) {
      toast.error("Không thể tải Mẫu 02: " + (err?.message || "Lỗi kết xuất"));
    }
  };

  const handleExportMau10Docx = async (rec: EvaluationRecordDto) => {
    if (!rec?.id) return;
    try {
      toast.info(`Đang tạo Phiếu thẩm định Mẫu 10 (.docx) cho ${rec.fullName}...`);
      await reportService.exportMau10Docx(rec.id, rec.fullName);
      toast.success("Tải Mẫu 10 (.docx) thành công.");
    } catch (err: any) {
      toast.error("Không thể tải Mẫu 10: " + (err?.message || "Lỗi kết xuất"));
    }
  };

  const handleExportMau11Docx = async () => {
    const period = periods.find((p) => p.id === selectedPeriodId);
    if (!period?.id) {
      toast.warning("Chưa chọn kỳ đánh giá.");
      return;
    }
    const branchId = myRecord?.partyCellId || branchRecords[0]?.partyCellId;
    const branchName = myRecord?.partyCellName || branchRecords[0]?.partyCellName;
    try {
      toast.info("Đang tạo Phiếu bỏ phiếu Chi bộ Mẫu 11 (.docx)...");
      await reportService.exportMau11Docx(period.id, branchId, branchName);
      toast.success("Tải Mẫu 11 (.docx) thành công.");
    } catch (err: any) {
      toast.error("Không thể tải Mẫu 11: " + (err?.message || "Lỗi kết xuất"));
    }
  };

  const handleExportMau13Docx = async () => {
    const period = periods.find((p) => p.id === selectedPeriodId);
    if (!period?.id) {
      toast.warning("Chưa chọn kỳ đánh giá.");
      return;
    }
    const branchId = myRecord?.partyCellId || branchRecords[0]?.partyCellId;
    const branchName = myRecord?.partyCellName || branchRecords[0]?.partyCellName;
    try {
      toast.info("Đang tạo Biên bản kiểm phiếu Chi bộ Mẫu 13 (.docx)...");
      await reportService.exportMau13Docx(period.id, branchId, branchName);
      toast.success("Tải Mẫu 13 (.docx) thành công.");
    } catch (err: any) {
      toast.error("Không thể tải Mẫu 13: " + (err?.message || "Lỗi kết xuất"));
    }
  };

  const handleExportMau14Excel = async () => {
    try {
      toast.info("Đang xuất Bảng tổng hợp Mẫu 14 (.xlsx)...");
      await reportService.exportForm14();
      toast.success("Tải Mẫu 14 (.xlsx) thành công.");
    } catch (err: any) {
      toast.error("Không thể tải Mẫu 14: " + (err?.message || "Lỗi kết xuất"));
    }
  };

  const handleExportMau15Excel = async () => {
    try {
      toast.info("Đang xuất Bảng kiểm soát trần 20% Mẫu 15A (.xlsx)...");
      await reportService.exportForm15();
      toast.success("Tải Mẫu 15A (.xlsx) thành công.");
    } catch (err: any) {
      toast.error("Không thể tải Mẫu 15A: " + (err?.message || "Lỗi kết xuất"));
    }
  };

  const activePeriod = periods.find((p) => p.id === selectedPeriodId);

  return (
    <div className="page-wrapper">
      {/* 1. Header Kỳ đánh giá & Bộ chọn */}
      <EvaluationPeriodHeader
        periods={periods}
        selectedPeriodId={selectedPeriodId}
        onSelectPeriod={handlePeriodChange}
        activePeriod={activePeriod}
        myRecord={myRecord}
        onOpenPdf={openIndividualPdf}
        onOpenHistory={openRecordHistory}
        onRefresh={() => loadPeriodData(selectedPeriodId)}
        loading={loading}
        canManagePeriods={hasPermission("evaluations.approve") || isAdmin}
        onCreatePeriod={handleCreatePeriod}
        onSetActivePeriod={handleSetActivePeriod}
      />

      {/* Thanh điều hướng 5 bước quy trình: chỉ hiển thị cho Cán bộ / Chi bộ / Thẩm định / BTV tham gia đánh giá */}
      {!isAdmin && (
        <EvaluationStepNav
          activeStep={activeStep}
          onSelectStep={handleSelectStep}
          myRecord={myRecord}
          hasPermission={hasPermission}
          branchRecordsCount={branchRecords.length}
          allRecordsCount={allRecords.length}
        />
      )}

      {/* Nội dung trang: padding trên-dưới-trái-phải đồng nhất như mọi trang */}
      <div className="page-body">
        {/* Nội dung trang: Phân định rõ ràng giữa Quản trị viên (Giám sát) và Cán bộ (Quy trình 5 bước) */}
        {loading ? (
          <div className="card border-0 shadow-sm p-5 text-center bg-white">
            <div className="spinner-border text-primary mx-auto mb-3" role="status">
              <span className="visually-hidden">Đang tải...</span>
            </div>
            <div className="text-secondary small">Đang tải dữ liệu...</div>
          </div>
        ) : isAdmin ? (
          /* ================================================================ */
          /* CHẾ ĐỘ QUẢN TRỊ VIÊN HỆ THỐNG: BẢNG GIÁM SÁT TOÀN BỘ HỒ SƠ      */
          /* ================================================================ */
          <div
            style={{
              background: "var(--bg-card)",
              border: "1px solid var(--border-base)",
              borderRadius: "var(--radius-lg)",
              padding: "16px 20px",
              boxShadow: "var(--shadow-sm)",
            }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "14px" }}>
              <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                <h2 style={{ fontSize: "15px", fontWeight: 700, margin: 0, color: "var(--text-primary)" }}>
                  Tiến độ hồ sơ đánh giá
                </h2>
                <span className="badge bg-light text-secondary border fw-normal" style={{ fontSize: "12px" }}>
                  {allRecords.length} hồ sơ
                </span>
              </div>
            </div>

            <div style={{ overflowX: "auto" }}>
              <table className="table table-hover align-middle mb-0" style={{ fontSize: "13px" }}>
                <thead style={{ background: "var(--bg-base)" }}>
                  <tr>
                    <th style={{ width: "45px", textAlign: "center" }}>STT</th>
                    <th>Cán bộ, Đảng viên</th>
                    <th>Chi bộ sinh hoạt</th>
                    <th>Chức vụ / Vị trí</th>
                    <th style={{ textAlign: "center" }}>M01 Đăng ký</th>
                    <th style={{ textAlign: "center" }}>M02 Tự chấm</th>
                    <th style={{ textAlign: "center" }}>M10 Chi bộ</th>
                    <th style={{ textAlign: "center" }}>M03 Thẩm định</th>
                    <th style={{ textAlign: "center" }}>M07 Chuẩn y</th>
                    <th style={{ textAlign: "center" }}>Trạng thái</th>
                    <th style={{ textAlign: "center", width: "80px" }}>Bản in</th>
                  </tr>
                </thead>
                <tbody>
                  {allRecords.length === 0 ? (
                    <tr>
                      <td colSpan={11} style={{ textAlign: "center", padding: "24px", color: "var(--text-muted)" }}>
                        Chưa có hồ sơ nào trong kỳ đánh giá này.
                      </td>
                    </tr>
                  ) : (
                    allRecords.map((rec, idx) => (
                      <tr key={rec.id}>
                        <td style={{ textAlign: "center", color: "var(--text-muted)" }}>{idx + 1}</td>
                        <td style={{ fontWeight: 600, color: "var(--text-primary)" }}>
                          {rec.fullName}
                          {rec.partyCardNumber && (
                            <div style={{ fontSize: "11px", color: "var(--text-secondary)", fontWeight: 400 }}>
                              Số thẻ: {rec.partyCardNumber}
                            </div>
                          )}
                        </td>
                        <td>{rec.partyCellName || "—"}</td>
                        <td style={{ fontSize: "12px", color: "var(--text-secondary)" }}>{rec.positionTitle || "Cán bộ"}</td>
                        <td style={{ textAlign: "center" }}>
                          {(rec.tasks?.length || 0) > 0 ? (
                            <span className="badge bg-success">{rec.tasks.length} việc</span>
                          ) : (
                            <span className="badge bg-light text-muted">Chưa nộp</span>
                          )}
                        </td>
                        <td style={{ textAlign: "center", fontWeight: 600 }}>
                          {(rec.totalSelfScore || 0) > 0 ? `${rec.totalSelfScore}đ` : "—"}
                        </td>
                        <td style={{ textAlign: "center" }}>
                          {rec.partyCellProposedGrade || "—"}
                        </td>
                        <td style={{ textAlign: "center", fontWeight: 600, color: "var(--color-cobalt)" }}>
                          {rec.appraisalScore ? `${rec.appraisalScore}đ` : "—"}
                        </td>
                        <td style={{ textAlign: "center", fontWeight: 700, color: "var(--color-success)" }}>
                          {rec.finalGrade || "—"}
                        </td>
                        <td style={{ textAlign: "center" }}>
                          <span className="badge bg-primary-subtle text-primary border border-primary-subtle">
                            {rec.statusDisplayName || "Đang xử lý"}
                          </span>
                        </td>
                        <td style={{ textAlign: "center" }}>
                          <div className="d-flex justify-content-center gap-1">
                            <button
                              type="button"
                              className="btn btn-sm btn-outline-primary py-0 px-2"
                              style={{ fontSize: "11px" }}
                              onClick={() => reportService.exportMau02Docx(rec.id, rec.fullName)}
                              title="Tải Mẫu 02 (.docx) của cán bộ này"
                            >
                              <i className="bi bi-file-earmark-word me-0.5"></i>Word
                            </button>
                            <button
                              type="button"
                              className="btn btn-sm btn-outline-secondary py-0 px-2"
                              style={{ fontSize: "11px" }}
                              onClick={() => openIndividualPdf(rec)}
                              title="Xem bản in hồ sơ đánh giá (PDF)"
                            >
                              <i className="bi bi-file-earmark-pdf me-0.5"></i>PDF
                            </button>
                            <button
                              type="button"
                              className="btn btn-sm btn-outline-secondary py-0 px-2"
                              style={{ fontSize: "11px" }}
                              onClick={() => openRecordHistory(rec)}
                              title="Xem lịch sử thao tác hồ sơ"
                            >
                              <i className="bi bi-clock-history"></i>
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        ) : (
          /* ================================================================ */
          /* CHẾ ĐỘ CÁN BỘ / ĐẢNG VIÊN / LÃNH ĐẠO: QUY TRÌNH 5 BƯỚC          */
          /* ================================================================ */
          <>
            {/* Bước 1: Mẫu 01 */}
            {activeStep === 1 && hasPermission("evaluations.register") && (
              <Step1RegisterTasks
                tasks={registerTasks}
                onChangeTasks={setRegisterTasks}
                onSubmit={handleSubmitStep1}
                isSubmitting={actionLoading}
                isLocked={myRecord?.status === "Approved"}
                onOpenUploadModal={handleOpenUploadForStep1}
                onOpenDocViewer={openDocumentViewer}
                onOpenPdf={() => openEvaluationPdf(getStep1RecordForPdf(), "mau01")}
                onExportDocx={handleExportMau01Docx}
              />
            )}

            {/* Bước 2: Mẫu 02 & 09 */}
            {activeStep === 2 && hasPermission("evaluations.self_score") && myRecord && (
              <Step2SelfScore
                myRecord={myRecord}
                generalScores={generalScores}
                onChangeGeneralScores={setGeneralScores}
                taskScoreRatios={taskScoreRatios}
                onChangeTaskRatio={(taskId, field, val) => {
                  const prev = taskScoreRatios[taskId] || { a: 1, b: 1, c: 1, d: 1, exceed: false };
                  setTaskScoreRatios({
                    ...taskScoreRatios,
                    [taskId]: { ...prev, [field]: val },
                  });
                }}
                selfProposedGrade={selfProposedGrade}
                onChangeProposedGrade={setSelfProposedGrade}
                onSubmit={handleSubmitStep2}
                isSubmitting={actionLoading}
                onOpenUploadModal={handleOpenUploadForStep2}
                onOpenDocViewer={openDocumentViewer}
                onOpenPdf={(rec: EvaluationRecordDto) => openEvaluationPdf(rec || myRecord, "mau02")}
                onExportDocx={handleExportMau02Docx}
              />
            )}

            {/* Bước 3: Mẫu 11, 12, 13 (Chi bộ đánh giá) */}
            {/* Bước 3: Mẫu 11 & Mẫu 13 (Chi bộ đánh giá & Biên bản kiểm phiếu) */}
            {activeStep === 3 && (hasPermission("evaluations.branch_vote") || hasPermission("evaluations.branch_review") || isAdmin) && (
              <Step3BranchReview
                records={branchRecords}
                totalVoters={totalVoters}
                onChangeTotalVoters={setTotalVoters}
                meetingVotes={meetingVotes}
                onChangeMemberVote={handleChangeMemberVote}
                onSubmitMeeting={handleSubmitStep3Meeting}
                isSubmitting={actionLoading}
                onOpenDocViewer={openDocumentViewer}
                onExportMau11Docx={handleExportMau11Docx}
                onExportMau13Docx={handleExportMau13Docx}
              />
            )}

            {/* Bước 4: Mẫu 10 & 03 (Tổ Thẩm định) */}
            {activeStep === 4 && hasPermission("evaluations.appraise") && (
              <Step4Appraisal
                records={allRecords}
                quotas={branchQuotas}
                selectedRecord={selectedAppraisalRecord}
                onSelectRecord={(rec) => {
                  setSelectedAppraisalRecord(rec);
                  setAppraisalScoreInput(rec.appraisalScore || rec.totalSelfScore || 90);
                  setAppraisalCommentInput(rec.appraisalComment || "");
                  setAppraisalGradeInput(rec.appraisalProposedGrade || "HoanThanhTot");
                }}
                appraisalScore={appraisalScoreInput}
                onChangeAppraisalScore={setAppraisalScoreInput}
                appraisalComment={appraisalCommentInput}
                onChangeAppraisalComment={setAppraisalCommentInput}
                appraisalGrade={appraisalGradeInput}
                onChangeAppraisalGrade={setAppraisalGradeInput}
                onSubmit={handleSubmitStep4}
                isSubmitting={actionLoading}
                onOpenDocViewer={openDocumentViewer}
                onOpenPdf={(rec, template) => openEvaluationPdf(rec, template || "individual")}
                onExportMau10Docx={(rec) => handleExportMau10Docx(rec || selectedAppraisalRecord)}
              />
            )}

            {/* Bước 5: Mẫu 14 & 15A (Ban Thường vụ Chuẩn y) */}
            {activeStep === 5 && hasPermission("evaluations.approve") && (
              <Step5Approval
                records={allRecords}
                selectedRecord={selectedApprovalRecord}
                onSelectRecord={(rec) => {
                  setSelectedApprovalRecord(rec);
                  setFinalScoreInput(rec.finalScore || rec.appraisalScore || rec.totalSelfScore || 90);
                  setFinalGradeInput(rec.finalGrade || "HoanThanhTot");
                }}
                finalScore={finalScoreInput}
                onChangeFinalScore={setFinalScoreInput}
                finalGrade={finalGradeInput}
                onChangeFinalGrade={setFinalGradeInput}
                onSubmit={handleSubmitStep5}
                isSubmitting={actionLoading}
                onOpenPdf={(rec) => openEvaluationPdf(rec, "individual")}
                onExportMau14Excel={handleExportMau14Excel}
                onExportMau15Excel={handleExportMau15Excel}
              />
            )}
          </>
      )}
      </div>{/* end page-body */}

      {/* Modal In PDF Thể thức Đảng */}
      {(pdfRecord || periods.find((p) => p.id === selectedPeriodId)) && (
        <EvaluationPdfModal
          isOpen={pdfModalOpen}
          onClose={() => setPdfModalOpen(false)}
          record={pdfRecord}
          period={periods.find((p) => p.id === selectedPeriodId) || null}
          allRecords={allRecords}
          branchQuotas={branchQuotas}
          templateType={pdfTemplateType}
        />
      )}

      {/* Modal Xem trước Văn bản / Minh chứng Online */}
      <DocumentViewerModal
        isOpen={viewerOpen}
        onClose={() => {
          setViewerOpen(false);
          setViewerAttachmentId(null);
          setViewerFileName(null);
        }}
        attachmentId={viewerAttachmentId}
        attachmentFileName={viewerFileName}
      />

      {/* Modal Tải lên Minh chứng Thực tế kéo-thả */}
      <FileUploadModal
        isOpen={uploadModalOpen}
        onClose={() => {
          setUploadModalOpen(false);
          setUploadTargetTaskIndex(null);
          setUploadTargetTaskId(null);
          setUploadCurrentAttId(null);
        }}
        formCode="MAU01"
        targetTitle={uploadTaskTitle}
        currentAttachmentId={uploadCurrentAttId}
        onUploadSuccess={handleUploadSuccess}
      />

      <EvaluationHistoryModal
        isOpen={historyModalOpen}
        record={historyRecord}
        history={recordHistory}
        loading={historyLoading}
        onClose={() => {
          setHistoryModalOpen(false);
          setHistoryRecord(null);
          setRecordHistory([]);
        }}
      />
    </div>
  );
}

export default function EvaluationsPage() {
  return (
    <Suspense
      fallback={
        <div className="card border-0 shadow-sm p-5 text-center bg-white my-3">
          <div className="spinner-border text-primary mx-auto mb-3" role="status">
            <span className="visually-hidden">Đang tải...</span>
          </div>
          <div className="text-secondary small">Đang nạp dữ liệu đánh giá...</div>
        </div>
      }
    >
      <EvaluationsContent />
    </Suspense>
  );
}
