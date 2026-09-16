"use client";

import React, { useState } from "react";
import { 
  Users, 
  ShieldCheck, 
  Building2, 
  SlidersHorizontal, 
  Search, 
  Plus, 
  Edit3, 
  Download, 
  CheckCircle2, 
  FileSpreadsheet, 
  UserCheck, 
  Layers
} from "lucide-react";
import Link from "next/link";

export default function QuanTriPage() {
  const [activeTab, setActiveTab] = useState<"can_bo" | "to_chuc" | "phan_quyen" | "nhat_ky">("can_bo");
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedBranch, setSelectedBranch] = useState("all");

  // Danh sach mau 68 can bo lanh dao / quan ly ATTECH
  const [cadres, setCadres] = useState([
    {
      id: "CB-01",
      fullName: "Nguyễn Văn A",
      partyCard: "ĐV-034891",
      branch: "Chi bộ Khối Kỹ thuật",
      partyRole: "Bí thư Chi bộ",
      dept: "Phòng Kế hoạch Kinh doanh",
      adminTitle: "Trưởng phòng Kế hoạch KD",
      jobGroup: "Khung 2 (Kỹ thuật - Nghiệp vụ)",
      approvalAuthority: "Đảng ủy ATTECH",
      status: "Đang hoạt động",
      roles: ["CAN_BO", "BI_THU_CHI_BO"]
    },
    {
      id: "CB-02",
      fullName: "Trần Thị B",
      partyCard: "ĐV-034892",
      branch: "Chi bộ Cơ quan Văn phòng",
      partyRole: "Chi ủy viên",
      dept: "Phòng TCCB - Lao động",
      adminTitle: "Trưởng phòng TCCB - LĐ",
      jobGroup: "Khung 3 (Tham mưu, Tổng hợp)",
      approvalAuthority: "Đảng ủy ATTECH",
      status: "Đang hoạt động",
      roles: ["CAN_BO", "TO_THAM_DINH"]
    },
    {
      id: "CB-03",
      fullName: "Lê Quang C",
      partyCard: "ĐV-034893",
      branch: "Chi bộ Khối Sản xuất",
      partyRole: "Bí thư Chi bộ",
      dept: "Xưởng Sản xuất thiết bị",
      adminTitle: "Quản đốc Xưởng Sản xuất",
      jobGroup: "Khung 1 (Lãnh đạo, Điều hành)",
      approvalAuthority: "BTV Đảng ủy Tổng công ty",
      status: "Đang hoạt động",
      roles: ["CAN_BO", "BI_THU_CHI_BO"]
    },
    {
      id: "CB-04",
      fullName: "Phạm Văn D",
      partyCard: "ĐV-034894",
      branch: "Chi bộ Cơ quan Văn phòng",
      partyRole: "Đảng viên",
      dept: "Phòng Tài chính - Kế toán",
      adminTitle: "Phó Trưởng phòng Kế toán",
      jobGroup: "Khung 2 (Kỹ thuật - Nghiệp vụ)",
      approvalAuthority: "Đảng ủy ATTECH",
      status: "Đang hoạt động",
      roles: ["CAN_BO"]
    },
    {
      id: "CB-05",
      fullName: "Hoàng Minh E",
      partyCard: "ĐV-034895",
      branch: "Chi bộ Khối Dịch vụ",
      partyRole: "Phó Bí thư Chi bộ",
      dept: "Trung tâm Dịch vụ Kỹ thuật",
      adminTitle: "Giám đốc TT Dịch vụ",
      jobGroup: "Khung 1 (Lãnh đạo, Điều hành)",
      approvalAuthority: "BTV Đảng ủy Tổng công ty",
      status: "Đang hoạt động",
      roles: ["CAN_BO", "BI_THU_CHI_BO"]
    },
    {
      id: "CB-06",
      fullName: "Vũ Thị F",
      partyCard: "ĐV-034896",
      branch: "Chi bộ Khối Kỹ thuật",
      partyRole: "Chi ủy viên",
      dept: "Phòng Kỹ thuật - Công nghệ",
      adminTitle: "Phó Trưởng phòng KT-CN",
      jobGroup: "Khung 2 (Kỹ thuật - Nghiệp vụ)",
      approvalAuthority: "Đảng ủy ATTECH",
      status: "Đang hoạt động",
      roles: ["CAN_BO"]
    }
  ]);

  // 4 Chi bo truc thuoc Dang bo ATTECH
  const branches = [
    { code: "CB-KT", name: "Chi bộ Khối Kỹ thuật", secretary: "Nguyễn Văn A", totalMembers: 18, location: "Tầng 4, Tòa nhà ATTECH" },
    { code: "CB-SX", name: "Chi bộ Khối Sản xuất", secretary: "Lê Quang C", totalMembers: 16, location: "Khu Xưởng Cơ điện CNS" },
    { code: "CB-DV", name: "Chi bộ Khối Dịch vụ", secretary: "Hoàng Minh E", totalMembers: 14, location: "Trung tâm Dịch vụ CNS" },
    { code: "CB-VP", name: "Chi bộ Cơ quan Văn phòng", secretary: "Trần Thị B", totalMembers: 20, location: "Tầng 3, Tòa nhà ATTECH" }
  ];

  // 5 Role he thong va Ma tran phan quyen
  const systemRoles = [
    {
      code: "CAN_BO",
      name: "Cán bộ Lãnh đạo / Quản lý",
      desc: "Nhập Mẫu 01, Mẫu 02 cá nhân; Bỏ phiếu kín Mẫu 11 chi bộ",
      color: "bg-blue-50 border-blue-200 text-blue-800",
      permissions: ["Đăng ký Mẫu 01", "Tự chấm Mẫu 02", "Bỏ phiếu kín Mẫu 11", "Đính kèm minh chứng", "Xem kết quả cá nhân"]
    },
    {
      code: "BI_THU_CHI_BO",
      name: "Bí thư / Cấp ủy Chi bộ",
      desc: "Chủ trì họp chi bộ, giám sát tiến độ, mở hòm phiếu kín, ký Biên bản Mẫu 12, 13",
      color: "bg-amber-50 border-amber-200 text-amber-800",
      permissions: ["Xem Mẫu 01, 02 toàn Chi bộ", "Chủ trì bỏ phiếu kín Mẫu 11", "Ký Biên bản kiểm phiếu Mẫu 12, 13", "Đôn đốc tiến độ nộp biểu mẫu"]
    },
    {
      code: "TO_THAM_DINH",
      name: "Tổ Thẩm định Đảng ủy",
      desc: "Thẩm định hồ sơ Mẫu 03, lập Phiếu thẩm định Mẫu 10 (chênh lệch >= 5đ), soát trần 20%",
      color: "bg-purple-50 border-purple-200 text-purple-800",
      permissions: ["Thẩm định Mẫu 03 toàn công ty", "Lập Phiếu Mẫu 10 khi vênh điểm", "Kiểm soát chỉ tiêu trần 20% Mẫu 15", "Đề xuất mức xếp loại quý"]
    },
    {
      code: "BAN_THUONG_VU",
      name: "Ban Thường vụ Đảng ủy",
      desc: "Xem xét Mẫu 14, 15; Quyết định xếp loại chính thức; Ký Báo cáo Mẫu 16 gửi Đảng ủy TCT",
      color: "bg-rose-50 border-rose-200 text-rose-800",
      permissions: ["Xem toàn bộ hồ sơ & biên bản", "Chuẩn y xếp loại chính thức Mẫu 14", "Quyết định xử lý ngoại lệ Mẫu 04", "Ký phát hành Báo cáo Mẫu 16"]
    },
    {
      code: "ADMIN_HE_THONG",
      name: "Quản trị viên Hệ thống",
      desc: "Quản trị danh mục 2 vai, thiết lập cấu hình thang điểm & trần tỷ lệ, kiểm soát phân quyền",
      color: "bg-emerald-50 border-emerald-200 text-emerald-800",
      permissions: ["Quản trị 68 cán bộ 2 vai", "Quản lý Chi bộ & Phòng ban", "Cấu hình thang điểm & tỷ trọng", "Phân quyền & Khóa kỳ đánh giá", "Sao lưu dữ liệu PostgreSQL"]
    }
  ];

  const filteredCadres = cadres.filter(c => {
    const matchSearch = c.fullName.toLowerCase().includes(searchTerm.toLowerCase()) ||
                        c.adminTitle.toLowerCase().includes(searchTerm.toLowerCase()) ||
                        c.partyCard.toLowerCase().includes(searchTerm.toLowerCase());
    const matchBranch = selectedBranch === "all" || c.branch === selectedBranch;
    return matchSearch && matchBranch;
  });

  return (
    <div className="space-y-6 animate-in fade-in duration-300">
      {/* Page Title & Stats */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-white p-6 rounded-2xl border border-slate-200 shadow-sm">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <span className="px-2.5 py-0.5 rounded-full text-[11px] font-bold tracking-wide uppercase bg-red-100 text-dang-crimson border border-red-200">
              Quản trị & Phân quyền RBAC
            </span>
            <span className="text-xs text-slate-400">• Mô hình 2 vai song song</span>
          </div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
            Quản trị Hệ thống & Danh mục Nhân sự Lãnh đạo
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            Công ty TNHH Kỹ thuật Quản lý bay (ATTECH) • Quy định theo Hướng dẫn 03-HD/TVĐU ngày 10/9/2026
          </p>
        </div>

        <div className="flex items-center gap-3">
          <Link
            href="/cau-hinh"
            className="flex items-center gap-2 px-3.5 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-xl text-xs font-semibold transition"
          >
            <SlidersHorizontal className="w-4 h-4 text-slate-600" />
            Cấu hình Điểm & Trần
          </Link>
          <a
            href="http://localhost:5000/api/ExportReport/mau-14"
            target="_blank"
            rel="noreferrer"
            className="flex items-center gap-2 px-4 py-2 bg-emerald-700 hover:bg-emerald-800 text-white rounded-xl text-xs font-semibold shadow-sm transition"
          >
            <FileSpreadsheet className="w-4 h-4" />
            Xuất Excel Mẫu 14
          </a>
        </div>
      </div>

      {/* Navigation Tabs */}
      <div className="flex items-center gap-2 border-b border-slate-200 pb-2">
        <button
          onClick={() => setActiveTab("can_bo")}
          className={`flex items-center gap-2 px-4 py-2.5 rounded-xl text-xs font-bold transition-all ${
            activeTab === "can_bo"
              ? "bg-dang-crimson text-white shadow-sm"
              : "text-slate-600 hover:bg-slate-100"
          }`}
        >
          <Users className="w-4 h-4" />
          68 Cán bộ Lãnh đạo & Vai kép
        </button>
        <button
          onClick={() => setActiveTab("to_chuc")}
          className={`flex items-center gap-2 px-4 py-2.5 rounded-xl text-xs font-bold transition-all ${
            activeTab === "to_chuc"
              ? "bg-dang-crimson text-white shadow-sm"
              : "text-slate-600 hover:bg-slate-100"
          }`}
        >
          <Building2 className="w-4 h-4" />
          4 Chi bộ & Cơ cấu Tổ chức
        </button>
        <button
          onClick={() => setActiveTab("phan_quyen")}
          className={`flex items-center gap-2 px-4 py-2.5 rounded-xl text-xs font-bold transition-all ${
            activeTab === "phan_quyen"
              ? "bg-dang-crimson text-white shadow-sm"
              : "text-slate-600 hover:bg-slate-100"
          }`}
        >
          <ShieldCheck className="w-4 h-4" />
          Ma trận Phân quyền (5 Roles)
        </button>
      </div>

      {/* TAB 1: 68 CAN BO 2 VAI */}
      {activeTab === "can_bo" && (
        <div className="space-y-4">
          {/* Filter and Search Bar */}
          <div className="bg-white p-4 rounded-xl border border-slate-200 flex flex-col md:flex-row md:items-center justify-between gap-3 shadow-sm">
            <div className="flex items-center gap-3 flex-1">
              <div className="relative flex-1 max-w-md">
                <Search className="w-4 h-4 text-slate-400 absolute left-3 top-2.5" />
                <input
                  type="text"
                  placeholder="Tìm theo họ tên, chức danh, số thẻ Đảng..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="w-full pl-9 pr-3 py-1.5 text-xs bg-slate-50 border border-slate-200 rounded-lg focus:outline-none focus:border-attech-blue"
                />
              </div>

              <select
                value={selectedBranch}
                onChange={(e) => setSelectedBranch(e.target.value)}
                className="text-xs bg-slate-50 border border-slate-200 rounded-lg px-3 py-1.5 focus:outline-none focus:border-attech-blue text-slate-700"
              >
                <option value="all">-- Tất cả Chi bộ --</option>
                <option value="Chi bộ Khối Kỹ thuật">Chi bộ Khối Kỹ thuật (18 đ/c)</option>
                <option value="Chi bộ Khối Sản xuất">Chi bộ Khối Sản xuất (16 đ/c)</option>
                <option value="Chi bộ Khối Dịch vụ">Chi bộ Khối Dịch vụ (14 đ/c)</option>
                <option value="Chi bộ Cơ quan Văn phòng">Chi bộ Cơ quan Văn phòng (20 đ/c)</option>
              </select>
            </div>

            <div className="flex items-center gap-2">
              <span className="text-xs text-slate-500 font-medium">Hiển thị {filteredCadres.length}/68 cán bộ</span>
              <button
                onClick={() => alert("Tính năng thêm mới cán bộ 2 vai")}
                className="flex items-center gap-1.5 px-3 py-1.5 bg-attech-navy hover:bg-slate-800 text-white rounded-lg text-xs font-semibold shadow-sm transition"
              >
                <Plus className="w-3.5 h-3.5" />
                Thêm Cán bộ
              </button>
            </div>
          </div>

          {/* Table */}
          <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs border-collapse">
                <thead>
                  <tr className="bg-slate-800 text-white font-semibold">
                    <th className="p-3 border-r border-slate-700 w-12 text-center">STT</th>
                    <th className="p-3 border-r border-slate-700">Họ và tên & Số thẻ</th>
                    <th className="p-3 border-r border-slate-700">Vai 1: Tổ chức Đảng</th>
                    <th className="p-3 border-r border-slate-700">Vai 2: Chính quyền</th>
                    <th className="p-3 border-r border-slate-700">Khung & Cấp duyệt</th>
                    <th className="p-3 border-r border-slate-700">Vai trò HT (Roles)</th>
                    <th className="p-3 text-center w-20">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {filteredCadres.map((cadre, index) => (
                    <tr key={cadre.id} className="hover:bg-slate-50/70 transition">
                      <td className="p-3 text-center text-slate-400 font-medium">{index + 1}</td>
                      <td className="p-3 border-r border-slate-100 font-semibold text-slate-900">
                        <div>{cadre.fullName}</div>
                        <div className="text-[11px] text-slate-400 font-mono font-normal">{cadre.partyCard}</div>
                      </td>
                      <td className="p-3 border-r border-slate-100">
                        <div className="font-semibold text-dang-crimson">{cadre.branch}</div>
                        <div className="text-[11px] text-slate-500">{cadre.partyRole}</div>
                      </td>
                      <td className="p-3 border-r border-slate-100">
                        <div className="font-semibold text-slate-800">{cadre.adminTitle}</div>
                        <div className="text-[11px] text-slate-400">{cadre.dept}</div>
                      </td>
                      <td className="p-3 border-r border-slate-100">
                        <div className="text-slate-700 font-medium">{cadre.jobGroup}</div>
                        <span className={`inline-block mt-0.5 px-2 py-0.5 rounded text-[10px] font-semibold ${
                          cadre.approvalAuthority.includes("Tổng công ty")
                            ? "bg-purple-50 text-purple-700 border border-purple-200"
                            : "bg-blue-50 text-blue-700 border border-blue-200"
                        }`}>
                          {cadre.approvalAuthority}
                        </span>
                      </td>
                      <td className="p-3 border-r border-slate-100">
                        <div className="flex flex-wrap gap-1">
                          {cadre.roles.map(r => (
                            <span key={r} className="px-1.5 py-0.5 bg-slate-100 text-slate-700 rounded text-[10px] font-mono font-bold">
                              {r}
                            </span>
                          ))}
                        </div>
                      </td>
                      <td className="p-3 text-center">
                        <button
                          title="Sửa hồ sơ"
                          onClick={() => alert(`Sửa hồ sơ của đồng chí: ${cadre.fullName}`)}
                          className="p-1.5 text-slate-500 hover:text-attech-blue hover:bg-blue-50 rounded transition"
                        >
                          <Edit3 className="w-3.5 h-3.5" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: TO CHUC 4 CHI BO */}
      {activeTab === "to_chuc" && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {branches.map(b => (
            <div key={b.code} className="bg-white p-5 rounded-xl border border-slate-200 shadow-sm flex flex-col justify-between">
              <div>
                <div className="flex items-center justify-between mb-2">
                  <span className="px-2 py-0.5 rounded bg-dang-crimson/10 text-dang-crimson font-mono font-bold text-xs">
                    {b.code}
                  </span>
                  <span className="text-xs text-slate-500 font-semibold">{b.totalMembers} cán bộ</span>
                </div>
                <h3 className="font-bold text-base text-slate-900">{b.name}</h3>
                <p className="text-xs text-slate-500 mt-1">Bí thư Chi bộ: <span className="font-semibold text-slate-800">{b.secretary}</span></p>
                <p className="text-xs text-slate-400">Địa điểm sinh hoạt: {b.location}</p>
              </div>

              <div className="mt-4 pt-4 border-t border-slate-100 flex items-center justify-between text-xs">
                <span className="text-emerald-700 font-semibold flex items-center gap-1">
                  <CheckCircle2 className="w-3.5 h-3.5" />
                  Đã chuẩn hóa 100% hồ sơ
                </span>
                <Link
                  href="/bieu-mau"
                  className="text-attech-blue hover:underline font-semibold"
                >
                  Xem biểu mẫu chi bộ →
                </Link>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* TAB 3: PHAN QUYEN RBAC 5 ROLES */}
      {activeTab === "phan_quyen" && (
        <div className="space-y-4">
          <div className="bg-amber-50 p-4 rounded-xl border border-amber-200 text-xs text-amber-900">
            <p className="font-bold mb-1">Nguyên tắc Phân quyền RBAC theo Hướng dẫn 03-HD/TVĐU:</p>
            <p>Hệ thống chia làm 5 vai trò khép kín, bảo đảm tính độc lập giữa tự đánh giá, tập thể chi bộ bỏ phiếu kín, tổ thẩm định đối soát chênh lệch, và Ban Thường vụ chuẩn y cuối cùng.</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {systemRoles.map(role => (
              <div key={role.code} className="bg-white rounded-xl border border-slate-200 shadow-sm p-5 flex flex-col justify-between">
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <span className="font-mono font-bold text-[11px] px-2 py-0.5 rounded bg-slate-100 text-slate-800">
                      {role.code}
                    </span>
                  </div>
                  <h4 className="font-bold text-sm text-slate-900">{role.name}</h4>
                  <p className="text-xs text-slate-500 mt-1 leading-relaxed">{role.desc}</p>

                  <div className="mt-4 space-y-1.5">
                    <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Quyền hạn cấp phát:</span>
                    {role.permissions.map((p, idx) => (
                      <div key={idx} className="flex items-center gap-2 text-xs text-slate-700">
                        <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600 shrink-0" />
                        <span>{p}</span>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="mt-4 pt-3 border-t border-slate-100 text-[11px] text-slate-400 flex items-center justify-between">
                  <span>Trạng thái: Hoạt động</span>
                  <span className="text-attech-navy font-semibold">Tự động kích hoạt</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
