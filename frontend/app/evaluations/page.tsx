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

// Modular Step Components
import { EvaluationPeriodHeader } from "@/components/evaluations/EvaluationPeriodHeader";
import { EvaluationStepNav } from "@/components/evaluations/EvaluationStepNav";
import { Step1RegisterTasks } from "@/components/evaluations/Step1RegisterTasks";
import { Step2SelfScore } from "@/components/evaluations/Step2SelfScore";
import { Step3BranchReview } from "@/components/evaluations/Step3BranchReview";
import { Step4Appraisal } from "@/components/evaluations/Step4Appraisal";
import { Step5Approval } from "@/components/evaluations/Step5Approval";

function EvaluationsContent() {
  const { user, hasPermission, hasRole } = useAuth();
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
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

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

  // State Bước 3: Chi bộ đánh giá (Mẫu 10, 11, 13)
  const [branchRecords, setBranchRecords] = useState<EvaluationRecordDto[]>([]);
  const [selectedBranchRecord, setSelectedBranchRecord] = useState<EvaluationRecordDto | null>(null);
  const [branchComment, setBranchComment] = useState<string>("");
  const [branchGrade, setBranchGrade] = useState<string>("HoanThanhTot");
  const [votesExcellent, setVotesExcellent] = useState<number>(0);
  const [votesGood, setVotesGood] = useState<number>(0);
  const [votesSatisfactory, setVotesSatisfactory] = useState<number>(0);
  const [votesUnsatisfactory, setVotesUnsatisfactory] = useState<number>(0);
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
    setLoading(true);
    setErrorMsg(null);
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
      setErrorMsg(err.message || "Không thể tải danh sách kỳ đánh giá.");
    } finally {
      setLoading(false);
    }
  };

  const loadPeriodData = async (periodId: string) => {
    setErrorMsg(null);
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

      if (hasPermission("evaluations.branch_vote") || hasPermission("evaluations.branch_review")) {
        const bRecs = await evaluationService.getBranchRecords(periodId);
        setBranchRecords(bRecs);
        if (bRecs.length > 0) {
          setSelectedBranchRecord(bRecs[0]);
          setBranchComment(bRecs[0].partyCellComment || "");
          setBranchGrade(bRecs[0].partyCellProposedGrade || "HoanThanhTot");
          setVotesExcellent(bRecs[0].votesExcellent || 0);
          setVotesGood(bRecs[0].votesGood || 0);
          setVotesSatisfactory(bRecs[0].votesSatisfactory || 0);
          setVotesUnsatisfactory(bRecs[0].votesUnsatisfactory || 0);
          setTotalVoters(bRecs[0].totalVoters || 0);
        }
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

      if (hasPermission("evaluations.branch_vote") || hasPermission("evaluations.branch_review") || isAdmin) {
        const bRecs = await evaluationService.getBranchRecords(periodId);
        setBranchRecords(bRecs);
        if (bRecs.length > 0) {
          setSelectedBranchRecord(bRecs[0]);
          setBranchComment(bRecs[0].partyCellComment || "");
          setBranchGrade(bRecs[0].partyCellProposedGrade || "HoanThanhTot");
        }
      }
    } catch (err: any) {
      setErrorMsg(err.message || "Không thể tải dữ liệu của kỳ này.");
    }
  };

  const handlePeriodChange = async (periodId: string) => {
    setSelectedPeriodId(periodId);
    setLoading(true);
    await loadPeriodData(periodId);
    setLoading(false);
  };

  // Nộp Bước 1 (Mẫu 01)
  const handleSubmitStep1 = async () => {
    if (!selectedPeriodId) return;
    setActionLoading(true);
    setErrorMsg(null);
    setSuccessMsg(null);
    try {
      const rec = await evaluationService.registerTasks({
        periodId: selectedPeriodId,
        tasks: registerTasks,
      });
      setMyRecord(rec);
      setSuccessMsg("Đã đăng ký danh mục nhiệm vụ chuyên môn (Mẫu 01) thành công!");
      await loadPeriodData(selectedPeriodId);
      setActiveStep(2);
    } catch (err: any) {
      setErrorMsg(err.message || "Đăng ký nhiệm vụ thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  // Nộp Bước 2 (Mẫu 02)
  const handleSubmitStep2 = async () => {
    if (!myRecord) return;
    setActionLoading(true);
    setErrorMsg(null);
    setSuccessMsg(null);
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
      setSuccessMsg("Đã lưu kết quả tự kiểm điểm và chấm điểm cá nhân (Mẫu 02) thành công!");
      await loadPeriodData(selectedPeriodId);
    } catch (err: any) {
      setErrorMsg(err.message || "Lưu tự chấm điểm thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  // Nộp Bước 3 (Mẫu 10, 11, 13)
  const handleSubmitStep3 = async () => {
    if (!selectedBranchRecord) return;
    setActionLoading(true);
    setErrorMsg(null);
    setSuccessMsg(null);
    try {
      await evaluationService.submitBranchReview({
        recordId: selectedBranchRecord.id,
        comment: branchComment,
        proposedGrade: branchGrade,
        votesExcellent,
        votesGood,
        votesSatisfactory,
        votesUnsatisfactory,
        totalVoters,
      });
      setSuccessMsg(`Đã hoàn thành đánh giá và nhập phiếu bầu Chi bộ cho đồng chí ${selectedBranchRecord.fullName}!`);
      await loadPeriodData(selectedPeriodId);
    } catch (err: any) {
      setErrorMsg(err.message || "Lưu kết quả Chi bộ thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  // Nộp Bước 4 (Mẫu 03)
  const handleSubmitStep4 = async () => {
    if (!selectedAppraisalRecord) return;
    setActionLoading(true);
    setErrorMsg(null);
    setSuccessMsg(null);
    try {
      await evaluationService.submitAppraisal({
        recordId: selectedAppraisalRecord.id,
        appraisalScore: appraisalScoreInput,
        comment: appraisalCommentInput,
        proposedGrade: appraisalGradeInput,
      });
      setSuccessMsg(`Đã ghi nhận kết quả thẩm định cho đồng chí ${selectedAppraisalRecord.fullName}!`);
      await loadPeriodData(selectedPeriodId);
    } catch (err: any) {
      setErrorMsg(err.message || "Thẩm định thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  // Nộp Bước 5 (Mẫu 07)
  const handleSubmitStep5 = async () => {
    if (!selectedApprovalRecord) return;
    setActionLoading(true);
    setErrorMsg(null);
    setSuccessMsg(null);
    try {
      await evaluationService.approveEvaluation({
        recordId: selectedApprovalRecord.id,
        finalScore: finalScoreInput,
        finalGrade: finalGradeInput,
      });
      setSuccessMsg(`Đã chuẩn y xếp loại chính thức cho đồng chí ${selectedApprovalRecord.fullName}!`);
      await loadPeriodData(selectedPeriodId);
    } catch (err: any) {
      setErrorMsg(err.message || "Chuẩn y xếp loại thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  // Khởi tạo kỳ đánh giá mới (Mẫu chuẩn Hướng dẫn 03)
  const handleCreatePeriod = async (dto: CreatePeriodDto, setAsActive: boolean) => {
    setActionLoading(true);
    setErrorMsg(null);
    try {
      const created = await evaluationService.createPeriod(dto);
      if (setAsActive && created.id) {
        await evaluationService.setActivePeriod(created.id);
      }
      setSuccessMsg(`Đã khởi tạo thành công kỳ đánh giá ${created.name}!`);
      const updatedPeriods = await evaluationService.getPeriods();
      setPeriods(updatedPeriods);
      setSelectedPeriodId(created.id);
      await loadPeriodData(created.id);
    } catch (err: any) {
      setErrorMsg(err.message || "Khởi tạo kỳ đánh giá thất bại.");
      throw err;
    } finally {
      setActionLoading(false);
    }
  };

  // Kích hoạt kỳ đánh giá làm kỳ hiện hành
  const handleSetActivePeriod = async (periodId: string) => {
    setActionLoading(true);
    setErrorMsg(null);
    try {
      await evaluationService.setActivePeriod(periodId);
      setSuccessMsg("Đã kích hoạt kỳ đánh giá làm kỳ hiện hành!");
      const updatedPeriods = await evaluationService.getPeriods();
      setPeriods(updatedPeriods);
      setSelectedPeriodId(periodId);
      await loadPeriodData(periodId);
    } catch (err: any) {
      setErrorMsg(err.message || "Kích hoạt kỳ đánh giá thất bại.");
    } finally {
      setActionLoading(false);
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
        {errorMsg && (
          <div className="alert alert-danger alert-dismissible fade show small mb-0" role="alert">
            <strong>Lỗi:</strong> {errorMsg}
            <button type="button" className="btn-close" onClick={() => setErrorMsg(null)} />
          </div>
        )}
        {successMsg && (
          <div className="alert alert-success alert-dismissible fade show small mb-0" role="alert">
            {successMsg}
            <button type="button" className="btn-close" onClick={() => setSuccessMsg(null)} />
          </div>
        )}

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
                          <Button
                            size="sm"
                            variant="outline-secondary"
                            onClick={() => openIndividualPdf(rec)}
                            title="Xem bản in hồ sơ đánh giá"
                            style={{ padding: "2px 8px", fontSize: "11.5px" }}
                          >
                            Mẫu 02
                          </Button>
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
              />
            )}

            {/* Bước 3: Mẫu 10, 11, 13 (Chi bộ đánh giá) */}
            {activeStep === 3 && (hasPermission("evaluations.branch_vote") || hasPermission("evaluations.branch_review")) && (
              <Step3BranchReview
                records={branchRecords}
                selectedRecord={selectedBranchRecord}
                onSelectRecord={(rec) => {
                  setSelectedBranchRecord(rec);
                  setBranchComment(rec.partyCellComment || "");
                  setBranchGrade(rec.partyCellProposedGrade || "HoanThanhTot");
                  setVotesExcellent(rec.votesExcellent || 0);
                  setVotesGood(rec.votesGood || 0);
                  setVotesSatisfactory(rec.votesSatisfactory || 0);
                  setVotesUnsatisfactory(rec.votesUnsatisfactory || 0);
                  setTotalVoters(rec.totalVoters || 10);
                }}
                comment={branchComment}
                onChangeComment={setBranchComment}
                proposedGrade={branchGrade}
                onChangeProposedGrade={setBranchGrade}
                votesExcellent={votesExcellent}
                onChangeVotesExcellent={setVotesExcellent}
                votesGood={votesGood}
                onChangeVotesGood={setVotesGood}
                votesSatisfactory={votesSatisfactory}
                onChangeVotesSatisfactory={setVotesSatisfactory}
                votesUnsatisfactory={votesUnsatisfactory}
                onChangeVotesUnsatisfactory={setVotesUnsatisfactory}
                totalVoters={totalVoters}
                onChangeTotalVoters={setTotalVoters}
                onSubmit={handleSubmitStep3}
                isSubmitting={actionLoading}
                onOpenDocViewer={openDocumentViewer}
                onOpenPdf={(rec, template) => openEvaluationPdf(rec, template || "mau10")}
              />
            )}

            {/* Bước 4: Mẫu 03 & 15 (Tổ Thẩm định) */}
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
              />
            )}

            {/* Bước 5: Mẫu 07, 08, 14 (Ban Thường vụ Chuẩn y) */}
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
