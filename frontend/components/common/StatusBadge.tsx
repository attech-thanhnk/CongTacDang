"use client";

import React from "react";

export type StatusBadgeType =
  | "grade"       // Xếp loại Đảng
  | "recordStatus"// Trạng thái quy trình hồ sơ đánh giá
  | "partyMember" // Đảng viên
  | "active"      // Trạng thái tài khoản/cán bộ
  | "quota"       // Trần 20%
  | "custom";     // Tùy biến

export interface StatusBadgeProps {
  type?: StatusBadgeType;
  value?: string | null;
  label?: string;
  className?: string;
}

export const StatusBadge: React.FC<StatusBadgeProps> = ({
  type = "custom",
  value,
  label,
  className = "",
}) => {
  if (!value && !label) {
    return <span className="text-muted" style={{ fontSize: "11px" }}>—</span>;
  }

  // 1. Mức xếp loại Đảng
  if (type === "grade") {
    switch (value) {
      case "HoanThanhXuatSac":
        return (
          <span
            className={`badge text-white ${className}`}
            style={{ backgroundColor: "#991b1b", fontSize: "11px", fontWeight: 600 }}
          >
            Xuất sắc
          </span>
        );
      case "HoanThanhTot":
        return (
          <span
            className={`badge ${className}`}
            style={{ backgroundColor: "#eff6ff", color: "#1d4ed8", border: "1px solid #bfdbfe", fontSize: "11px" }}
          >
            Hoàn thành tốt
          </span>
        );
      case "HoanThanh":
        return (
          <span
            className={`badge ${className}`}
            style={{ backgroundColor: "#ecfdf5", color: "#047857", border: "1px solid #a7f3d0", fontSize: "11px" }}
          >
            Hoàn thành
          </span>
        );
      case "KhongHoanThanh":
        return (
          <span
            className={`badge ${className}`}
            style={{ backgroundColor: "#fef2f2", color: "#b91c1c", border: "1px solid #fecaca", fontSize: "11px" }}
          >
            Không hoàn thành
          </span>
        );
      default:
        return (
          <span className={`badge bg-light text-secondary border ${className}`} style={{ fontSize: "11px" }}>
            {label || value || "Chưa xếp loại"}
          </span>
        );
    }
  }

  // 2. Trạng thái quy trình hồ sơ đánh giá
  if (type === "recordStatus") {
    switch (value) {
      case "Draft":
        return (
          <span className={`badge bg-secondary-subtle text-secondary border ${className}`} style={{ fontSize: "12px" }}>
            Bản nháp
          </span>
        );
      case "Submitted":
        return (
          <span className={`badge ${className}`} style={{ backgroundColor: "#eff6ff", color: "#1d4ed8", border: "1px solid #bfdbfe", fontSize: "12px" }}>
            Đã đăng ký (M01)
          </span>
        );
      case "BranchReviewed":
        return (
          <span className={`badge ${className}`} style={{ backgroundColor: "#fefce8", color: "#854d0e", border: "1px solid #fef08a", fontSize: "12px" }}>
            Chi bộ đã họp
          </span>
        );
      case "Appraised":
        return (
          <span className={`badge ${className}`} style={{ backgroundColor: "#f5f3ff", color: "#6d28d9", border: "1px solid #ddd6fe", fontSize: "12px" }}>
            Đã thẩm định
          </span>
        );
      case "Approved":
        return (
          <span className={`badge ${className}`} style={{ backgroundColor: "#ecfdf5", color: "#047857", border: "1px solid #a7f3d0", fontSize: "12px" }}>
            BTV chuẩn y
          </span>
        );
      default:
        return (
          <span className={`badge bg-light text-secondary border ${className}`} style={{ fontSize: "12px" }}>
            {label || value}
          </span>
        );
    }
  }

  // 3. Trạng thái Đảng viên
  if (type === "partyMember") {
    return (
      <span
        className={`badge ${className}`}
        style={{ backgroundColor: "#fff1f2", color: "#be123c", border: "1px solid #fecdd3", fontSize: "12px" }}
      >
        <i className="bi bi-star-fill me-1" style={{ fontSize: "10px" }}></i>
        Đảng viên
      </span>
    );
  }

  // 4. Trạng thái hoạt động
  if (type === "active") {
    const isActive = value === "active" || value === "true" || value === "Hoạt động";
    return (
      <span
        className={`badge ${className}`}
        style={{
          backgroundColor: isActive ? "#ecfdf5" : "#f1f5f9",
          color: isActive ? "#047857" : "#475569",
          border: isActive ? "1px solid #a7f3d0" : "1px solid #e2e8f0",
          fontSize: "12px",
        }}
      >
        {isActive ? "Hoạt động" : "Tạm dừng"}
      </span>
    );
  }

  // 5. Kiểm soát trần Quota 20%
  if (type === "quota") {
    const isExceeded = value === "exceeded" || value === "true";
    return (
      <span
        className={`badge ${className}`}
        style={{
          backgroundColor: isExceeded ? "#fef2f2" : "#ecfdf5",
          color: isExceeded ? "#b91c1c" : "#047857",
          border: isExceeded ? "1px solid #fecaca" : "1px solid #a7f3d0",
          fontSize: "11px",
        }}
      >
        {isExceeded ? "Vượt trần (>20%)" : "Hợp lệ (≤20%)"}
      </span>
    );
  }

  // Custom fallback
  return (
    <span className={`badge bg-light text-secondary border ${className}`} style={{ fontSize: "11px" }}>
      {label || value}
    </span>
  );
};

export default StatusBadge;
