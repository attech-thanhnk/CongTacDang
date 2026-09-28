"use client";

import React, { useState } from "react";
import { EvaluationPeriodDto, EvaluationRecordDto, CreatePeriodDto } from "@/services/evaluationService";
import { Button } from "@/components/common";

interface EvaluationPeriodHeaderProps {
  periods: EvaluationPeriodDto[];
  selectedPeriodId: string;
  onSelectPeriod: (id: string) => void;
  activePeriod?: EvaluationPeriodDto;
  myRecord?: EvaluationRecordDto | null;
  onOpenPdf?: (record: EvaluationRecordDto) => void;
  onOpenHistory?: (record: EvaluationRecordDto) => void;
  onRefresh?: () => void;
  loading: boolean;
  canManagePeriods?: boolean;
  onCreatePeriod?: (dto: CreatePeriodDto, setAsActive: boolean) => Promise<void>;
  onSetActivePeriod?: (periodId: string) => Promise<void>;
}

export function EvaluationPeriodHeader({
  periods,
  selectedPeriodId,
  onSelectPeriod,
  activePeriod,
  myRecord,
  onOpenPdf,
  onOpenHistory,
  onRefresh,
  loading,
  canManagePeriods = false,
  onCreatePeriod,
  onSetActivePeriod,
}: EvaluationPeriodHeaderProps) {
  // State Modal Tạo Kỳ mới
  const [showCreateModal, setShowCreateModal] = useState(false);
  const currentYear = new Date().getFullYear();
  const [year, setYear] = useState<number>(currentYear);
  const [quarter, setQuarter] = useState<number>(3);
  const [name, setName] = useState<string>(`Đánh giá, xếp loại cán bộ Quý III/${currentYear}`);
  const [startDate, setStartDate] = useState<string>(`${currentYear}-07-01`);
  const [endDate, setEndDate] = useState<string>(`${currentYear}-09-30`);
  const [setAsActive, setSetAsActive] = useState<boolean>(true);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Tự động cập nhật tên kỳ khi đổi Quý hoặc Năm
  const handleQuarterChange = (q: number) => {
    setQuarter(q);
    const roman = q === 1 ? "I" : q === 2 ? "II" : q === 3 ? "III" : "IV";
    setName(`Đánh giá, xếp loại cán bộ Quý ${roman}/${year}`);
    // Gợi ý ngày bắt đầu - kết thúc
    if (q === 1) {
      setStartDate(`${year}-01-01`);
      setEndDate(`${year}-03-31`);
    } else if (q === 2) {
      setStartDate(`${year}-04-01`);
      setEndDate(`${year}-06-30`);
    } else if (q === 3) {
      setStartDate(`${year}-07-01`);
      setEndDate(`${year}-09-30`);
    } else {
      setStartDate(`${year}-10-01`);
      setEndDate(`${year}-12-31`);
    }
  };

  const handleYearChange = (y: number) => {
    setYear(y);
    const roman = quarter === 1 ? "I" : quarter === 2 ? "II" : quarter === 3 ? "III" : "IV";
    setName(`Đánh giá, xếp loại cán bộ Quý ${roman}/${y}`);
  };

  const handleSubmitCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!onCreatePeriod) return;
    setErrorMsg(null);
    setIsSubmitting(true);

    try {
      await onCreatePeriod(
        {
          year,
          quarter,
          name: name.trim(),
          startDate: new Date(startDate).toISOString(),
          endDate: new Date(endDate).toISOString(),
        },
        setAsActive
      );
      setShowCreateModal(false);
    } catch (err: any) {
      setErrorMsg(err.message || "Tạo kỳ đánh giá thất bại.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const selectedPeriod = periods.find((p) => p.id === selectedPeriodId);
  const isSelectedActive = selectedPeriod?.isActive;

  return (
    <>
      <div className="page-header-bar evaluation-page-header">
        {/* Tiêu đề trang */}
        <div className="page-header-content">
          <h1>Đánh giá cán bộ</h1>
        </div>

        {/* Thao tác chọn kỳ, Tạo kỳ mới & In ấn */}
        <div className="page-header-actions">
          {/* Dropdown chọn kỳ */}
          <select
            value={selectedPeriodId}
            onChange={(e) => onSelectPeriod(e.target.value)}
            disabled={loading}
            className="form-select form-select-sm fw-medium"
            style={{ minWidth: "215px" }}
            aria-label="Chọn kỳ đánh giá"
          >
            {periods.map((p) => {
              const shortName = p.quarter && p.year
                ? `Quý ${p.quarter === 1 ? 'I' : p.quarter === 2 ? 'II' : p.quarter === 3 ? 'III' : 'IV'}/${p.year}`
                : p.name.replace(/^Đánh giá, xếp loại cán bộ\s*/i, "").trim();
              return (
                <option key={p.id} value={p.id}>
                  {shortName} {p.isActive ? "(Hiện hành)" : ""}
                </option>
              );
            })}
          </select>

          {canManagePeriods && selectedPeriod && !isSelectedActive && onSetActivePeriod && (
            <Button
              size="sm"
              variant="outline-secondary"
              disabled={loading}
              onClick={() => onSetActivePeriod(selectedPeriod.id)}
              title="Đặt kỳ này làm kỳ hiện hành"
            >
              Kích hoạt
            </Button>
          )}

          {canManagePeriods && onCreatePeriod && (
            <Button
              size="sm"
              variant="outline-primary"
              onClick={() => setShowCreateModal(true)}
            >
              Tạo kỳ
            </Button>
          )}

          {myRecord && onOpenHistory && (
            <Button
              size="sm"
              variant="outline-secondary"
              onClick={() => onOpenHistory(myRecord)}
              title="Xem lịch sử hồ sơ đánh giá"
            >
              <i className="bi bi-clock-history me-1" />Lịch sử
            </Button>
          )}
        </div>
      </div>

      {/* Modal Khởi tạo Kỳ Đánh giá Mới chuẩn nghiệp vụ */}
      {showCreateModal && (
        <div
          className="modal fade show d-block"
          tabIndex={-1}
          style={{ backgroundColor: "rgba(15, 23, 42, 0.5)", zIndex: 1060 }}
        >
          <div className="modal-dialog modal-dialog-centered" style={{ maxWidth: "480px" }}>
            <div className="modal-content border-0 shadow-lg" style={{ borderRadius: "12px" }}>
              <div className="modal-header py-3 px-4 border-bottom">
                <div className="d-flex align-items-center gap-2">
                  <i className="bi bi-calendar-plus text-primary fs-5"></i>
                  <h2 className="modal-title fs-6 fw-bold mb-0 text-dark">
                    Khởi tạo kỳ đánh giá mới
                  </h2>
                </div>
                <button
                  type="button"
                  className="btn-close"
                  onClick={() => setShowCreateModal(false)}
                  disabled={isSubmitting}
                ></button>
              </div>

              <form onSubmit={handleSubmitCreate}>
                <div className="modal-body p-4">
                  {errorMsg && (
                    <div className="alert alert-danger py-2 px-3 small mb-3">
                      <i className="bi bi-exclamation-octagon-fill me-1.5"></i>
                      {errorMsg}
                    </div>
                  )}

                  <div className="row g-3 mb-3">
                    <div className="col-6">
                      <label className="form-label small fw-semibold text-secondary mb-1">
                        Năm đánh giá <span className="text-danger">*</span>
                      </label>
                      <input
                        type="number"
                        min="2020"
                        max="2035"
                        required
                        value={year}
                        onChange={(e) => handleYearChange(parseInt(e.target.value) || currentYear)}
                        className="form-control form-control-sm"
                      />
                    </div>

                    <div className="col-6">
                      <label className="form-label small fw-semibold text-secondary mb-1">
                        Quý đánh giá <span className="text-danger">*</span>
                      </label>
                      <select
                        value={quarter}
                        onChange={(e) => handleQuarterChange(parseInt(e.target.value) || 1)}
                        className="form-select form-select-sm"
                      >
                        <option value={1}>Quý I</option>
                        <option value={2}>Quý II</option>
                        <option value={3}>Quý III</option>
                        <option value={4}>Quý IV</option>
                      </select>
                    </div>
                  </div>

                  <div className="mb-3">
                    <label className="form-label small fw-semibold text-secondary mb-1">
                      Tên kỳ đánh giá <span className="text-danger">*</span>
                    </label>
                    <input
                      type="text"
                      required
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                      className="form-control form-control-sm"
                      placeholder="VD: Đánh giá, xếp loại cán bộ Quý IV/2026"
                    />
                  </div>

                  <div className="row g-3 mb-3">
                    <div className="col-6">
                      <label className="form-label small fw-semibold text-secondary mb-1">
                        Ngày bắt đầu <span className="text-danger">*</span>
                      </label>
                      <input
                        type="date"
                        required
                        value={startDate}
                        onChange={(e) => setStartDate(e.target.value)}
                        className="form-control form-control-sm"
                      />
                    </div>

                    <div className="col-6">
                      <label className="form-label small fw-semibold text-secondary mb-1">
                        Ngày kết thúc <span className="text-danger">*</span>
                      </label>
                      <input
                        type="date"
                        required
                        value={endDate}
                        onChange={(e) => setEndDate(e.target.value)}
                        className="form-control form-control-sm"
                      />
                    </div>
                  </div>

                  <div className="form-check">
                    <input
                      className="form-check-input"
                      type="checkbox"
                      id="setActiveCheck"
                      checked={setAsActive}
                      onChange={(e) => setSetAsActive(e.target.checked)}
                    />
                    <label className="form-check-label text-dark small" htmlFor="setActiveCheck">
                      Đặt làm kỳ hiện hành ngay sau khi tạo
                    </label>
                  </div>
                </div>

                <div className="modal-footer py-2.5 px-4 bg-light-subtle border-top d-flex justify-content-end gap-2">
                  <Button
                    size="sm"
                    variant="outline-secondary"
                    onClick={() => setShowCreateModal(false)}
                    disabled={isSubmitting}
                  >
                    Hủy
                  </Button>
                  <Button
                    size="sm"
                    variant="primary"
                    type="submit"
                    loading={isSubmitting}
                    loadingText="Đang khởi tạo..."
                  >
                    Khởi tạo kỳ đánh giá
                  </Button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

export default EvaluationPeriodHeader;
