"use client";

import React, { useState } from "react";
import Link from "next/link";
import { reportService } from "@/services/reportService";
import { PageHeader, DataTable, DataTableColumn, Button } from "@/components/common";

interface StandardForm {
  id: string;
  code: string;
  title: string;
  group: string;
  targetUser: string;
  purpose: string;
  actionType: "report" | "evaluation";
}

const STANDARD_FORMS: StandardForm[] = [
  {
    id: "form-01",
    code: "Mẫu 01",
    title: "Phiếu đăng ký công việc chuyên môn hằng quý",
    group: "Giao việc & Đăng ký",
    targetUser: "Cán bộ lãnh đạo, quản lý",
    purpose: "Đăng ký 3 - 7 nhiệm vụ đầu quý (tổng 70 điểm)",
    actionType: "evaluation",
  },
  {
    id: "form-02",
    code: "Mẫu 02",
    title: "Bản tự đánh giá kết quả thực hiện nhiệm vụ",
    group: "Tự chấm điểm",
    targetUser: "Cá nhân cán bộ",
    purpose: "Tự chấm điểm sản phẩm chuyên môn theo 4 tiêu chí A-B-C-D",
    actionType: "evaluation",
  },
  {
    id: "form-09",
    code: "Mẫu 09",
    title: "Phiếu tự chấm điểm tiêu chí chung",
    group: "Tự chấm điểm",
    targetUser: "Cán bộ lãnh đạo, quản lý",
    purpose: "Tự chấm tiêu chí chung và kết quả nhiệm vụ theo bộ tiêu chí của kỳ",
    actionType: "evaluation",
  },
  {
    id: "form-10",
    code: "Mẫu 10",
    title: "Phiếu nhận xét, đánh giá của Chi bộ",
    group: "Chi bộ đánh giá",
    targetUser: "Chi ủy, Bí thư Chi bộ",
    purpose: "Nhận xét phẩm chất và kết quả công tác của cán bộ",
    actionType: "evaluation",
  },
  {
    id: "form-13",
    code: "Mẫu 13",
    title: "Biên bản kiểm phiếu đánh giá xếp loại",
    group: "Chi bộ đánh giá",
    targetUser: "Tổ kiểm phiếu Chi bộ",
    purpose: "Ghi nhận kết quả bỏ phiếu kín của Hội nghị Chi bộ",
    actionType: "evaluation",
  },
  {
    id: "form-03",
    code: "Mẫu 03",
    title: "Bảng thẩm định điểm số và kiểm tra minh chứng",
    group: "Tổ thẩm định",
    targetUser: "Tổ thẩm định Đảng ủy",
    purpose: "Đối soát hồ sơ, minh chứng và đề xuất xếp loại",
    actionType: "report",
  },
  {
    id: "form-15",
    code: "Mẫu 15",
    title: "Kiểm soát tỷ lệ hoàn thành xuất sắc nhiệm vụ",
    group: "Tổ thẩm định",
    targetUser: "Tổ thẩm định & BTV",
    purpose: "Kiểm soát trần 20% xuất sắc theo từng Chi bộ",
    actionType: "report",
  },
  {
    id: "form-14",
    code: "Mẫu 14",
    title: "Bảng tổng hợp xếp loại cán bộ toàn Đảng bộ",
    group: "BTV Chuẩn y",
    targetUser: "Ban Thường vụ Đảng ủy",
    purpose: "Chuẩn y và quyết định mức xếp loại chính thức",
    actionType: "report",
  },
];

export default function FormsPage() {
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedGroup, setSelectedGroup] = useState("all");

  const groups = Array.from(new Set(STANDARD_FORMS.map((f) => f.group)));

  const filteredForms = STANDARD_FORMS.filter((f) => {
    const term = searchTerm.toLowerCase();
    const matchSearch =
      f.title.toLowerCase().includes(term) ||
      f.code.toLowerCase().includes(term) ||
      f.purpose.toLowerCase().includes(term);

    const matchGroup = selectedGroup === "all" || f.group === selectedGroup;

    return matchSearch && matchGroup;
  });

  const columns: DataTableColumn<StandardForm>[] = [
    {
      header: "STT",
      width: "40px",
      align: "center",
      render: (_, idx) => <span className="text-muted">{idx + 1}</span>,
    },
    {
      header: "Ký hiệu",
      width: "95px",
      align: "center",
      render: (f) => <span className="font-monospace fw-bold text-primary">{f.code}</span>,
    },
    {
      header: "Tên biểu mẫu",
      render: (f) => <span className="fw-semibold text-dark">{f.title}</span>,
    },
    {
      header: "Nhóm quy trình",
      width: "150px",
      render: (f) => <span className="text-secondary">{f.group}</span>,
    },
    {
      header: "Đối tượng áp dụng",
      width: "160px",
      render: (f) => <span className="text-secondary">{f.targetUser}</span>,
    },
    {
      header: "Mục đích sử dụng",
      render: (f) => <span className="text-muted" style={{ fontSize: "12px" }}>{f.purpose}</span>,
    },
    {
      header: "Thao tác",
      width: "120px",
      align: "center",
      render: (f) => (
        <Link
          href={f.actionType === "report" ? "/reports" : "/evaluations"}
          className="btn btn-sm btn-outline-primary py-0.5 px-2.5 d-inline-flex align-items-center gap-1"
          style={{ fontSize: "11.5px" }}
        >
          <span>Xem</span>
        </Link>
      ),
    },
  ];

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Biểu mẫu"
        actions={
          <Button
            size="sm"
            variant="outline-secondary"
            icon="bi-printer"
            onClick={() => reportService.printDocument()}
          >
            In danh mục
          </Button>
        }
      />

      <div className="page-body">
        <DataTable
          columns={columns}
          data={filteredForms}
          keyExtractor={(item) => item.id}
          searchTerm={searchTerm}
          onSearchChange={setSearchTerm}
          searchPlaceholder="Tìm kiếm biểu mẫu..."
          filters={
            <div className="d-flex align-items-center gap-2">
              <span className="small text-secondary text-nowrap">Nhóm:</span>
              <select
                value={selectedGroup}
                onChange={(e) => setSelectedGroup(e.target.value)}
                className="form-select form-select-sm"
                style={{ width: "200px" }}
              >
                <option value="all">Tất cả các nhóm</option>
                {groups.map((g) => (
                  <option key={g} value={g}>{g}</option>
                ))}
              </select>
            </div>
          }
          emptyTitle="Không tìm thấy biểu mẫu"
          emptyDescription="Không có biểu mẫu nào khớp với bộ lọc hiện tại."
          emptyIcon="bi-file-earmark-text"
        />
      </div>
    </div>
  );
}
