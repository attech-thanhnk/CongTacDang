"use client";

import React, { useState } from "react";
import { reportService, REPORT_LIST, ReportItem } from "@/services/reportService";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { PageHeader, DataTable, DataTableColumn, Button } from "@/components/common";

export default function ReportsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const [searchTerm, setSearchTerm] = useState("");
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const handleDownload = async (report: ReportItem) => {
    if (!report.endpoint) return;
    setDownloadingId(report.id);
    try {
      await reportService.downloadReport(report.endpoint, report.fileName);
      toast.success(`Đã xuất và tải về báo cáo "${report.title}".`);
    } catch (err: any) {
      toast.error(err.message || "Không thể tải báo cáo. Vui lòng kiểm tra quyền hạn tài khoản.");
    } finally {
      setDownloadingId(null);
    }
  };

  const filteredReports = REPORT_LIST.filter((rep) => {
    const term = searchTerm.toLowerCase();
    return (
      rep.title.toLowerCase().includes(term) ||
      rep.code.toLowerCase().includes(term) ||
      rep.description.toLowerCase().includes(term) ||
      rep.category.toLowerCase().includes(term)
    );
  });

  const columns: DataTableColumn<ReportItem>[] = [
    {
      header: "Mã số",
      width: "110px",
      render: (rep) => (
        <span
          style={{
            fontFamily: "var(--font-mono)",
            fontWeight: 700,
            fontSize: "12px",
            color: "var(--color-cobalt)",
            background: "var(--color-primary-light)",
            border: "1px solid var(--color-primary-border)",
            borderRadius: "4px",
            padding: "2px 7px",
          }}
        >
          {rep.code}
        </span>
      ),
    },
    {
      header: "Tên báo cáo",
      render: (rep) => (
        <div>
          <div style={{ fontWeight: 600, color: "var(--text-primary)" }}>{rep.title}</div>
          <div style={{ fontSize: "11.5px", color: "var(--text-muted)", marginTop: "2px" }}>
            {rep.description}
          </div>
        </div>
      ),
    },
    {
      header: "Nhóm",
      width: "160px",
      render: (rep) => (
        <span style={{ fontSize: "12.5px", color: "var(--text-secondary)" }}>{rep.category}</span>
      ),
    },
    {
      header: "Định dạng",
      width: "80px",
      align: "center",
      render: (rep) => (
        <span
          style={{
            fontFamily: "var(--font-mono)",
            fontWeight: 700,
            fontSize: "11px",
            color: "var(--text-muted)",
          }}
        >
          {rep.fileType}
        </span>
      ),
    },
  ];

  if (hasPermission("report.export")) {
    columns.push({
      header: "Thao tác",
      width: "120px",
      align: "center",
      render: (rep) => (
        <Button
          size="sm"
          variant="outline-primary"
          icon="bi-download"
          loading={downloadingId === rep.id}
          loadingText="Tải..."
          onClick={() => handleDownload(rep)}
          style={{ fontSize: "12px" }}
        >
          Xuất Excel
        </Button>
      ),
    });
  }

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Báo cáo"
        actions={
          <Button
            size="sm"
            variant="outline-secondary"
            icon="bi-printer"
            onClick={() => reportService.printDocument()}
          >
            In báo cáo
          </Button>
        }
      />

      <div className="page-body">
        <DataTable
          columns={columns}
          data={filteredReports}
          keyExtractor={(item) => item.id}
          searchTerm={searchTerm}
          onSearchChange={setSearchTerm}
          searchPlaceholder="Tìm kiếm báo cáo..."
          totalCountText={`${filteredReports.length} báo cáo`}
          emptyTitle="Không tìm thấy báo cáo"
          emptyDescription="Không có báo cáo nào phù hợp với từ khóa tìm kiếm."
          emptyIcon="bi-file-earmark-bar-graph"
        />
      </div>
    </div>
  );
}
