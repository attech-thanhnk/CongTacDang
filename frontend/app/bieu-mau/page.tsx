'use client';

import React, { useState } from 'react';
import Link from 'next/link';

interface BieuMauItem {
  id: string;
  code: string;
  name: string;
  group: string;
  pagePdf: string;
  targetUser: string;
  actionUrl?: string;
  actionText?: string;
  description: string;
  previewComponent: string;
}

const DANH_SACH_BIEU_MAU: BieuMauItem[] = [
  // Nhóm 1: Giao việc & Tự chấm
  {
    id: 'mau-01',
    code: 'Mẫu số 01',
    name: 'Phiếu giao / Đăng ký sản phẩm, công việc chuyên môn hằng quý',
    group: 'Nhóm I: Giao việc & Tự chấm điểm',
    pagePdf: 'Trang 40',
    targetUser: 'Cán bộ lãnh đạo, quản lý đăng ký 03 - 07 việc với cấp trên',
    actionUrl: '/mau-01',
    actionText: 'Nhập phiếu Mẫu 01',
    description: 'Dùng để thống nhất đầu ra, mốc quý, tiêu chuẩn và trọng số. Tổng trọng số nhóm nhiệm vụ cố định bằng 70 điểm. Hoàn thành trong 05 ngày làm việc đầu quý.',
    previewComponent: 'Mau01Preview',
  },
  {
    id: 'mau-02',
    code: 'Mẫu số 02',
    name: 'Phiếu tự đánh giá kết quả thực hiện sản phẩm, công việc hằng quý',
    group: 'Nhóm I: Giao việc & Tự chấm điểm',
    pagePdf: 'Trang 41',
    targetUser: 'Cá nhân tự chấm điểm kết quả thực hiện từng sản phẩm',
    actionUrl: '/mau-02',
    actionText: 'Thực hiện chấm Mẫu 02',
    description: 'Chấm điểm từng sản phẩm theo 4 tiêu chí A (Khối lượng), B (Chất lượng), C (Tiến độ), D (Hiệu quả). Tự động nhân tỷ trọng theo khối chức danh chuyên môn.',
    previewComponent: 'Mau02Preview',
  },
  {
    id: 'mau-09a',
    code: 'Mẫu số 09A',
    name: 'Phiếu tự chấm điểm đánh giá, xếp loại chất lượng hằng quý đối với cán bộ',
    group: 'Nhóm I: Giao việc & Tự chấm điểm',
    pagePdf: 'Trang 50-52',
    targetUser: 'Cá nhân giữ chức vụ lãnh đạo, quản lý',
    actionUrl: '/mau-02',
    actionText: 'Xem tích hợp ở Mẫu 02',
    description: 'Cấu trúc 100 điểm gồm: Nhóm tiêu chí chung tối đa 30 điểm (T1-T6: phẩm chất, đạo đức, đổi mới, tự soi tự sửa) + Nhóm sản phẩm chuyên môn tối đa 70 điểm.',
    previewComponent: 'Mau09APreview',
  },
  {
    id: 'mau-09b',
    code: 'Mẫu số 09B',
    name: 'Phiếu tự chấm điểm áp dụng riêng cho kỳ chuyển tiếp Quý III/2026',
    group: 'Nhóm I: Giao việc & Tự chấm điểm',
    pagePdf: 'Trang 53-62',
    targetUser: 'Áp dụng cho kỳ chuyển tiếp đầu tiên theo Hướng dẫn 03',
    description: 'Bản tự chấm theo 6 trục công việc cụ thể hóa cho giai đoạn đầu triển khai tại Đảng bộ Tổng công ty và Công ty ATTECH.',
    previewComponent: 'Mau09BPreview',
  },
  {
    id: 'mau-09c',
    code: 'Mẫu số 09C',
    name: 'Bản tự đánh giá, xếp loại của cá nhân',
    group: 'Nhóm I: Giao việc & Tự chấm điểm',
    pagePdf: 'Trang 63',
    targetUser: 'Các trường hợp thuộc đối tượng đánh giá định kỳ',
    description: 'Tóm tắt kết quả theo trục nhiệm vụ, giải trình mức độ hoàn thành nhiệm vụ trọng tâm và tự đề xuất mức xếp loại.',
    previewComponent: 'Mau09CPreview',
  },
  {
    id: 'mau-09d',
    code: 'Mẫu số 09D',
    name: 'Phụ lục kết quả thực hiện nhiệm vụ, công việc được giao trong quý',
    group: 'Nhóm I: Giao việc & Tự chấm điểm',
    pagePdf: 'Trang 64-65',
    targetUser: 'Đính kèm bản tự đánh giá của cán bộ',
    description: 'Bảng kê chi tiết các nhiệm vụ theo Trục 1 đến Trục 6, thời hạn hoàn thành, sản phẩm hoàn thành thực tế và minh chứng.',
    previewComponent: 'Mau09DPreview',
  },

  // Nhóm 2: Biến động & Điều chỉnh trong kỳ
  {
    id: 'mau-03',
    code: 'Mẫu số 03',
    name: 'Phiếu ý kiến chuyên môn độc lập / Đối soát dữ liệu chuyên ngành',
    group: 'Nhóm II: Biến động & Điều chỉnh trong kỳ',
    pagePdf: 'Trang 42',
    targetUser: 'Cơ quan thẩm định (TCCB / Đảng ủy) trưng cầu Ban chuyên môn',
    actionUrl: '/tham-dinh',
    actionText: 'Đối soát tại màn Thẩm định',
    description: 'Áp dụng khi cơ quan thẩm định hoặc cấp có thẩm quyền yêu cầu xác nhận dữ liệu chuyên môn, chỉ tiêu kỹ thuật, tiến độ hoặc an toàn bay.',
    previewComponent: 'Mau03Preview',
  },
  {
    id: 'mau-04',
    code: 'Mẫu số 04',
    name: 'Phiếu đề nghị điều chỉnh, chuyển tiếp, loại trừ / ngoại lệ sản phẩm',
    group: 'Nhóm II: Biến động & Điều chỉnh trong kỳ',
    pagePdf: 'Trang 43',
    targetUser: 'Cán bộ quản lý đề xuất điều chỉnh việc do khách quan',
    actionUrl: '/mau-01#dieu-chinh',
    actionText: 'Tạo phiếu điều chỉnh',
    description: 'Áp dụng khi phát sinh sự cố kỹ thuật ngoài kiểm soát, thiên tai, thay đổi chính sách cấp trên hoặc chậm trễ đối tác đã cảnh báo.',
    previewComponent: 'Mau04Preview',
  },
  {
    id: 'mau-05',
    code: 'Mẫu số 05',
    name: 'Phiếu ghi nhận nhiệm vụ bổ sung, khẩn cấp, đột xuất trong kỳ',
    group: 'Nhóm II: Biến động & Điều chỉnh trong kỳ',
    pagePdf: 'Trang 44',
    targetUser: 'Cấp có thẩm quyền giao nhiệm vụ đột xuất trục chính',
    actionUrl: '/mau-01#dot-xuat',
    actionText: 'Giao việc đột xuất',
    description: 'Nhiệm vụ cấp bách phát sinh trong quý. Thực hiện nguyên tắc bảo toàn 70 điểm (thay thế nhiệm vụ cũ hoặc rút bớt trọng số nhiệm vụ khác).',
    previewComponent: 'Mau05Preview',
  },

  // Nhóm 3: Đánh giá tập thể & Bỏ phiếu kín
  {
    id: 'mau-06',
    code: 'Mẫu số 06',
    name: 'Báo cáo kết quả tập thể / Lĩnh vực phục vụ liên kết trách nhiệm cán bộ',
    group: 'Nhóm III: Đánh giá tập thể & Bỏ phiếu kín',
    pagePdf: 'Trang 45',
    targetUser: 'Đảng ủy, Chi ủy, Tập thể lãnh đạo Phòng / Xưởng',
    description: 'Báo cáo số lượng và tỷ lệ hoàn thành nhiệm vụ của đơn vị trong quý để làm căn cứ liên kết trách nhiệm của người đứng đầu.',
    previewComponent: 'Mau06Preview',
  },
  {
    id: 'mau-07',
    code: 'Mẫu số 07',
    name: 'Báo cáo tự đánh giá, xếp loại chất lượng của tập thể Đảng ủy (Chi ủy, Chi bộ)',
    group: 'Nhóm III: Đánh giá tập thể & Bỏ phiếu kín',
    pagePdf: 'Trang 46-47',
    targetUser: 'Chi ủy 4 Chi bộ ATTECH (Kỹ thuật, Sản xuất, Dịch vụ, Văn phòng)',
    description: 'Đánh giá tập thể Chi ủy theo thang điểm: 30 điểm tiêu chí chung + 70 điểm kết quả thực hiện nhiệm vụ chính trị của tổ chức Đảng.',
    previewComponent: 'Mau07Preview',
  },
  {
    id: 'mau-08',
    code: 'Mẫu số 08',
    name: 'Báo cáo tổng hợp kết quả thực hiện các nhiệm vụ của cơ quan, đơn vị',
    group: 'Nhóm III: Đánh giá tập thể & Bỏ phiếu kín',
    pagePdf: 'Trang 48-49',
    targetUser: 'Phòng ban / Đơn vị trực thuộc Công ty ATTECH',
    description: 'Tổng hợp đối chiếu kế hoạch và kết quả thực hiện trên 6 mảng: Sản xuất kinh doanh, Xây dựng Đảng, An ninh quốc phòng, Đoàn thể, KHCN chuyển đổi số, Sắp xếp bộ máy.',
    previewComponent: 'Mau08Preview',
  },
  {
    id: 'mau-11',
    code: 'Mẫu số 11',
    name: 'Phiếu đánh giá, xếp loại cán bộ quý (Bỏ phiếu kín)',
    group: 'Nhóm III: Đánh giá tập thể & Bỏ phiếu kín',
    pagePdf: 'Trang 67',
    targetUser: 'Thành viên Hội nghị Chi bộ / Tập thể lãnh đạo đơn vị',
    actionUrl: '/bo-phieu',
    actionText: 'Bỏ phiếu kín điện tử',
    description: 'Phiếu bầu nặc danh không lưu danh tính. Thành viên đánh dấu mức xếp loại (Hoàn thành xuất sắc, Tốt, Hoàn thành, Không hoàn thành).',
    previewComponent: 'Mau11Preview',
  },
  {
    id: 'mau-12',
    code: 'Mẫu số 12',
    name: 'Biên bản Hội nghị đánh giá, xếp loại chất lượng cán bộ quý',
    group: 'Nhóm III: Đánh giá tập thể & Bỏ phiếu kín',
    pagePdf: 'Trang 68-69',
    targetUser: 'Thư ký Hội nghị Chi bộ / Cơ quan đơn vị',
    actionUrl: '/bo-phieu',
    actionText: 'Xem Biên bản Mẫu 12',
    description: 'Ghi nhận thời gian, địa điểm, thành phần triệu tập, thảo luận nhận xét và tiến hành quy trình bỏ phiếu kín.',
    previewComponent: 'Mau12Preview',
  },
  {
    id: 'mau-13',
    code: 'Mẫu số 13',
    name: 'Biên bản kiểm phiếu đánh giá, xếp loại chất lượng cán bộ quý',
    group: 'Nhóm III: Đánh giá tập thể & Bỏ phiếu kín',
    pagePdf: 'Trang 70-71',
    targetUser: 'Tổ kiểm phiếu Chi bộ',
    actionUrl: '/bo-phieu',
    actionText: 'Xem Biên bản kiểm phiếu',
    description: 'Hệ thống tự động tổng hợp số phiếu phát ra, thu về, hợp lệ và kết quả số phiếu bầu cho từng cán bộ theo từng mức xếp loại.',
    previewComponent: 'Mau13Preview',
  },

  // Nhóm 4: Thẩm định & Kết xuất báo cáo
  {
    id: 'mau-10',
    code: 'Mẫu số 10',
    name: 'Phiếu thẩm định, nhận xét, đề xuất xếp loại và ghi nhận giải trình',
    group: 'Nhóm IV: Thẩm định & Kết xuất Báo cáo',
    pagePdf: 'Trang 66',
    targetUser: 'Cơ quan Thẩm định (Tổ thẩm định Đảng ủy / Ban TCCB)',
    actionUrl: '/tham-dinh',
    actionText: 'Thực hiện thẩm định Mẫu 10',
    description: 'Đối soát điểm tự chấm với thẩm định. Tự động phát hiện chênh lệch >= 5 điểm và ghi nhận giải trình của cán bộ.',
    previewComponent: 'Mau10Preview',
  },
  {
    id: 'mau-14',
    code: 'Mẫu số 14',
    name: 'Danh sách đánh giá và đề xuất xếp loại quý đối với cán bộ',
    group: 'Nhóm IV: Thẩm định & Kết xuất Báo cáo',
    pagePdf: 'Trang 72',
    targetUser: 'Cấp ủy / Đơn vị trình cấp có thẩm quyền phê duyệt',
    actionUrl: 'http://localhost:5000/api/ExportReport/mau-14?periodId=3',
    actionText: 'Tải Excel Mẫu 14',
    description: 'Bảng tổng hợp đối chiếu kết quả các cấp: Điểm tự chấm -> Tập thể đề xuất -> Cơ quan tham mưu thẩm định -> Cấp phê duyệt.',
    previewComponent: 'Mau14Preview',
  },
  {
    id: 'mau-15a',
    code: 'Mẫu số 15A',
    name: 'Tổng hợp kết quả đánh giá, xếp loại cán bộ đề nghị BTV Đảng ủy TCT phê duyệt',
    group: 'Nhóm IV: Thẩm định & Kết xuất Báo cáo',
    pagePdf: 'Trang 73-74',
    targetUser: 'Đảng ủy ATTECH báo cáo Ban Thường vụ Đảng ủy TCT',
    actionUrl: 'http://localhost:5000/api/ExportReport/mau-15?periodId=3',
    actionText: 'Tải Excel Mẫu 15A',
    description: 'Bảng tổng hợp theo chức danh lãnh đạo, tính toán tỷ lệ % xếp loại Xuất sắc so với số Tốt trở lên (áp trần 20%).',
    previewComponent: 'Mau15APreview',
  },
  {
    id: 'mau-15b',
    code: 'Mẫu số 15B',
    name: 'Tổng hợp kết quả đánh giá, xếp loại cán bộ thuộc diện Đảng ủy cơ sở phê duyệt',
    group: 'Nhóm IV: Thẩm định & Kết xuất Báo cáo',
    pagePdf: 'Trang 75-76',
    targetUser: 'Cấp ủy Chi bộ trực thuộc trình Đảng ủy Công ty ATTECH',
    actionUrl: '/tham-dinh',
    actionText: 'Xem trực tiếp trên Web',
    description: 'Bảng thống kê tỷ lệ % Xuất sắc cho các chức danh thuộc thẩm quyền chuẩn y của Đảng ủy Công ty ATTECH.',
    previewComponent: 'Mau15BPreview',
  },
  {
    id: 'mau-16',
    code: 'Mẫu số 16',
    name: 'Báo cáo về kết quả đánh giá, xếp loại chất lượng cán bộ quý',
    group: 'Nhóm IV: Thẩm định & Kết xuất Báo cáo',
    pagePdf: 'Trang 77-78',
    targetUser: 'Đảng ủy ATTECH gửi Ban Thường vụ Đảng ủy Tổng công ty',
    actionUrl: '/tham-dinh',
    actionText: 'Xem báo cáo quý',
    description: 'Báo cáo chính thức tổng kết công tác đánh giá cán bộ hằng quý kèm thống kê số liệu và kiến nghị gửi cấp ủy cấp trên.',
    previewComponent: 'Mau16Preview',
  },
];

export default function BieuMauPage() {
  const [selectedMau, setSelectedMau] = useState<BieuMauItem>(DANH_SACH_BIEU_MAU[0]);
  const [activeTab, setActiveTab] = useState<string>('all');

  const groups = [
    { id: 'all', name: 'Tất cả 16 Biểu mẫu' },
    { id: 'Nhóm I: Giao việc & Tự chấm điểm', name: 'I. Giao việc & Tự chấm' },
    { id: 'Nhóm II: Biến động & Điều chỉnh trong kỳ', name: 'II. Điều chỉnh & Đột xuất' },
    { id: 'Nhóm III: Đánh giá tập thể & Bỏ phiếu kín', name: 'III. Tập thể & Bỏ phiếu kín' },
    { id: 'Nhóm IV: Thẩm định & Kết xuất Báo cáo', name: 'IV. Thẩm định & Báo cáo' },
  ];

  const filteredMaus = activeTab === 'all' 
    ? DANH_SACH_BIEU_MAU 
    : DANH_SACH_BIEU_MAU.filter(m => m.group === activeTab);

  const handlePrint = () => {
    window.print();
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="bg-white rounded-lg shadow p-5 border-l-4 border-dangdo flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="bg-red-100 text-dangdo text-xs font-bold px-2.5 py-0.5 rounded border border-red-300">
              03-HD/TVĐU
            </span>
            <h2 className="text-xl font-bold text-slate-800">
              Hệ thống Biểu mẫu Đánh giá Cán bộ (Mẫu 01 → 16)
            </h2>
          </div>
          <p className="text-sm text-slate-500 mt-1">
            Đầy đủ 16 biểu mẫu chuẩn văn bản Đảng ủy VATM áp dụng tại Công ty TNHH Kỹ thuật Quản lý bay (ATTECH).
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={handlePrint}
            className="px-4 py-2 bg-slate-800 text-white rounded hover:bg-slate-700 text-sm font-medium flex items-center gap-1.5 shadow"
          >
            🖨 In biểu mẫu (A4)
          </button>
          {selectedMau.actionUrl && (
            <Link
              href={selectedMau.actionUrl}
              className="px-4 py-2 bg-dangdo text-white rounded hover:bg-red-800 text-sm font-bold flex items-center gap-1.5 shadow"
            >
              🚀 {selectedMau.actionText || 'Mở thao tác'}
            </Link>
          )}
        </div>
      </div>

      {/* Tabs lọc nhóm */}
      <div className="flex flex-wrap gap-2 border-b pb-2">
        {groups.map((g) => (
          <button
            key={g.id}
            onClick={() => setActiveTab(g.id)}
            className={`px-3 py-1.5 rounded-full text-xs font-semibold transition ${
              activeTab === g.id
                ? 'bg-dangdo text-white shadow'
                : 'bg-white text-slate-600 hover:bg-slate-100 border'
            }`}
          >
            {g.name}
          </button>
        ))}
      </div>

      {/* Grid 2 cột: Danh sách bên trái, Khung Preview bên phải */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Cột trái: Danh sách biểu mẫu (5 cột) */}
        <div className="lg:col-span-4 space-y-2.5 max-h-[850px] overflow-y-auto pr-1">
          {filteredMaus.map((item) => (
            <div
              key={item.id}
              onClick={() => setSelectedMau(item)}
              className={`p-3.5 rounded-lg border cursor-pointer transition text-left ${
                selectedMau.id === item.id
                  ? 'bg-red-50 border-dangdo shadow-sm'
                  : 'bg-white hover:bg-slate-50 border-slate-200'
              }`}
            >
              <div className="flex justify-between items-start gap-2">
                <span className={`text-xs font-bold px-2 py-0.5 rounded ${
                  selectedMau.id === item.id ? 'bg-dangdo text-white' : 'bg-slate-100 text-slate-700'
                }`}>
                  {item.code}
                </span>
                <span className="text-[11px] text-slate-400 font-mono">
                  {item.pagePdf}
                </span>
              </div>
              <h3 className="font-semibold text-sm text-slate-800 mt-1.5 leading-snug">
                {item.name}
              </h3>
              <p className="text-xs text-slate-500 mt-1 line-clamp-2">
                {item.description}
              </p>
            </div>
          ))}
        </div>

        {/* Cột phải: Khung xem trước biểu mẫu A4 (8 cột) */}
        <div className="lg:col-span-8">
          <div className="bg-white rounded-lg shadow border border-slate-300 p-8 min-h-[850px] print:m-0 print:p-0 print:border-none print:shadow-none text-slate-800">
            {/* Header Quốc hiệu / Cờ Đảng */}
            <div className="border-b-2 border-slate-300 pb-4 mb-6">
              <div className="flex justify-between items-start text-center text-xs">
                <div className="w-1/2 text-left">
                  <p className="font-bold uppercase text-[11px]">ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VN</p>
                  <p className="font-bold uppercase text-[11px] text-dangdo">ĐẢNG ỦY CÔNG TY TNHH KT QUẢN LÝ BAY</p>
                  <p className="italic text-[11px]">Số: .....-ĐG/ATTECH</p>
                </div>
                <div className="w-1/2 text-right">
                  <p className="font-bold uppercase text-[11px]">ĐẢNG CỘNG SẢN VIỆT NAM</p>
                  <p className="italic text-[11px]">Hà Nội, ngày ..... tháng ..... năm 2026</p>
                  <div className="mt-1 inline-block border-b border-black text-right">
                    <span className="font-bold text-xs">{selectedMau.code}</span>
                  </div>
                </div>
              </div>

              <div className="text-center mt-6">
                <h2 className="text-base md:text-lg font-bold uppercase tracking-wide text-slate-900">
                  {selectedMau.name}
                </h2>
                <p className="italic text-xs text-slate-600 mt-1">
                  (Ban hành kèm theo Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026 của BTV Đảng ủy VATM)
                </p>
              </div>
            </div>

            {/* Thông tin metadata người/đơn vị */}
            <div className="text-xs space-y-1.5 mb-6 bg-slate-50 p-3 rounded border">
              <div className="grid grid-cols-2 gap-3">
                <p><strong>Kỳ đánh giá:</strong> Quý III / Năm 2026</p>
                <p><strong>Đơn vị áp dụng:</strong> Công ty TNHH Kỹ thuật Quản lý bay</p>
                <p><strong>Đối tượng thực hiện:</strong> {selectedMau.targetUser}</p>
                <p><strong>Vị trí tài liệu gốc:</strong> Hướng dẫn 03-HD/TVĐU ({selectedMau.pagePdf})</p>
              </div>
              <div className="mt-2 text-slate-600 italic border-t pt-2">
                <strong>Ghi chú nghiệp vụ:</strong> {selectedMau.description}
              </div>
            </div>

            {/* Nội dung minh họa form */}
            <div className="border border-slate-300 rounded overflow-hidden mb-6 text-xs">
              <table className="w-full border-collapse">
                <thead>
                  <tr className="bg-slate-100 text-slate-700 border-b border-slate-300">
                    <th className="p-2 border-r border-slate-300 w-10 text-center">STT</th>
                    <th className="p-2 border-r border-slate-300 text-left">Nội dung / Chỉ tiêu đánh giá</th>
                    <th className="p-2 border-r border-slate-300 w-24 text-center">Trọng số / Điểm</th>
                    <th className="p-2 border-r border-slate-300 w-28 text-center">Tiêu chuẩn đạt</th>
                    <th className="p-2 text-center w-36">Minh chứng / Kết quả</th>
                  </tr>
                </thead>
                <tbody>
                  <tr className="border-b border-slate-200">
                    <td className="p-2 text-center border-r font-bold">I</td>
                    <td className="p-2 border-r font-bold" colSpan={4}>
                      TIÊU CHUẨN CHUNG (Phẩm chất, Đạo đức, Đổi mới, Ý thức kỷ luật) - Tối đa 30 điểm
                    </td>
                  </tr>
                  <tr className="border-b border-slate-200 text-slate-600">
                    <td className="p-2 text-center border-r">1</td>
                    <td className="p-2 border-r">Tư tưởng chính trị, đạo đức lối sống, trách nhiệm nêu gương (T1-T2)</td>
                    <td className="p-2 border-r text-center font-semibold">18,0 đ</td>
                    <td className="p-2 border-r text-center">Hoàn thành tốt</td>
                    <td className="p-2 text-center italic">Đạt chuẩn nêu gương</td>
                  </tr>
                  <tr className="border-b border-slate-200 text-slate-600">
                    <td className="p-2 text-center border-r">2</td>
                    <td className="p-2 border-r">Tác phong lề lối, ý thức tổ chức kỷ luật, tự soi tự sửa (T3-T6)</td>
                    <td className="p-2 border-r text-center font-semibold">12,0 đ</td>
                    <td className="p-2 border-r text-center">Đạt 100%</td>
                    <td className="p-2 text-center italic">Không vi phạm</td>
                  </tr>
                  <tr className="border-b border-slate-200">
                    <td className="p-2 text-center border-r font-bold">II</td>
                    <td className="p-2 border-r font-bold" colSpan={4}>
                      KẾT QUẢ THỰC HIỆN NHIỆM VỤ CHUYÊN MÔN / CÔNG VIỆC TRỌNG TÂM - Cố định 70 điểm
                    </td>
                  </tr>
                  <tr className="border-b border-slate-200 text-slate-600">
                    <td className="p-2 text-center border-r">1</td>
                    <td className="p-2 border-r">Nhiệm vụ trọng tâm Trục chính (Kỹ thuật CNS, sản xuất thiết bị)</td>
                    <td className="p-2 border-r text-center font-semibold">35,0 đ</td>
                    <td className="p-2 border-r text-center">Chất lượng cao</td>
                    <td className="p-2 text-center italic">Báo cáo nghiệm thu</td>
                  </tr>
                  <tr className="border-b border-slate-200 text-slate-600">
                    <td className="p-2 text-center border-r">2</td>
                    <td className="p-2 border-r">Nhiệm vụ quản lý, điều hành và phối hợp liên phòng ban</td>
                    <td className="p-2 border-r text-center font-semibold">20,0 đ</td>
                    <td className="p-2 border-r text-center">Đúng hạn</td>
                    <td className="p-2 text-center italic">Nhật ký điều hành</td>
                  </tr>
                  <tr className="border-b border-slate-200 text-slate-600">
                    <td className="p-2 text-center border-r">3</td>
                    <td className="p-2 border-r">Nhiệm vụ thường xuyên / Báo cáo định kỳ / Chuyển đổi số</td>
                    <td className="p-2 border-r text-center font-semibold">15,0 đ</td>
                    <td className="p-2 border-r text-center">Đầy đủ</td>
                    <td className="p-2 text-center italic">Dữ liệu phần mềm</td>
                  </tr>
                  <tr className="bg-amber-50 font-bold">
                    <td className="p-2 text-center border-r" colSpan={2}>TỔNG CỘNG ĐIỂM ĐÁNH GIÁ (I + II)</td>
                    <td className="p-2 border-r text-center text-dangdo font-extrabold">100,0 đ</td>
                    <td className="p-2 text-center" colSpan={2}>
                      XẾP LOẠI: HOÀN THÀNH XUẤT SẮC NHIỆM VỤ
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            {/* Khung chữ ký các bên */}
            <div className="grid grid-cols-3 gap-4 text-center text-xs mt-10 pt-4 border-t">
              <div>
                <p className="font-bold uppercase">CÁ NHÂN TỰ ĐÁNH GIÁ</p>
                <p className="italic text-[11px] text-slate-500">(Ký, ghi rõ họ tên)</p>
                <div className="h-16"></div>
                <p className="font-semibold text-slate-700">Nguyễn Văn A</p>
              </div>

              <div>
                <p className="font-bold uppercase">TẬP THỂ LÃNH ĐẠO / CHI BỘ</p>
                <p className="italic text-[11px] text-slate-500">(Xác nhận thời điểm, ký tên)</p>
                <div className="h-16"></div>
                <p className="font-semibold text-slate-700">Chi bộ Khối Kỹ thuật</p>
              </div>

              <div>
                <p className="font-bold uppercase">CẤP CÓ THẨM QUYỀN PHÊ DUYỆT</p>
                <p className="italic text-[11px] text-slate-500">(Ký, đóng dấu nếu có)</p>
                <div className="h-16"></div>
                <p className="font-semibold text-slate-700">T/M ĐẢNG ỦY ATTECH</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
