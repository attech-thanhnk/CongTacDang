"use client";

import React from "react";
import {
  EvaluationRecordDto,
  EvaluationPeriodDto,
  BranchQuotaCheckDto
} from "@/services/evaluationService";

export type PrintTemplateType =
  | "mau01"
  | "mau02"
  | "mau09"
  | "mau10"
  | "mau13"
  | "mau14"
  | "mau15"
  | "individual";

interface Props {
  templateType: PrintTemplateType;
  record?: EvaluationRecordDto | null;
  period?: EvaluationPeriodDto | null;
  allRecords?: EvaluationRecordDto[];
  branchQuotas?: BranchQuotaCheckDto[];
}

const CRITERIA_LABELS = [
  { id: "T1", name: "Tư tưởng chính trị", desc: "Trung thành với đường lối của Đảng, pháp luật Nhà nước; giữ gìn lập trường chính trị vững vàng." },
  { id: "T2", name: "Đạo đức, lối sống", desc: "Giản dị, khiêm tốn, gương mẫu; kiên quyết phòng chống tham nhũng, lãng phí, tiêu cực, 'tự diễn biến', 'tự chuyển hóa'." },
  { id: "T3", name: "Tác phong, lề lối làm việc", desc: "Khoa học, dân chủ, sâu sát cơ sở; nêu cao tinh thần hợp tác, trách nhiệm với nhân dân và tổ chức." },
  { id: "T4", name: "Ý thức tổ chức kỷ luật", desc: "Chấp hành nghiêm túc sự phân công của Đảng, quy chế cơ quan; giữ gìn đoàn kết nội bộ." },
  { id: "T5", name: "Tinh thần đổi mới, sáng tạo", desc: "Dám nghĩ, dám làm, dám chịu trách nhiệm vì lợi ích chung; chủ động ứng dụng công nghệ, cải tiến quy trình." },
  { id: "T6", name: "Thực hiện trách nhiệm nêu gương", desc: "Nêu gương của người cán bộ lãnh đạo, quản lý cả về tư tưởng, hành động và đạo đức." }
];

export const EvaluationPrintTemplate: React.FC<Props> = ({
  templateType,
  record,
  period,
  allRecords = [],
  branchQuotas = []
}) => {
  const formatGrade = (grade?: string) => {
    switch (grade) {
      case "HoanThanhXuatSac": return "Hoàn thành xuất sắc nhiệm vụ";
      case "HoanThanhTot": return "Hoàn thành tốt nhiệm vụ";
      case "HoanThanh": return "Hoàn thành nhiệm vụ";
      case "KhongHoanThanh": return "Không hoàn thành nhiệm vụ";
      default: return grade || "Chưa xếp loại";
    }
  };

  const currentDateStr = () => {
    const d = new Date();
    return `ngày ${d.getDate().toString().padStart(2, "0")} tháng ${(d.getMonth() + 1).toString().padStart(2, "0")} năm ${d.getFullYear()}`;
  };

  const renderHeader = (subTitle?: string) => (
    <div className="grid grid-cols-2 gap-4 pb-4 border-b border-black mb-6">
      <div className="text-center font-bold text-[11pt]">
        <p className="uppercase">ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM</p>
        <p className="uppercase">ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QLB</p>
        <p className="font-semibold underline mt-1">
          {subTitle || `Chi bộ: ${record?.partyCellName || "......................................."}`}
        </p>
      </div>
      <div className="text-center text-[11pt]">
        <p className="font-bold uppercase tracking-wider">ĐẢNG CỘNG SẢN VIỆT NAM</p>
        <p className="italic mt-2">Hà Nội, {currentDateStr()}</p>
      </div>
    </div>
  );

  const renderCadreInfo = () => {
    if (!record) return null;
    return (
      <div className="my-4 bg-slate-50 p-3.5 border border-slate-300 rounded space-y-1.5 text-[12pt]">
        <div className="grid grid-cols-2 gap-2">
          <p><span className="font-semibold">Họ và tên:</span> <span className="font-bold uppercase">{record.fullName}</span></p>
          <p><span className="font-semibold">Số thẻ Đảng viên:</span> {record.partyCardNumber || "........................"}</p>
        </div>
        <div className="grid grid-cols-2 gap-2">
          <p><span className="font-semibold">Chức vụ Đảng:</span> {record.partyRole || "Đảng viên"}</p>
          <p><span className="font-semibold">Chức vụ chính quyền:</span> {record.positionTitle}</p>
        </div>
        <div className="grid grid-cols-2 gap-2">
          <p><span className="font-semibold">Đơn vị công tác:</span> {record.departmentName || "Công ty TNHH Kỹ thuật Quản lý bay"}</p>
          <p><span className="font-semibold">Khung chức danh:</span> <span className="italic">{record.jobGroup}</span></p>
        </div>
      </div>
    );
  };

  /* =========================================================================
     MẪU 01: BẢN ĐĂNG KÝ SẢN PHẨM, CÔNG VIỆC CHUYÊN MÔN (ĐẦU QUÝ)
     ========================================================================= */
  if (templateType === "mau01") {
    if (!record) return <div className="p-8 text-center text-slate-500 font-serif">Chưa có dữ liệu cán bộ.</div>;
    const totalWeight = record.tasks?.reduce((sum, t) => sum + (t.weight || 0), 0) || 0;

    return (
      <div className="party-doc-print font-serif text-black leading-relaxed p-8 max-w-4xl mx-auto bg-white text-[13pt]">
        {renderHeader()}

        <div className="text-center my-6">
          <div className="text-right text-[11pt] italic mb-2">Mẫu số 01-HD/TVĐU</div>
          <h1 className="text-[15pt] font-bold uppercase tracking-wide">
            BẢN ĐĂNG KÝ SẢN PHẨM, CÔNG VIỆC CHUYÊN MÔN HẰNG QUÝ
          </h1>
          <p className="italic text-[11.5pt] mt-1">
            (Thực hiện vào đầu quý theo Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026 của Ban Thường vụ Đảng ủy Tổng công ty)
          </p>
          <p className="font-bold text-[12.5pt] mt-1 text-red-900">
            Kỳ đánh giá: {record.periodName || period?.name || "................................................"}
          </p>
        </div>

        {renderCadreInfo()}

        <div className="mt-4 mb-2 text-[12pt] italic text-slate-800">
          Căn cứ chức trách, nhiệm vụ được giao, tôi xin đăng ký danh mục sản phẩm, công việc chuyên môn trọng tâm thực hiện trong quý như sau:
        </div>

        <table className="w-full border-collapse border border-black text-[11pt] mb-4">
          <thead>
            <tr className="bg-slate-100 text-center font-bold">
              <th className="border border-black p-2 w-10">STT</th>
              <th className="border border-black p-2">Tên sản phẩm, công việc chuyên môn</th>
              <th className="border border-black p-2 w-48">Chỉ tiêu / Tiêu chuẩn kỹ thuật đầu ra</th>
              <th className="border border-black p-2 w-20">Trọng số (Điểm)</th>
              <th className="border border-black p-2 w-28">Hạn hoàn thành</th>
            </tr>
          </thead>
          <tbody>
            {record.tasks && record.tasks.length > 0 ? (
              record.tasks.map((task, idx) => (
                <tr key={task.id || idx}>
                  <td className="border border-black p-2 text-center">{idx + 1}</td>
                  <td className="border border-black p-2 font-medium">{task.taskName}</td>
                  <td className="border border-black p-2 text-xs">{task.targetOutput}</td>
                  <td className="border border-black p-2 text-center font-bold">{task.weight?.toFixed(1)}</td>
                  <td className="border border-black p-2 text-center text-xs">
                    {task.deadline ? task.deadline.split("T")[0] : "Trong quý"}
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={5} className="border border-black p-3 text-center italic text-slate-500">
                  Chưa có danh mục nhiệm vụ đăng ký.
                </td>
              </tr>
            )}
            <tr className="font-bold bg-slate-50">
              <td colSpan={3} className="border border-black p-2 text-right uppercase">
                Tổng cộng trọng số nhiệm vụ chuyên môn:
              </td>
              <td className="border border-black p-2 text-center font-bold text-[12pt]">
                {totalWeight.toFixed(1)}
              </td>
              <td className="border border-black p-2 text-center"></td>
            </tr>
          </tbody>
        </table>

        {/* Chữ ký 2 bên: Cán bộ đăng ký & Cấp ủy / Lãnh đạo phê duyệt */}
        <div className="mt-10 grid grid-cols-2 gap-8 text-center text-[11.5pt] avoid-break">
          <div>
            <p className="font-bold uppercase">Ý KIẾN DUYỆT CỦA CẤP CÓ THẨM QUYỀN</p>
            <p className="italic text-xs">(Thủ trưởng đơn vị / Bí thư Chi bộ ký, ghi rõ họ tên)</p>
            <div className="h-24"></div>
            <p className="font-bold">....................................</p>
          </div>
          <div>
            <p className="font-bold uppercase">NGƯỜI ĐĂNG KÝ</p>
            <p className="italic text-xs">(Ký và ghi rõ họ tên)</p>
            <div className="h-24"></div>
            <p className="font-bold uppercase">{record.fullName}</p>
          </div>
        </div>
      </div>
    );
  }

  /* =========================================================================
     MẪU 02: BẢN TỰ ĐÁNH GIÁ KẾT QUẢ THỰC HIỆN CÔNG VIỆC CHUYÊN MÔN (CUỐI QUÝ - 70 ĐIỂM)
     ========================================================================= */
  if (templateType === "mau02") {
    if (!record) return <div className="p-8 text-center text-slate-500 font-serif">Chưa có dữ liệu cán bộ.</div>;
    const taskScore = record.tasksScore ?? 0;

    return (
      <div className="party-doc-print font-serif text-black leading-relaxed p-8 max-w-4xl mx-auto bg-white text-[13pt]">
        {renderHeader()}

        <div className="text-center my-6">
          <div className="text-right text-[11pt] italic mb-2">Mẫu số 02-HD/TVĐU</div>
          <h1 className="text-[15pt] font-bold uppercase tracking-wide">
            BẢN TỰ ĐÁNH GIÁ KẾT QUẢ THỰC HIỆN SẢN PHẨM, CÔNG VIỆC CHUYÊN MÔN
          </h1>
          <p className="italic text-[11.5pt] mt-1">
            (Thực hiện vào cuối quý theo Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026 của Ban Thường vụ Đảng ủy Tổng công ty)
          </p>
          <p className="font-bold text-[12.5pt] mt-1 text-red-900">
            Kỳ đánh giá: {record.periodName || period?.name || "................................................"}
          </p>
        </div>

        {renderCadreInfo()}

        <div className="mt-4 mb-2 text-[12pt] font-semibold">
          Kết quả tự chấm điểm thực hiện nhiệm vụ chuyên môn theo 4 tiêu chí A, B, C, D (Tối đa 70.0 điểm):
        </div>

        <table className="w-full border-collapse border border-black text-[10.5pt] mb-4">
          <thead>
            <tr className="bg-slate-100 text-center font-bold">
              <th className="border border-black p-1.5 w-8">STT</th>
              <th className="border border-black p-1.5">Tên sản phẩm, công việc chuyên môn</th>
              <th className="border border-black p-1.5 w-14">Trọng số</th>
              <th className="border border-black p-1.5 w-14">A (Khối lượng)</th>
              <th className="border border-black p-1.5 w-14">B (Chất lượng)</th>
              <th className="border border-black p-1.5 w-14">C (Tiến độ)</th>
              <th className="border border-black p-1.5 w-14">D (Hiệu quả)</th>
              <th className="border border-black p-1.5 w-16">Điểm tự chấm</th>
            </tr>
          </thead>
          <tbody>
            {record.tasks && record.tasks.length > 0 ? (
              record.tasks.map((task, idx) => (
                <tr key={task.id || idx}>
                  <td className="border border-black p-1.5 text-center">{idx + 1}</td>
                  <td className="border border-black p-1.5 font-medium">{task.taskName}</td>
                  <td className="border border-black p-1.5 text-center font-bold">{task.weight?.toFixed(1)}</td>
                  <td className="border border-black p-1.5 text-center font-mono text-xs">{Math.round((task.criteriaA_Ratio ?? 1) * 100)}%</td>
                  <td className="border border-black p-1.5 text-center font-mono text-xs">{Math.round((task.criteriaB_Ratio ?? 1) * 100)}%</td>
                  <td className="border border-black p-1.5 text-center font-mono text-xs">{Math.round((task.criteriaC_Ratio ?? 1) * 100)}%</td>
                  <td className="border border-black p-1.5 text-center font-mono text-xs">{Math.round((task.criteriaD_Ratio ?? 1) * 100)}%</td>
                  <td className="border border-black p-1.5 text-center font-bold text-blue-900">
                    {(task.selfScore ?? task.weight)?.toFixed(2)}
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={8} className="border border-black p-3 text-center italic text-slate-500">
                  Chưa có danh mục nhiệm vụ.
                </td>
              </tr>
            )}
            <tr className="font-bold bg-slate-50">
              <td colSpan={7} className="border border-black p-2 text-right uppercase">
                TỔNG CỘNG ĐIỂM SẢN PHẨM CHUYÊN MÔN ĐẠT ĐƯỢC (Tối đa 70.0đ):
              </td>
              <td className="border border-black p-2 text-center text-blue-900 font-extrabold text-[12pt]">
                {taskScore.toFixed(2)}đ
              </td>
            </tr>
          </tbody>
        </table>

        <div className="mt-8 grid grid-cols-2 gap-8 text-center text-[11.5pt] avoid-break">
          <div>
            <p className="font-bold uppercase">NHẬN XÉT CỦA LÃNH ĐẠO TRỰC TIẾP</p>
            <p className="italic text-xs">(Ký, ghi rõ họ tên)</p>
            <div className="h-24"></div>
            <p className="font-bold">....................................</p>
          </div>
          <div>
            <p className="font-bold uppercase">NGƯỜI TỰ ĐÁNH GIÁ</p>
            <p className="italic text-xs">(Ký và ghi rõ họ tên)</p>
            <div className="h-24"></div>
            <p className="font-bold uppercase">{record.fullName}</p>
          </div>
        </div>
      </div>
    );
  }

  /* =========================================================================
     MẪU 09: PHIẾU TỰ ĐÁNH GIÁ TIÊU CHÍ CHUNG (CUỐI QUÝ - 30 ĐIỂM)
     ========================================================================= */
  if (templateType === "mau09") {
    if (!record) return <div className="p-8 text-center text-slate-500 font-serif">Chưa có dữ liệu cán bộ.</div>;
    const generalScore = record.generalCriteriaScore ?? 0;

    return (
      <div className="party-doc-print font-serif text-black leading-relaxed p-8 max-w-4xl mx-auto bg-white text-[13pt]">
        {renderHeader()}

        <div className="text-center my-6">
          <div className="text-right text-[11pt] italic mb-2">Mẫu số 09-HD/TVĐU</div>
          <h1 className="text-[15pt] font-bold uppercase tracking-wide">
            PHIẾU TỰ ĐÁNH GIÁ TIÊU CHÍ CHUNG CỦA CÁN BỘ LÃNH ĐẠO, QUẢN LÝ
          </h1>
          <p className="italic text-[11.5pt] mt-1">
            (Thực hiện vào cuối quý theo Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026 của Ban Thường vụ Đảng ủy Tổng công ty)
          </p>
          <p className="font-bold text-[12.5pt] mt-1 text-red-900">
            Kỳ đánh giá: {record.periodName || period?.name || "................................................"}
          </p>
        </div>

        {renderCadreInfo()}

        <div className="mt-4 mb-2 text-[12pt] font-semibold">
          Nội dung tự chấm điểm 06 tiêu chí chung (Mỗi tiêu chí tối đa 5.0 điểm, tổng điểm tối đa 30.0 điểm):
        </div>

        <table className="w-full border-collapse border border-black text-[11pt] mb-4">
          <thead>
            <tr className="bg-slate-100 text-center font-bold">
              <th className="border border-black p-2 w-12">Mã</th>
              <th className="border border-black p-2">Tiêu chí đánh giá</th>
              <th className="border border-black p-2 w-24">Điểm tối đa</th>
              <th className="border border-black p-2 w-28">Cán bộ tự chấm</th>
            </tr>
          </thead>
          <tbody>
            {CRITERIA_LABELS.map((item, idx) => {
              const score = record.generalScores && record.generalScores[idx] !== undefined ? record.generalScores[idx] : 5.0;
              return (
                <tr key={item.id}>
                  <td className="border border-black p-2 text-center font-bold text-slate-700">{item.id}</td>
                  <td className="border border-black p-2">
                    <p className="font-bold">{item.name}</p>
                    <p className="text-xs text-slate-600 mt-0.5">{item.desc}</p>
                  </td>
                  <td className="border border-black p-2 text-center font-semibold">5.0 đ</td>
                  <td className="border border-black p-2 text-center font-bold text-blue-900 text-[12pt]">
                    {score.toFixed(1)} đ
                  </td>
                </tr>
              );
            })}
            <tr className="font-bold bg-slate-50">
              <td colSpan={2} className="border border-black p-2 text-right uppercase">
                TỔNG CỘNG ĐIỂM TIÊU CHÍ CHUNG (Tối đa 30.0đ):
              </td>
              <td className="border border-black p-2 text-center">30.0 đ</td>
              <td className="border border-black p-2 text-center text-blue-900 font-extrabold text-[12pt]">
                {generalScore.toFixed(1)} đ
              </td>
            </tr>
          </tbody>
        </table>

        <div className="mt-8 text-right text-[11.5pt] avoid-break">
          <div className="inline-block text-center pr-8">
            <p className="font-bold uppercase">NGƯỜI TỰ ĐÁNH GIÁ</p>
            <p className="italic text-xs">(Ký và ghi rõ họ tên)</p>
            <div className="h-24"></div>
            <p className="font-bold uppercase">{record.fullName}</p>
          </div>
        </div>
      </div>
    );
  }

  /* =========================================================================
     MẪU 10: BẢN NHẬN XÉT, ĐÁNH GIÁ CỦA CẤP ỦY NƠI SINH HOẠT
     ========================================================================= */
  if (templateType === "mau10") {
    if (!record) return <div className="p-8 text-center text-slate-500 font-serif">Chưa có dữ liệu cán bộ.</div>;

    return (
      <div className="party-doc-print font-serif text-black leading-relaxed p-8 max-w-4xl mx-auto bg-white text-[13pt]">
        {renderHeader()}

        <div className="text-center my-6">
          <div className="text-right text-[11pt] italic mb-2">Mẫu số 10-HD/TVĐU</div>
          <h1 className="text-[15pt] font-bold uppercase tracking-wide">
            BẢN NHẬN XÉT, ĐÁNH GIÁ CỦA CẤP ỦY NƠI SINH HOẠT
          </h1>
          <p className="italic text-[11.5pt] mt-1">
            (Tại Hội nghị kiểm điểm định kỳ theo Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026 của Ban Thường vụ Đảng ủy Tổng công ty)
          </p>
          <p className="font-bold text-[12.5pt] mt-1 text-red-900">
            Kỳ đánh giá: {record.periodName || period?.name || "................................................"}
          </p>
        </div>

        {renderCadreInfo()}

        <div className="space-y-4 my-6 text-[12.5pt]">
          <div className="border border-black p-4 rounded">
            <h3 className="font-bold uppercase text-[12pt] border-b border-black pb-1 mb-2">
              1. Ý kiến nhận xét, đánh giá của Cấp ủy Chi bộ:
            </h3>
            <div className="text-justify leading-relaxed italic text-slate-900 min-h-[120px]">
              {record.partyCellComment ? (
                <p>{record.partyCellComment}</p>
              ) : (
                <div className="text-slate-400 not-italic space-y-3 font-mono text-[11pt] pt-1">
                  <p>................................................................................................................................................................</p>
                  <p>................................................................................................................................................................</p>
                  <p>................................................................................................................................................................</p>
                  <p>................................................................................................................................................................</p>
                </div>
              )}
            </div>
          </div>

          <div className="border border-black p-4 rounded bg-slate-50">
            <h3 className="font-bold uppercase text-[12pt] border-b border-black pb-1 mb-2">
              2. Đề xuất mức xếp loại chất lượng của Cấp ủy Chi bộ:
            </h3>
            <div className="flex items-center justify-between mt-3 text-[13pt]">
              <span>Cấp ủy Chi bộ thống nhất đề xuất mức xếp loại:</span>
              <span className="font-bold uppercase text-red-900 underline">
                {record.partyCellProposedGrade ? formatGrade(record.partyCellProposedGrade) : "................................................"}
              </span>
            </div>
          </div>
        </div>

        <div className="mt-12 text-right text-[11.5pt] avoid-break">
          <div className="inline-block text-center pr-8">
            <p className="font-bold uppercase">TM. CẤP ỦY CHI BỘ</p>
            <p className="italic text-xs">BÍ THƯ (Ký và ghi rõ họ tên)</p>
            <div className="h-28"></div>
            <p className="font-bold">....................................</p>
          </div>
        </div>
      </div>
    );
  }

  /* =========================================================================
     MẪU 13: BIÊN BẢN KIỂM PHIẾU ĐÁNH GIÁ, XẾP LOẠI CÁN BỘ CỦA CHI BỘ
     ========================================================================= */
  if (templateType === "mau13") {
    if (!record) return <div className="p-8 text-center text-slate-500 font-serif">Chưa có dữ liệu cán bộ.</div>;
    const totalVotes = record.totalVoters || 0;
    const hasVoted = totalVotes > 0;
    const pctExcellent = hasVoted ? ((record.votesExcellent || 0) / totalVotes * 100).toFixed(1) + "%" : ".....";
    const pctGood = hasVoted ? ((record.votesGood || 0) / totalVotes * 100).toFixed(1) + "%" : ".....";
    const pctSatisfactory = hasVoted ? ((record.votesSatisfactory || 0) / totalVotes * 100).toFixed(1) + "%" : ".....";
    const pctUnsatisfactory = hasVoted ? ((record.votesUnsatisfactory || 0) / totalVotes * 100).toFixed(1) + "%" : ".....";

    return (
      <div className="party-doc-print font-serif text-black leading-relaxed p-8 max-w-4xl mx-auto bg-white text-[13pt]">
        {renderHeader()}

        <div className="text-center my-6">
          <div className="text-right text-[11pt] italic mb-2">Mẫu số 13-HD/TVĐU</div>
          <h1 className="text-[15pt] font-bold uppercase tracking-wide">
            BIÊN BẢN KIỂM PHIẾU ĐÁNH GIÁ, XẾP LOẠI CÁN BỘ LÃNH ĐẠO, QUẢN LÝ
          </h1>
          <p className="italic text-[11.5pt] mt-1">
            (Tại Hội nghị Chi bộ kiểm điểm hằng quý theo Hướng dẫn số 03-HD/TVĐU)
          </p>
          <p className="font-bold text-[12.5pt] mt-1 text-red-900">
            Kỳ đánh giá: {record.periodName || period?.name || "................................................"}
          </p>
        </div>

        <div className="space-y-3 my-4 text-[12pt]">
          <p>Hôm nay, vào hồi ..... giờ ..... ngày ..... tháng ..... năm {new Date().getFullYear()};</p>
          <p>Tại phòng họp Chi bộ {record.partyCellName || "................................................................"};</p>
          <p>Chi bộ đã tiến hành bỏ phiếu kín đánh giá, xếp loại đối với đồng chí: <strong className="uppercase">{record.fullName}</strong> - Chức vụ: <strong>{record.positionTitle || "................................"}</strong>.</p>
          <p>• Tổng số đảng viên của Chi bộ triệu tập: <strong>{hasVoted ? totalVotes : "....."}</strong> đồng chí.</p>
          <p>• Số đảng viên có mặt tham gia bỏ phiếu: <strong>{hasVoted ? totalVotes : "....."}</strong> đồng chí (Số vắng mặt: ..... đồng chí).</p>
          <p>• Số phiếu phát ra: <strong>{hasVoted ? totalVotes : "....."}</strong> phiếu; Số phiếu thu về: <strong>{hasVoted ? totalVotes : "....."}</strong> phiếu; Số phiếu hợp lệ: <strong>{hasVoted ? totalVotes : "....."}</strong> phiếu.</p>
        </div>

        <div className="my-4 font-bold text-[12.5pt]">
          KẾT QUẢ KIỂM PHIẾU TÍN NHIỆM:
        </div>

        <table className="w-full border-collapse border border-black text-[11pt] mb-4">
          <thead>
            <tr className="bg-slate-100 text-center font-bold">
              <th className="border border-black p-2 w-12">STT</th>
              <th className="border border-black p-2">Mức xếp loại chất lượng</th>
              <th className="border border-black p-2 w-32">Số phiếu tán thành</th>
              <th className="border border-black p-2 w-32">Tỷ lệ % / Tổng số</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td className="border border-black p-2 text-center">1</td>
              <td className="border border-black p-2 font-semibold">Hoàn thành xuất sắc nhiệm vụ</td>
              <td className="border border-black p-2 text-center font-bold text-red-800 text-[12pt]">
                {hasVoted ? (record.votesExcellent ?? 0) : "....."}
              </td>
              <td className="border border-black p-2 text-center font-bold">{pctExcellent}</td>
            </tr>
            <tr>
              <td className="border border-black p-2 text-center">2</td>
              <td className="border border-black p-2 font-semibold">Hoàn thành tốt nhiệm vụ</td>
              <td className="border border-black p-2 text-center font-bold text-blue-800 text-[12pt]">
                {hasVoted ? (record.votesGood ?? 0) : "....."}
              </td>
              <td className="border border-black p-2 text-center font-bold">{pctGood}</td>
            </tr>
            <tr>
              <td className="border border-black p-2 text-center">3</td>
              <td className="border border-black p-2 font-semibold">Hoàn thành nhiệm vụ</td>
              <td className="border border-black p-2 text-center font-bold text-emerald-800 text-[12pt]">
                {hasVoted ? (record.votesSatisfactory ?? 0) : "....."}
              </td>
              <td className="border border-black p-2 text-center font-bold">{pctSatisfactory}</td>
            </tr>
            <tr>
              <td className="border border-black p-2 text-center">4</td>
              <td className="border border-black p-2 font-semibold">Không hoàn thành nhiệm vụ</td>
              <td className="border border-black p-2 text-center font-bold text-slate-500 text-[12pt]">
                {hasVoted ? (record.votesUnsatisfactory ?? 0) : "....."}
              </td>
              <td className="border border-black p-2 text-center font-bold">{pctUnsatisfactory}</td>
            </tr>
            <tr className="font-bold bg-slate-50">
              <td colSpan={2} className="border border-black p-2 text-right uppercase">Căn cứ kết quả bỏ phiếu, Chi bộ đề xuất mức:</td>
              <td colSpan={2} className="border border-black p-2 text-center text-red-900 uppercase font-extrabold text-[12.5pt]">
                {record.partyCellProposedGrade ? formatGrade(record.partyCellProposedGrade) : "........................................"}
              </td>
            </tr>
          </tbody>
        </table>

        <div className="mt-10 grid grid-cols-2 gap-8 text-center text-[11.5pt] avoid-break">
          <div>
            <p className="font-bold uppercase">TM. TỔ KIỂM PHIẾU</p>
            <p className="italic text-xs">TỔ TRƯỞNG (Ký, ghi rõ họ tên)</p>
            <div className="h-24"></div>
            <p className="font-bold">....................................</p>
          </div>
          <div>
            <p className="font-bold uppercase">TM. CHI BỘ</p>
            <p className="italic text-xs">BÍ THƯ CHI BỘ (Ký, ghi rõ họ tên)</p>
            <div className="h-24"></div>
            <p className="font-bold">....................................</p>
          </div>
        </div>
      </div>
    );
  }

  /* =========================================================================
     MẪU 14: BẢNG TỔNG HỢP KẾT QUẢ ĐÁNH GIÁ, XẾP LOẠI CÁN BỘ TOÀN ĐẢNG BỘ (A4 NGANG)
     ========================================================================= */
  if (templateType === "mau14") {
    return (
      <div className="party-doc-print font-serif text-black leading-relaxed p-6 max-w-6xl mx-auto bg-white text-[11pt]">
        <div className="grid grid-cols-2 gap-4 pb-3 border-b border-black mb-4">
          <div className="text-center font-bold text-[10pt]">
            <p className="uppercase">ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM</p>
            <p className="uppercase">ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QLB</p>
            <p className="font-normal italic">Số: .....-BC/ĐU-ATTECH</p>
          </div>
          <div className="text-center text-[10pt]">
            <p className="font-bold uppercase">ĐẢNG CỘNG SẢN VIỆT NAM</p>
            <p className="italic mt-1">Hà Nội, {currentDateStr()}</p>
          </div>
        </div>

        <div className="text-center my-4">
          <div className="text-right text-[10pt] italic mb-1">Mẫu số 14-HD/TVĐU</div>
          <h1 className="text-[15pt] font-bold uppercase tracking-wide">
            BẢNG TỔNG HỢP KẾT QUẢ ĐÁNH GIÁ, XẾP LOẠI CÁN BỘ LÃNH ĐẠO, QUẢN LÝ
          </h1>
          <p className="font-semibold text-[11pt]">
            (Kèm theo Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026 của Ban Thường vụ Đảng ủy Tổng công ty)
          </p>
          <p className="font-bold text-red-900 mt-1 text-[12pt]">
            Kỳ đánh giá: {period?.name || "................................................"}
          </p>
        </div>

        <table className="w-full border-collapse border border-black text-[9.5pt] mb-4">
          <thead>
            <tr className="bg-slate-100 text-center font-bold">
              <th className="border border-black p-1.5 w-8">STT</th>
              <th className="border border-black p-1.5 w-36">Họ và tên cán bộ</th>
              <th className="border border-black p-1.5 w-24">Chức vụ Đảng</th>
              <th className="border border-black p-1.5 w-28">Chức danh chính quyền</th>
              <th className="border border-black p-1.5 w-28">Chi bộ sinh hoạt</th>
              <th className="border border-black p-1.5 w-14">Điểm tự chấm</th>
              <th className="border border-black p-1.5 w-20">Phiếu Chi bộ (XS/T/HT)</th>
              <th className="border border-black p-1.5 w-14">Điểm thẩm định</th>
              <th className="border border-black p-1.5 w-14">Điểm chính thức</th>
              <th className="border border-black p-1.5 w-28">Xếp loại BTV chuẩn y</th>
            </tr>
          </thead>
          <tbody>
            {allRecords && allRecords.length > 0 ? (
              allRecords.map((r, idx) => (
                <tr key={r.id || idx}>
                  <td className="border border-black p-1 text-center">{idx + 1}</td>
                  <td className="border border-black p-1 font-semibold">{r.fullName}</td>
                  <td className="border border-black p-1 text-xs">{r.partyRole}</td>
                  <td className="border border-black p-1 text-xs">{r.positionTitle}</td>
                  <td className="border border-black p-1 text-xs">{r.partyCellName}</td>
                  <td className="border border-black p-1 text-center font-semibold">
                    {r.totalSelfScore ? r.totalSelfScore.toFixed(1) : "-"}
                  </td>
                  <td className="border border-black p-1 text-center text-xs font-mono">
                    {r.votesExcellent}/{r.votesGood}/{r.votesSatisfactory}
                  </td>
                  <td className="border border-black p-1 text-center font-semibold text-blue-900">
                    {r.appraisalScore ? r.appraisalScore.toFixed(1) : "-"}
                  </td>
                  <td className="border border-black p-1 text-center font-bold text-red-900">
                    {r.finalScore ? r.finalScore.toFixed(1) : "-"}
                  </td>
                  <td className="border border-black p-1 font-bold text-center text-xs">
                    {formatGrade(r.finalGrade)}
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={10} className="border border-black p-3 text-center italic text-slate-500">
                  Chưa có dữ liệu hồ sơ đánh giá trong kỳ này.
                </td>
              </tr>
            )}
          </tbody>
        </table>

        <div className="mt-8 grid grid-cols-3 gap-4 text-center text-[10.5pt] avoid-break">
          <div>
            <p className="font-bold uppercase">NGƯỜI LẬP BIỂU</p>
            <p className="italic text-xs">(Ký, ghi rõ họ tên)</p>
            <div className="h-20"></div>
            <p className="font-bold">....................................</p>
          </div>
          <div>
            <p className="font-bold uppercase">TRƯỞNG BAN TỔ CHỨC ĐẢNG ỦY</p>
            <p className="italic text-xs">(Ký, ghi rõ họ tên)</p>
            <div className="h-20"></div>
            <p className="font-bold">....................................</p>
          </div>
          <div>
            <p className="font-bold uppercase">TM. BAN THƯỜNG VỤ</p>
            <p className="italic text-xs">BÍ THƯ ĐẢNG ỦY (Ký, đóng dấu)</p>
            <div className="h-20"></div>
            <p className="font-bold">....................................</p>
          </div>
        </div>
      </div>
    );
  }

  /* =========================================================================
     MẪU 15: BÁO CÁO THẨM ĐỊNH & KIỂM SOÁT HẠN NGẠCH 20% XUẤT SẮC (A4 DỌC)
     ========================================================================= */
  if (templateType === "mau15") {
    return (
      <div className="party-doc-print font-serif text-black leading-relaxed p-8 max-w-4xl mx-auto bg-white text-[12.5pt]">
        <div className="grid grid-cols-2 gap-4 pb-3 border-b border-black mb-4">
          <div className="text-center font-bold text-[11pt]">
            <p className="uppercase">ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QLB</p>
            <p className="uppercase text-red-900 font-extrabold">TỔ THẨM ĐỊNH ĐẢNG ỦY</p>
            <p className="font-normal italic">Số: .....-BC/TTĐ</p>
          </div>
          <div className="text-center text-[11pt]">
            <p className="font-bold uppercase">ĐẢNG CỘNG SẢN VIỆT NAM</p>
            <p className="italic mt-1">Hà Nội, {currentDateStr()}</p>
          </div>
        </div>

        <div className="text-center my-6">
          <div className="text-right text-[11pt] italic mb-1">Mẫu số 15-HD/TVĐU</div>
          <h1 className="text-[15pt] font-bold uppercase tracking-wide">
            BÁO CÁO THẨM ĐỊNH VÀ RÀ SOÁT TỶ LỆ 20% XẾP LOẠI XUẤT SẮC
          </h1>
          <p className="italic text-[11pt]">
            (Kèm theo Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026 của Ban Thường vụ Đảng ủy Tổng công ty)
          </p>
          <p className="font-bold text-red-900 mt-1 text-[12.5pt]">
            Kỳ đánh giá: {period?.name || "................................................"}
          </p>
        </div>

        <div className="mb-4 text-[12pt] space-y-1">
          <p><strong>Căn cứ:</strong> Hướng dẫn số 03-HD/TVĐU của Ban Thường vụ Đảng ủy Tổng công ty;</p>
          <p className="italic pl-4 text-slate-800">
            "Số cán bộ được xếp loại Hoàn thành xuất sắc nhiệm vụ tại mỗi Chi bộ không được vượt quá <strong>20%</strong> tổng số cán bộ được xếp loại Hoàn thành tốt nhiệm vụ trở lên của Chi bộ đó."
          </p>
        </div>

        <table className="w-full border-collapse border border-black text-[11pt] mb-4">
          <thead>
            <tr className="bg-slate-100 text-center font-bold">
              <th className="border border-black p-2 w-8">STT</th>
              <th className="border border-black p-2">Tên Chi bộ</th>
              <th className="border border-black p-2 w-16">Tổng sĩ số</th>
              <th className="border border-black p-2 w-20">Đạt loại Tốt trở lên</th>
              <th className="border border-black p-2 w-20">Trần 20% cho phép</th>
              <th className="border border-black p-2 w-20">Đề xuất Xuất sắc</th>
              <th className="border border-black p-2 w-24">Kết luận Thẩm định</th>
            </tr>
          </thead>
          <tbody>
            {branchQuotas && branchQuotas.length > 0 ? (
              branchQuotas.map((b, idx) => (
                <tr key={b.branchId || idx}>
                  <td className="border border-black p-2 text-center">{idx + 1}</td>
                  <td className="border border-black p-2 font-semibold">{b.branchName}</td>
                  <td className="border border-black p-2 text-center">{b.totalCadres}</td>
                  <td className="border border-black p-2 text-center font-medium">{b.goodOrBetterCount}</td>
                  <td className="border border-black p-2 text-center font-bold text-blue-900">{b.maxExcellentAllowed}</td>
                  <td className="border border-black p-2 text-center font-bold text-red-900">{b.proposedExcellentCount}</td>
                  <td className="border border-black p-2 text-center font-bold">
                    {b.isExceedingQuota ? (
                      <span className="text-red-700 bg-red-50 px-1 py-0.5 rounded border border-red-300 text-xs">VƯỢT TRẦN</span>
                    ) : (
                      <span className="text-emerald-800 text-xs">HỢP LỆ (≤ 20%)</span>
                    )}
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={7} className="border border-black p-3 text-center italic text-slate-500">
                  Chưa có dữ liệu thống kê hạn ngạch các Chi bộ.
                </td>
              </tr>
            )}
          </tbody>
        </table>

        <div className="mt-4 p-3 bg-slate-50 border border-slate-400 rounded text-[11.5pt]">
          <p className="font-bold underline mb-1">Ý KIẾN KẾT LUẬN CỦA TỔ THẨM ĐỊNH:</p>
          <div className="italic text-slate-800 space-y-2 pt-1">
            <p className="font-mono text-slate-400 not-italic">
              ................................................................................................................................................................
            </p>
            <p className="font-mono text-slate-400 not-italic">
              ................................................................................................................................................................
            </p>
          </div>
        </div>

        <div className="mt-10 grid grid-cols-2 gap-8 text-center text-[11pt] avoid-break">
          <div>
            <p className="font-bold uppercase">THƯ KÝ TỔ THẨM ĐỊNH</p>
            <p className="italic text-xs">(Ký, ghi rõ họ tên)</p>
            <div className="h-24"></div>
            <p className="font-bold">....................................</p>
          </div>
          <div>
            <p className="font-bold uppercase">TỔ TRƯỞNG TỔ THẨM ĐỊNH</p>
            <p className="italic text-xs">(Ký, ghi rõ họ tên)</p>
            <div className="h-24"></div>
            <p className="font-bold">....................................</p>
          </div>
        </div>
      </div>
    );
  }

  /* =========================================================================
     MẪU TỔNG HỢP: HỒ SƠ ĐÁNH GIÁ CÁ NHÂN TOÀN DIỆN (TỔNG HỢP)
     ========================================================================= */
  if (templateType === "individual") {
    if (!record) return <div className="p-8 text-center text-slate-500 font-serif">Chưa có dữ liệu cán bộ.</div>;
    const totalWeight = record.tasks?.reduce((sum, t) => sum + (t.weight || 0), 0) || 0;
    const taskScore = record.tasksScore ?? 0;
    const generalScore = record.generalCriteriaScore ?? 0;
    const totalScore = record.totalSelfScore ?? (taskScore + generalScore);

    return (
      <div className="party-doc-print font-serif text-black leading-relaxed p-8 max-w-4xl mx-auto bg-white text-[13pt]">
        {renderHeader()}

        <div className="text-center my-6">
          <h1 className="text-[16pt] font-bold uppercase tracking-wide">
            HỒ SƠ TỔNG HỢP ĐÁNH GIÁ, XẾP LOẠI CÁN BỘ LÃNH ĐẠO, QUẢN LÝ
          </h1>
          <p className="italic text-[12pt] mt-1">
            (Tổng hợp các Mẫu 01, 02, 09, 10, 13 theo Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026)
          </p>
          <p className="font-bold text-[13pt] mt-1 text-red-800">
            Kỳ đánh giá: {record.periodName || period?.name || "................................................"}
          </p>
        </div>

        {renderCadreInfo()}

        {/* PHẦN I: NHIỆM VỤ CHUYÊN MÔN */}
        <div className="mt-6">
          <h2 className="font-bold text-[13pt] uppercase border-b-2 border-black pb-1 mb-3">
            I. KẾT QUẢ THỰC HIỆN NHIỆM VỤ CHUYÊN MÔN (Mẫu 01 & 02 - Tối đa 70.0đ)
          </h2>
          <table className="w-full border-collapse border border-black text-[11pt] mb-3">
            <thead>
              <tr className="bg-slate-100 text-center font-bold">
                <th className="border border-black p-1.5 w-8">STT</th>
                <th className="border border-black p-1.5">Nhiệm vụ trọng tâm đăng ký</th>
                <th className="border border-black p-1.5 w-36">Chỉ tiêu / Sản phẩm đầu ra</th>
                <th className="border border-black p-1.5 w-16">Trọng số</th>
                <th className="border border-black p-1.5 w-24">Tỷ lệ HT (A-B-C-D)</th>
                <th className="border border-black p-1.5 w-16">Điểm đạt</th>
              </tr>
            </thead>
            <tbody>
              {record.tasks && record.tasks.length > 0 ? (
                record.tasks.map((task, idx) => (
                  <tr key={task.id || idx}>
                    <td className="border border-black p-1.5 text-center">{idx + 1}</td>
                    <td className="border border-black p-1.5 font-medium">{task.taskName}</td>
                    <td className="border border-black p-1.5 text-xs">{task.targetOutput}</td>
                    <td className="border border-black p-1.5 text-center font-bold">{task.weight?.toFixed(1)}</td>
                    <td className="border border-black p-1.5 text-center text-xs font-mono">
                      {Math.round((task.criteriaA_Ratio ?? 1) * 100)}% / {Math.round((task.criteriaB_Ratio ?? 1) * 100)}% / {Math.round((task.criteriaC_Ratio ?? 1) * 100)}% / {Math.round((task.criteriaD_Ratio ?? 1) * 100)}%
                    </td>
                    <td className="border border-black p-1.5 text-center font-bold text-blue-900">
                      {(task.selfScore ?? task.weight)?.toFixed(2)}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={6} className="border border-black p-3 text-center italic text-slate-500">
                    Chưa đăng ký nhiệm vụ.
                  </td>
                </tr>
              )}
              <tr className="font-bold bg-slate-50">
                <td colSpan={3} className="border border-black p-2 text-right uppercase">Tổng cộng điểm nhiệm vụ chuyên môn:</td>
                <td className="border border-black p-2 text-center text-red-700">{totalWeight.toFixed(1)}đ</td>
                <td className="border border-black p-2"></td>
                <td className="border border-black p-2 text-center text-blue-900 font-extrabold text-[12pt]">
                  {taskScore.toFixed(2)}đ
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        {/* PHẦN II: TIÊU CHÍ CHUNG */}
        <div className="mt-6">
          <h2 className="font-bold text-[13pt] uppercase border-b-2 border-black pb-1 mb-3">
            II. KẾT QUẢ TỰ ĐÁNH GIÁ TIÊU CHÍ CHUNG (Mẫu 09 - Tối đa 30.0đ)
          </h2>
          <table className="w-full border-collapse border border-black text-[11pt] mb-3">
            <thead>
              <tr className="bg-slate-100 text-center font-bold">
                <th className="border border-black p-1.5 w-8">STT</th>
                <th className="border border-black p-1.5">Tiêu chí đánh giá</th>
                <th className="border border-black p-1.5 w-24">Điểm tối đa</th>
                <th className="border border-black p-1.5 w-24">Cán bộ tự chấm</th>
              </tr>
            </thead>
            <tbody>
              {CRITERIA_LABELS.map((item, idx) => {
                const score = record.generalScores && record.generalScores[idx] !== undefined ? record.generalScores[idx] : 5.0;
                return (
                  <tr key={item.id}>
                    <td className="border border-black p-1.5 text-center font-semibold">{item.id}</td>
                    <td className="border border-black p-1.5">{item.name}</td>
                    <td className="border border-black p-1.5 text-center">5.0 đ</td>
                    <td className="border border-black p-1.5 text-center font-bold text-blue-900">{score.toFixed(1)} đ</td>
                  </tr>
                );
              })}
              <tr className="font-bold bg-slate-50">
                <td colSpan={2} className="border border-black p-2 text-right uppercase">Tổng cộng Tiêu chí chung:</td>
                <td className="border border-black p-2 text-center">30.0 đ</td>
                <td className="border border-black p-2 text-center text-blue-900 font-extrabold text-[12pt]">
                  {generalScore.toFixed(1)} đ
                </td>
              </tr>
            </tbody>
          </table>
          <div className="p-3 bg-slate-100 border border-black rounded text-[12pt] flex justify-between items-center">
            <span><strong>TỔNG ĐIỂM TỰ ĐÁNH GIÁ:</strong> <span className="text-[14pt] text-red-900 font-bold">{totalScore.toFixed(2)} / 100 điểm</span></span>
            <span><strong>Tự đề xuất xếp loại:</strong> <span className="font-bold underline text-blue-900">{formatGrade(record.selfProposedGrade)}</span></span>
          </div>
        </div>

        {/* PHẦN III: CHI BỘ */}
        <div className="mt-6 avoid-break">
          <h2 className="font-bold text-[13pt] uppercase border-b-2 border-black pb-1 mb-3">
            III. Ý KIẾN CHI BỘ & KẾT QUẢ BỎ PHIẾU KÍN (Mẫu 10 & 13)
          </h2>
          <div className="border border-black p-3 rounded mb-3 text-[12pt]">
            <p className="font-bold mb-1">1. Nhận xét của Cấp ủy Chi bộ:</p>
            <div className="italic text-slate-800 min-h-[40px] pl-2 border-l-2 border-slate-400">
              {record.partyCellComment ? (
                <p>{record.partyCellComment}</p>
              ) : (
                <p className="font-mono text-slate-400 not-italic">
                  ................................................................................................................................................................
                </p>
              )}
            </div>
          </div>
          <div className="border border-black p-3 rounded text-[12pt]">
            <p className="font-bold mb-2">2. Kết quả bỏ phiếu tín nhiệm tại Hội nghị Chi bộ:</p>
            <div className="grid grid-cols-4 gap-2 text-center text-[11pt] bg-slate-50 p-2 border border-slate-300">
              <div className="p-1 border border-slate-300 bg-white">
                <p className="text-xs text-slate-600">Xuất sắc</p>
                <p className="font-bold text-red-700 text-lg">{record.votesExcellent ?? 0} / {record.totalVoters ?? 10}</p>
              </div>
              <div className="p-1 border border-slate-300 bg-white">
                <p className="text-xs text-slate-600">Tốt</p>
                <p className="font-bold text-blue-700 text-lg">{record.votesGood ?? 0} / {record.totalVoters ?? 10}</p>
              </div>
              <div className="p-1 border border-slate-300 bg-white">
                <p className="text-xs text-slate-600">Hoàn thành</p>
                <p className="font-bold text-emerald-700 text-lg">{record.votesSatisfactory ?? 0} / {record.totalVoters ?? 10}</p>
              </div>
              <div className="p-1 border border-slate-300 bg-white">
                <p className="text-xs text-slate-600">Không HT</p>
                <p className="font-bold text-slate-500 text-lg">{record.votesUnsatisfactory ?? 0} / {record.totalVoters ?? 10}</p>
              </div>
            </div>
            <p className="mt-2 text-right">
              <strong>Chi bộ thống nhất đề xuất mức xếp loại: </strong>
              <span className="font-bold uppercase text-red-800">
                {record.partyCellProposedGrade ? formatGrade(record.partyCellProposedGrade) : "........................................"}
              </span>
            </p>
          </div>
        </div>

        {/* PHẦN IV: THẨM ĐỊNH & PHÊ DUYỆT */}
        <div className="mt-6 avoid-break">
          <h2 className="font-bold text-[13pt] uppercase border-b-2 border-black pb-1 mb-3">
            IV. KẾT QUẢ THẨM ĐỊNH VÀ PHÊ DUYỆT CỦA ĐẢNG ỦY
          </h2>
          <div className="grid grid-cols-2 gap-4 border border-black p-3 rounded text-[11.5pt] bg-slate-50">
            <div>
              <p className="font-bold text-blue-900 uppercase underline mb-1">Tổ Thẩm định Đảng ủy (Mẫu 03):</p>
              <p>• Điểm thẩm định: <strong>{record.appraisalScore != null ? `${record.appraisalScore.toFixed(2)} đ` : "............ đ"}</strong></p>
              <p>• Đề xuất xếp loại: <strong>{record.appraisalProposedGrade ? formatGrade(record.appraisalProposedGrade) : "........................................"}</strong></p>
              <p className="italic text-xs text-slate-700 mt-1">Ý kiến: {record.appraisalComment || "...................................................................................................."}</p>
            </div>
            <div>
              <p className="font-bold text-red-900 uppercase underline mb-1">Ban Thường vụ Đảng ủy (Mẫu 16):</p>
              <p>• Điểm chính thức: <strong className="text-lg text-red-900">{record.finalScore != null ? `${record.finalScore.toFixed(2)} đ` : "............ đ"}</strong></p>
              <p>• Mức xếp loại chính thức: <strong className="uppercase text-red-900">{record.finalGrade ? formatGrade(record.finalGrade) : "........................................"}</strong></p>
              <p className="text-xs text-slate-600 mt-1">Trạng thái: <strong>{record.statusDisplayName || record.status || "Đang xử lý"}</strong></p>
            </div>
          </div>
        </div>

        {/* CHỮ KÝ 4 BÊN */}
        <div className="mt-10 avoid-break grid grid-cols-4 gap-2 text-center text-[11pt]">
          <div>
            <p className="font-bold uppercase">NGƯỜI TỰ ĐÁNH GIÁ</p>
            <p className="italic text-xs mt-1">(Ký, ghi rõ họ tên)</p>
            <div className="h-24"></div>
            <p className="font-bold uppercase">{record.fullName}</p>
          </div>
          <div>
            <p className="font-bold uppercase">TM. CHI BỘ</p>
            <p className="italic text-xs mt-1">BÍ THƯ (Ký, họ tên)</p>
            <div className="h-24"></div>
            <p className="font-bold">....................................</p>
          </div>
          <div>
            <p className="font-bold uppercase">TỔ THẨM ĐỊNH</p>
            <p className="italic text-xs mt-1">TỔ TRƯỞNG (Ký, họ tên)</p>
            <div className="h-24"></div>
            <p className="font-bold">....................................</p>
          </div>
          <div>
            <p className="font-bold uppercase">TM. BAN THƯỜNG VỤ</p>
            <p className="italic text-xs mt-1">BÍ THƯ (Ký, đóng dấu)</p>
            <div className="h-24"></div>
            <p className="font-bold">....................................</p>
          </div>
        </div>
      </div>
    );
  }

  return null;
};
