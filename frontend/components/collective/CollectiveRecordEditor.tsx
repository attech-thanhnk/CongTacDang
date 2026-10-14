"use client";

import React, { FormEvent, useEffect, useState } from "react";
import type { CollectiveEvaluationItemDto } from "@/services/evaluationService";
import type { BranchItem, DepartmentItem } from "@/services/organizationService";
import { CollectiveFormCatalog, CollectiveRecord, SaveCollectiveRecord, collectiveService } from "@/services/collectiveService";
import { usePermissionScope, withinScope } from "@/services/authService";

export type CollectiveForm = "M06" | "M07" | "M08";

interface Props {
  periodId: string;
  catalog: CollectiveFormCatalog | null;
  branches: BranchItem[];
  departments: DepartmentItem[];
  /** Hồ sơ đang sửa (null = tạo mới). */
  editing: CollectiveRecord | null;
  onSaved: (record: CollectiveRecord) => void;
  onCancel: () => void;
  showError: (message: string) => void;
}

const emptyItem = (category: string, order: number): CollectiveEvaluationItemDto => ({
  itemOrder: order,
  category,
  taskName: "",
  planOrDirection: "",
  result: "",
  limitations: "",
  notes: "",
});

/** Nhập hồ sơ tập thể theo đúng mục của biểu mẫu gốc: Mẫu 07 (mục A.I–VI, B), Mẫu 08 (13 nhóm nội dung), Mẫu 06. */
export function CollectiveRecordEditor({ periodId, catalog, branches, departments, editing, onSaved, onCancel, showError }: Props) {
  const [form, setForm] = useState<CollectiveForm>("M07");
  const [unit, setUnit] = useState<"cell" | "department">("cell");
  const [partyCellId, setPartyCellId] = useState("");
  const [departmentId, setDepartmentId] = useState("");
  const [subjectName, setSubjectName] = useState("");
  const [sections, setSections] = useState<Record<string, string>>({});
  const [text, setText] = useState({ strengths: "", limitations: "", causes: "", previousRemediation: "", explanation: "", responsibilities: "", remediationPlan: "" });
  const [general, setGeneral] = useState(0);
  const [task, setTask] = useState(0);
  const [grade, setGrade] = useState("HoanThanhTot");
  const [items, setItems] = useState<CollectiveEvaluationItemDto[]>([]);
  const [saving, setSaving] = useState(false);

  // Chỉ cho chọn nơi được lập hồ sơ tập thể. Mẫu 07 là tự đánh giá của cấp ủy: chỉ tổ chức Đảng.
  const scope = usePermissionScope("collective.manage");
  const cellOptions = withinScope(branches, scope, "partyCell");
  const departmentOptions = withinScope(departments, scope, "department");
  const unitOptions: ("cell" | "department")[] = [
    ...(cellOptions.length > 0 ? (["cell"] as const) : []),
    ...(departmentOptions.length > 0 && form !== "M07" ? (["department"] as const) : []),
  ];
  const formOptions = (["M07", "M08", "M06"] as CollectiveForm[]).filter((value) =>
    cellOptions.length > 0 || (value !== "M07" && departmentOptions.length > 0));

  useEffect(() => {
    if (editing || !scope) return;
    if (!formOptions.includes(form) && formOptions.length > 0) {
      setForm(formOptions[0]);
      return;
    }
    if (!unitOptions.includes(unit) && unitOptions.length > 0) setUnit(unitOptions[0]);
    if (cellOptions.length === 1 && !partyCellId) setPartyCellId(cellOptions[0].id);
    if (departmentOptions.length === 1 && !departmentId) setDepartmentId(departmentOptions[0].id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editing, scope, form, unit, cellOptions.length, departmentOptions.length]);

  useEffect(() => {
    if (!editing) {
      setSubjectName("");
      setSections({});
      setText({ strengths: "", limitations: "", causes: "", previousRemediation: "", explanation: "", responsibilities: "", remediationPlan: "" });
      setGeneral(0);
      setTask(0);
      setItems([]);
      return;
    }
    setForm(editing.form as CollectiveForm);
    setUnit(editing.departmentId && !editing.partyCellId ? "department" : "cell");
    setPartyCellId(editing.partyCellId || "");
    setDepartmentId(editing.departmentId || "");
    setSubjectName(editing.subjectName);
    setSections(editing.sections || {});
    setText({
      strengths: editing.strengths || "",
      limitations: editing.limitations || "",
      causes: editing.causes || "",
      previousRemediation: editing.previousRemediation || "",
      explanation: editing.explanation || "",
      responsibilities: editing.responsibilities || "",
      remediationPlan: editing.remediationPlan || "",
    });
    setGeneral(editing.generalCriteriaScore);
    setTask(editing.taskCriteriaScore);
    setGrade(editing.selfProposedGrade || "HoanThanhTot");
    setItems(editing.items.map((item) => ({ ...item })));
  }, [editing]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!subjectName.trim()) {
      showError("Hãy nhập tên tập thể (Đảng ủy, Chi ủy, Chi bộ) hoặc cơ quan, đơn vị.");
      return;
    }
    if (!editing && unit === "cell" && !partyCellId) {
      showError("Hãy chọn Chi bộ / tổ chức Đảng của hồ sơ.");
      return;
    }
    if (!editing && unit === "department" && !departmentId) {
      showError("Hãy chọn Phòng / đơn vị của hồ sơ.");
      return;
    }
    setSaving(true);
    const payload: SaveCollectiveRecord = {
      version: editing?.version,
      periodId,
      form,
      partyCellId: unit === "cell" ? partyCellId || undefined : undefined,
      departmentId: unit === "department" ? departmentId || undefined : undefined,
      subjectName,
      ...text,
      generalCriteriaScore: Number(general),
      taskCriteriaScore: Number(task),
      selfProposedGrade: grade,
      sections: form === "M07" ? sections : {},
      items: form === "M08" ? items.map((item, index) => ({ ...item, itemOrder: index + 1 })) : [],
    };
    try {
      const saved = editing ? await collectiveService.updateRecord(editing.id, payload) : await collectiveService.createRecord(payload);
      onSaved(saved);
    } catch (error: any) {
      showError(error?.message || "Không lưu được hồ sơ tập thể.");
    } finally {
      setSaving(false);
    }
  };

  const textArea = (label: string, value: string, onChange: (value: string) => void, rows = 3, hint?: string) => (
    <div className="col-12">
      <label className="form-label">{label}</label>
      {hint && <div className="form-text mt-0 mb-1">{hint}</div>}
      <textarea className="form-control form-control-sm" rows={rows} value={value} onChange={(e) => onChange(e.target.value)} />
    </div>
  );

  const setItem = (index: number, patch: Partial<CollectiveEvaluationItemDto>) =>
    setItems(items.map((item, i) => (i === index ? { ...item, ...patch } : item)));

  if (!editing && scope && formOptions.length === 0)
    return <div className="text-secondary small">Bạn chưa được giao lập hồ sơ tập thể ở đơn vị nào.</div>;

  const FORM_LABELS: Record<CollectiveForm, string> = {
    M07: "Mẫu 07 — Tự đánh giá của tập thể Đảng ủy (Chi ủy, Chi bộ)",
    M08: "Mẫu 08 — Tổng hợp kết quả nhiệm vụ của cơ quan, đơn vị",
    M06: "Mẫu 06 — Kết quả tập thể/lĩnh vực",
  };

  return (
    <form onSubmit={submit} className="row g-2">
      <div className="col-md-4">
        <label className="form-label">Biểu mẫu</label>
        <select className="form-select form-select-sm" value={form} disabled={!!editing} onChange={(e) => setForm(e.target.value as CollectiveForm)}>
          {(editing ? (["M07", "M08", "M06"] as CollectiveForm[]) : formOptions).map((value) => <option key={value} value={value}>{FORM_LABELS[value]}</option>)}
        </select>
      </div>
      <div className="col-md-3">
        <label className="form-label">Thuộc</label>
        <select className="form-select form-select-sm" value={unit} disabled={!!editing} onChange={(e) => setUnit(e.target.value as "cell" | "department")}>
          {(editing ? (["cell", "department"] as const) : unitOptions).map((value) => (
            <option key={value} value={value}>{value === "cell" ? "Tổ chức Đảng" : "Phòng / đơn vị"}</option>
          ))}
        </select>
      </div>
      <div className="col-md-5">
        <label className="form-label">{unit === "cell" ? "Chi bộ / tổ chức Đảng" : "Phòng / đơn vị"}</label>
        {unit === "cell" ? (
          <select className="form-select form-select-sm" value={partyCellId} disabled={!!editing} onChange={(e) => setPartyCellId(e.target.value)}>
            <option value="">Chọn tổ chức Đảng</option>
            {(editing ? branches : cellOptions).map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
          </select>
        ) : (
          <select className="form-select form-select-sm" value={departmentId} disabled={!!editing} onChange={(e) => setDepartmentId(e.target.value)}>
            <option value="">Chọn Phòng / đơn vị</option>
            {(editing ? departments : departmentOptions).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        )}
      </div>
      <div className="col-12">
        <label className="form-label">{form === "M08" ? "Tên cơ quan, đơn vị" : "Tên tập thể (Đảng ủy, Chi ủy, Chi bộ)"}</label>
        <input className="form-control form-control-sm" value={subjectName} onChange={(e) => setSubjectName(e.target.value)} />
      </div>

      {form === "M07" && (
        <>
          <div className="col-12 mt-3 fw-semibold small text-uppercase text-secondary">A. Nội dung tự đánh giá — I. Ưu điểm, kết quả đạt được</div>
          {(catalog?.form07Strengths ?? []).map((section, index) => (
            <React.Fragment key={section.code}>
              {textArea(`${index + 1}. ${section.title.length > 110 ? section.title.slice(0, 110) + "…" : section.title}`,
                sections[section.code] || "",
                (value) => setSections({ ...sections, [section.code]: value }))}
            </React.Fragment>
          ))}
          <div className="col-12 mt-2 fw-semibold small text-uppercase text-secondary">II. Hạn chế, khuyết điểm và nguyên nhân</div>
          {textArea("- Hạn chế, khuyết điểm.", text.limitations, (v) => setText({ ...text, limitations: v }))}
          {textArea("- Nguyên nhân của hạn chế, khuyết điểm.", text.causes, (v) => setText({ ...text, causes: v }))}
          {textArea("III. Kết quả khắc phục những hạn chế, khuyết điểm đã được cấp có thẩm quyền kết luận hoặc được chỉ ra ở các kỳ kiểm điểm trước", text.previousRemediation, (v) => setText({ ...text, previousRemediation: v }))}
          {textArea("IV. Giải trình những vấn đề được gợi ý kiểm điểm (nếu có)", text.explanation, (v) => setText({ ...text, explanation: v }))}
          {textArea("V. Trách nhiệm của tập thể, cá nhân", text.responsibilities, (v) => setText({ ...text, responsibilities: v }))}
          {textArea("VI. Phương hướng, biện pháp khắc phục hạn chế, khuyết điểm", text.remediationPlan, (v) => setText({ ...text, remediationPlan: v }))}
        </>
      )}

      {form === "M06" && (
        <>
          {textArea("Ưu điểm, kết quả đạt được", text.strengths, (v) => setText({ ...text, strengths: v }))}
          {textArea("Hạn chế, khuyết điểm", text.limitations, (v) => setText({ ...text, limitations: v }))}
          {textArea("Nguyên nhân", text.causes, (v) => setText({ ...text, causes: v }))}
          {textArea("Phương hướng khắc phục", text.remediationPlan, (v) => setText({ ...text, remediationPlan: v }))}
        </>
      )}

      {form !== "M08" && (
        <>
          <div className="col-12 mt-2 fw-semibold small text-uppercase text-secondary">B. Kết quả chấm điểm các tiêu chí đánh giá</div>
          <div className="col-md-4">
            <label className="form-label">1. Nhóm tiêu chí chung (/30)</label>
            <input type="number" min={0} max={30} step={0.5} className="form-control form-control-sm" value={general} onChange={(e) => setGeneral(Number(e.target.value))} />
          </div>
          <div className="col-md-4">
            <label className="form-label">2. Nhóm kết quả thực hiện nhiệm vụ (/70)</label>
            <input type="number" min={0} max={70} step={0.5} className="form-control form-control-sm" value={task} onChange={(e) => setTask(Number(e.target.value))} />
          </div>
          <div className="col-md-4">
            <label className="form-label">Tổng điểm (/100)</label>
            <input className="form-control form-control-sm" readOnly value={(Number(general) + Number(task)).toFixed(1)} />
          </div>
          {form === "M06" && (
            <div className="col-md-6">
              <label className="form-label">Mức tự xếp loại của tập thể</label>
              <select className="form-select form-select-sm" value={grade} onChange={(e) => setGrade(e.target.value)}>
                <option value="HoanThanhXuatSac">Hoàn thành xuất sắc nhiệm vụ</option>
                <option value="HoanThanhTot">Hoàn thành tốt nhiệm vụ</option>
                <option value="HoanThanh">Hoàn thành nhiệm vụ</option>
                <option value="KhongHoanThanh">Không hoàn thành nhiệm vụ</option>
              </select>
            </div>
          )}
        </>
      )}

      {form === "M08" && (
        <div className="col-12 mt-2">
          <div className="fw-semibold small text-uppercase text-secondary mb-1">Nhiệm vụ theo nhóm nội dung của biểu mẫu</div>
          {items.map((item, index) => (
            <div key={index} className="border rounded p-2 mb-2">
              <div className="row g-1">
                <div className="col-md-5">
                  <select className="form-select form-select-sm" aria-label="Nhóm nội dung" value={item.category} onChange={(e) => setItem(index, { category: e.target.value })}>
                    {(catalog?.form08Categories ?? []).map((c) => (
                      <option key={c.code} value={c.code}>{c.code}. {c.title.length > 70 ? c.title.slice(0, 70) + "…" : c.title}</option>
                    ))}
                  </select>
                </div>
                <div className="col-md-6">
                  <input className="form-control form-control-sm" placeholder="Nhiệm vụ" value={item.taskName} onChange={(e) => setItem(index, { taskName: e.target.value })} />
                </div>
                <div className="col-md-1 text-end">
                  <button type="button" className="btn btn-sm btn-outline-danger" aria-label="Bỏ dòng" onClick={() => setItems(items.filter((_, i) => i !== index))}>
                    <i className="bi bi-x" />
                  </button>
                </div>
                <div className="col-md-6">
                  <textarea className="form-control form-control-sm" rows={2} placeholder="Kế hoạch hoặc sự chỉ đạo, yêu cầu của lãnh đạo Tổng công ty" value={item.planOrDirection} onChange={(e) => setItem(index, { planOrDirection: e.target.value })} />
                </div>
                <div className="col-md-6">
                  <textarea className="form-control form-control-sm" rows={2} placeholder="Kết quả thực hiện trong kỳ đánh giá" value={item.result} onChange={(e) => setItem(index, { result: e.target.value })} />
                </div>
                <div className="col-md-8">
                  <input className="form-control form-control-sm" placeholder="Tồn tại, hạn chế hoặc thành tích đã được ghi nhận, biểu dương" value={item.limitations} onChange={(e) => setItem(index, { limitations: e.target.value })} />
                </div>
                <div className="col-md-4">
                  <input className="form-control form-control-sm" placeholder="Ghi chú" value={item.notes} onChange={(e) => setItem(index, { notes: e.target.value })} />
                </div>
              </div>
            </div>
          ))}
          <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => setItems([...items, emptyItem(catalog?.form08Categories[0]?.code ?? "1", items.length + 1)])}>
            <i className="bi bi-plus me-1" />Thêm nhiệm vụ
          </button>
        </div>
      )}

      <div className="col-12 d-flex gap-2 mt-3">
        <button className="btn btn-primary btn-sm" disabled={saving}>{saving ? "Đang lưu..." : editing ? "Lưu thay đổi" : "Lưu hồ sơ tập thể"}</button>
        {editing && <button type="button" className="btn btn-outline-secondary btn-sm" onClick={onCancel}>Hủy sửa</button>}
      </div>
    </form>
  );
}

export default CollectiveRecordEditor;
