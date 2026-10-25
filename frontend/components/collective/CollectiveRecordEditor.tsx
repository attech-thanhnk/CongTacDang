"use client";

import React, { FormEvent, useEffect, useMemo, useState } from "react";
import type { CollectiveEvaluationItemDto } from "@/services/evaluationService";
import type { BranchItem, DepartmentItem } from "@/services/organizationService";
import {
  CatalogState,
  CollectiveCriteriaItem,
  CollectiveFormCatalog,
  CollectiveRecord,
  SaveCollectiveRecord,
  collectiveService,
} from "@/services/collectiveService";
import { usePermissionScope, withinScope } from "@/services/authService";
import { fmt as formatScore } from "@/services/criteriaService";
import { errorMessage } from "@/components/admin/adminUtils";
import { Button } from "@/components/common/Button";
import { useToast } from "@/contexts/ToastContext";
import styles from "./Collective.module.css";

export type CollectiveForm = "M06" | "M07" | "M08";

interface Props {
  periodId: string;
  catalog: CollectiveFormCatalog | null;
  /** Trạng thái tải khung tiêu chí của kỳ: chỉ cho lưu khi đã tải xong. */
  catalogState: CatalogState;
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

const EMPTY_TEXT = { strengths: "", limitations: "", causes: "", previousRemediation: "", explanation: "", responsibilities: "", remediationPlan: "" };

/** Nội dung tiêu chí dài hơn ngưỡng này thì rút gọn, bấm để xem đầy đủ. */
const TEXT_PREVIEW_LENGTH = 140;

const shortText = (text: string, length = 60) => (text.length > length ? text.slice(0, length) + "…" : text);

const FORM_LABELS: Record<CollectiveForm, string> = {
  M07: "Mẫu 07 — Tự đánh giá của tập thể Đảng ủy (Chi ủy, Chi bộ)",
  M08: "Mẫu 08 — Tổng hợp kết quả nhiệm vụ của cơ quan, đơn vị",
  M06: "Mẫu 06 — Kết quả tập thể/lĩnh vực",
};

/** Nhập hồ sơ tập thể theo đúng mục của biểu mẫu gốc: Mẫu 07 (mục A.I–VI, B), Mẫu 08 (13 nhóm nội dung), Mẫu 06. */
export function CollectiveRecordEditor({ periodId, catalog, catalogState, branches, departments, editing, onSaved, onCancel, showError }: Props) {
  const { confirm } = useToast();
  const [form, setForm] = useState<CollectiveForm>("M07");
  const [unit, setUnit] = useState<"cell" | "department">("cell");
  const [partyCellId, setPartyCellId] = useState("");
  const [departmentId, setDepartmentId] = useState("");
  const [subjectName, setSubjectName] = useState("");
  const [sections, setSections] = useState<Record<string, string>>({});
  const [text, setText] = useState(EMPTY_TEXT);
  const [general, setGeneral] = useState(0);
  const [task, setTask] = useState(0);
  /** Điểm từng tiêu chí của khung tập thể, giữ dạng chuỗi để ô chưa chấm để trống. */
  const [criteriaInput, setCriteriaInput] = useState<Record<string, string>>({});
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const [grade, setGrade] = useState("HoanThanhTot");
  const [items, setItems] = useState<CollectiveEvaluationItemDto[]>([]);
  const [saving, setSaving] = useState(false);
  /** Đã bấm Lưu: từ đó báo lỗi ngay dưới ô bắt buộc. */
  const [submitted, setSubmitted] = useState(false);
  /** Nội dung lúc mở, để chỉ bật Lưu khi có thay đổi. */
  const [baseline, setBaseline] = useState<string | null>(null);
  const [baselineTick, setBaselineTick] = useState(0);

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
      setText(EMPTY_TEXT);
      setGeneral(0);
      setTask(0);
      setCriteriaInput({});
      setExpanded({});
      setItems([]);
      setBaselineTick((tick) => tick + 1);
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
    setCriteriaInput(
      Object.fromEntries(Object.entries(editing.criteriaScores ?? {}).map(([code, score]) => [code, String(score)])),
    );
    setExpanded({});
    setGrade(editing.selfProposedGrade || "HoanThanhTot");
    setItems(editing.items.map((item) => ({ ...item })));
    setBaselineTick((tick) => tick + 1);
  }, [editing]);

  const snapshot = JSON.stringify({ subjectName, sections, text, general, task, criteriaInput, grade, items });
  // Chụp ở lần vẽ sau khi dữ liệu hồ sơ đã vào các ô.
  useEffect(() => {
    if (baselineTick > 0) setBaseline(snapshot);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [baselineTick]);
  const dirty = baseline !== null && snapshot !== baseline;

  const groups = useMemo(() => catalog?.collectiveGroups ?? [], [catalog]);
  const generalMax = catalog?.generalMaxScore ?? 30;
  const taskMax = catalog?.taskMaxScore ?? 70;
  /** Mẫu 06/07 chấm theo từng tiêu chí khi bộ tiêu chí của kỳ có khung tập thể. */
  const useFramework = form !== "M08" && groups.some((group) => group.items.length > 0);

  /** Điểm đã nhập hợp lệ của một tiêu chí (null = chưa chấm hoặc không phải số). */
  const itemScore = (item: CollectiveCriteriaItem): number | null => {
    const raw = (criteriaInput[item.code] ?? "").trim();
    if (raw === "") return null;
    const value = Number(raw);
    return Number.isFinite(value) ? value : null;
  };

  const groupSum = (items: CollectiveCriteriaItem[]) => items.reduce((sum, item) => sum + (itemScore(item) ?? 0), 0);
  const groupMax = (items: CollectiveCriteriaItem[]) => items.reduce((sum, item) => sum + item.maxScore, 0);

  const frameworkGeneral = groups.filter((g) => g.kind !== "Task").reduce((sum, g) => sum + groupSum(g.items), 0);
  const frameworkTask = groups.filter((g) => g.kind === "Task").reduce((sum, g) => sum + groupSum(g.items), 0);
  const generalScore = useFramework ? frameworkGeneral : Number(general) || 0;
  const taskScore = useFramework ? frameworkTask : Number(task) || 0;
  const generalInvalid = !useFramework && form !== "M08" && (generalScore < 0 || generalScore > generalMax);
  const taskInvalid = !useFramework && form !== "M08" && (taskScore < 0 || taskScore > taskMax);

  const nameMissing = !subjectName.trim();
  const unitMissing = !editing && ((unit === "cell" && !partyCellId) || (unit === "department" && !departmentId));

  /** Kiểm tra đủ và đúng khoảng điểm các tiêu chí; trả về thông báo lỗi hoặc null. */
  const validateFramework = (): string | null => {
    const missing: string[] = [];
    const outOfRange: string[] = [];
    for (const group of groups) {
      for (const item of group.items) {
        const raw = (criteriaInput[item.code] ?? "").trim();
        const value = itemScore(item);
        if (raw === "") missing.push(`${item.code}. ${shortText(item.text)}`);
        else if (value === null || value < 0 || value > item.maxScore) outOfRange.push(`${item.code} (0–${formatScore(item.maxScore)})`);
      }
    }
    const list = (values: string[]) => values.slice(0, 5).join("; ") + (values.length > 5 ? `; và ${values.length - 5} tiêu chí khác` : "");
    if (missing.length > 0) return `Chưa chấm điểm ${missing.length} tiêu chí: ${list(missing)}.`;
    if (outOfRange.length > 0) return `Điểm vượt khoảng cho phép ở tiêu chí: ${list(outOfRange)}.`;
    return null;
  };

  const cancel = () => {
    if (!dirty) {
      onCancel();
      return;
    }
    confirm({
      title: "Bỏ nội dung chưa lưu",
      message: "Nội dung vừa nhập chưa được lưu. Đóng và bỏ các thay đổi này?",
      confirmText: "Bỏ thay đổi",
      isDanger: true,
      onConfirm: onCancel,
    });
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitted(true);
    if (catalogState !== "ready") return;
    // Lỗi ô bắt buộc và điểm nhóm hiện ngay dưới ô.
    if (nameMissing || unitMissing || generalInvalid || taskInvalid) return;
    let criteriaScores: Record<string, number> | undefined;
    if (useFramework) {
      const problem = validateFramework();
      if (problem) {
        showError(problem);
        return;
      }
      criteriaScores = {};
      for (const group of groups) for (const item of group.items) criteriaScores[item.code] = itemScore(item) ?? 0;
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
      generalCriteriaScore: form === "M08" ? Number(general) : generalScore,
      taskCriteriaScore: form === "M08" ? Number(task) : taskScore,
      ...(criteriaScores ? { criteriaScores } : {}),
      selfProposedGrade: grade,
      sections: form === "M07" || form === "M06" ? sections : {},
      items: form === "M08" ? items.map((item, index) => ({ ...item, itemOrder: index + 1 })) : [],
    };
    try {
      const saved = editing ? await collectiveService.updateRecord(editing.id, payload) : await collectiveService.createRecord(payload);
      onSaved(saved);
    } catch (error: unknown) {
      showError(errorMessage(error, "Lưu hồ sơ tập thể không thành công."));
    } finally {
      setSaving(false);
    }
  };

  const textArea = (id: string, label: string, value: string, onChange: (value: string) => void, rows = 3) => (
    <div className="col-12" key={id}>
      <label className="form-label" htmlFor={`cr-${id}`}>{label}</label>
      <textarea id={`cr-${id}`} className="form-control form-control-sm" rows={rows} value={value} onChange={(e) => onChange(e.target.value)} />
    </div>
  );

  const setItem = (index: number, patch: Partial<CollectiveEvaluationItemDto>) =>
    setItems(items.map((item, i) => (i === index ? { ...item, ...patch } : item)));

  if (!editing && scope && formOptions.length === 0)
    return <div className="text-secondary small">Bạn chưa được giao lập hồ sơ tập thể ở đơn vị nào.</div>;

  const showUnitError = submitted && unitMissing;
  const showNameError = submitted && nameMissing;

  return (
    <form onSubmit={submit} noValidate>
      <section className={styles.section}>
        <h3 className={styles.legend}>Thông tin chung</h3>
        <div className="row g-2">
          <div className="col-md-4">
            <label className="form-label" htmlFor="cr-form">Biểu mẫu</label>
            <select id="cr-form" className="form-select form-select-sm" value={form} disabled={!!editing} onChange={(e) => setForm(e.target.value as CollectiveForm)}>
              {(editing ? (["M07", "M08", "M06"] as CollectiveForm[]) : formOptions).map((value) => <option key={value} value={value}>{FORM_LABELS[value]}</option>)}
            </select>
          </div>
          <div className="col-md-3">
            <label className="form-label" htmlFor="cr-unit">Thuộc</label>
            <select id="cr-unit" className="form-select form-select-sm" value={unit} disabled={!!editing} onChange={(e) => setUnit(e.target.value as "cell" | "department")}>
              {(editing ? (["cell", "department"] as const) : unitOptions).map((value) => (
                <option key={value} value={value}>{value === "cell" ? "Tổ chức Đảng" : "Phòng / đơn vị"}</option>
              ))}
            </select>
          </div>
          <div className="col-md-5">
            <label className="form-label" htmlFor="cr-org">{unit === "cell" ? "Chi bộ / tổ chức Đảng" : "Phòng / đơn vị"}</label>
            {unit === "cell" ? (
              <select id="cr-org" className={`form-select form-select-sm${showUnitError ? " is-invalid" : ""}`} value={partyCellId} disabled={!!editing} onChange={(e) => setPartyCellId(e.target.value)}>
                <option value="">Chọn tổ chức Đảng</option>
                {(editing ? branches : cellOptions).map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
              </select>
            ) : (
              <select id="cr-org" className={`form-select form-select-sm${showUnitError ? " is-invalid" : ""}`} value={departmentId} disabled={!!editing} onChange={(e) => setDepartmentId(e.target.value)}>
                <option value="">Chọn Phòng / đơn vị</option>
                {(editing ? departments : departmentOptions).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
            )}
            {showUnitError && <div className="invalid-feedback">{unit === "cell" ? "Hãy chọn tổ chức Đảng." : "Hãy chọn Phòng / đơn vị."}</div>}
          </div>
          <div className="col-12">
            <label className="form-label" htmlFor="cr-name">{form === "M08" ? "Tên cơ quan, đơn vị" : "Tên tập thể (Đảng ủy, Chi ủy, Chi bộ)"}</label>
            <input id="cr-name" className={`form-control form-control-sm${showNameError ? " is-invalid" : ""}`} value={subjectName} onChange={(e) => setSubjectName(e.target.value)} />
            {showNameError && <div className="invalid-feedback">Hãy nhập tên {form === "M08" ? "cơ quan, đơn vị" : "tập thể"}.</div>}
          </div>
        </div>
      </section>

      {form === "M07" && (
        <section className={styles.section}>
          <h3 className={styles.legend}>A. Nội dung tự đánh giá</h3>
          <div className="row g-2">
            <div className={`col-12 ${styles.subhead}`}>I. Ưu điểm, kết quả đạt được</div>
            {(catalog?.form07Strengths ?? []).map((section, index) =>
              textArea(`s-${section.code}`, `${index + 1}. ${section.title.length > 110 ? section.title.slice(0, 110) + "…" : section.title}`,
                sections[section.code] || "",
                (value) => setSections({ ...sections, [section.code]: value })))}
            <div className={`col-12 ${styles.subhead}`}>II. Hạn chế, khuyết điểm và nguyên nhân</div>
            {textArea("limitations", "Hạn chế, khuyết điểm", text.limitations, (v) => setText({ ...text, limitations: v }))}
            {textArea("causes", "Nguyên nhân của hạn chế, khuyết điểm", text.causes, (v) => setText({ ...text, causes: v }))}
            {textArea("previous", "III. Kết quả khắc phục hạn chế, khuyết điểm đã được kết luận hoặc chỉ ra ở các kỳ kiểm điểm trước", text.previousRemediation, (v) => setText({ ...text, previousRemediation: v }))}
            {textArea("explanation", "IV. Giải trình những vấn đề được gợi ý kiểm điểm (nếu có)", text.explanation, (v) => setText({ ...text, explanation: v }))}
            {textArea("responsibilities", "V. Trách nhiệm của tập thể, cá nhân", text.responsibilities, (v) => setText({ ...text, responsibilities: v }))}
            {textArea("plan", "VI. Phương hướng, biện pháp khắc phục hạn chế, khuyết điểm", text.remediationPlan, (v) => setText({ ...text, remediationPlan: v }))}
          </div>
        </section>
      )}

      {form === "M06" && (
        <section className={styles.section}>
          <h3 className={styles.legend}>Nội dung đánh giá</h3>
          <div className="row g-2">
            {(catalog?.form06Fields ?? []).map((field) => {
              const value = sections[field.code] || "";
              const onChange = (next: string) => setSections({ ...sections, [field.code]: next });
              if (field.code.startsWith("S.")) return textArea(`s-${field.code}`, field.title, value, onChange, 2);
              const numeric = field.code.startsWith("T.");
              return (
                <div className="col-12 col-md-6" key={field.code}>
                  <label className="form-label" htmlFor={`cr-${field.code}`}>{field.title}</label>
                  <input
                    id={`cr-${field.code}`}
                    className="form-control form-control-sm"
                    type={numeric ? "number" : "text"}
                    min={numeric ? 0 : undefined}
                    step={numeric ? 1 : undefined}
                    value={value}
                    onChange={(e) => onChange(e.target.value)}
                  />
                </div>
              );
            })}
            {textArea("strengths", "Ưu điểm, kết quả đạt được", text.strengths, (v) => setText({ ...text, strengths: v }))}
            {textArea("limitations", "Hạn chế, khuyết điểm", text.limitations, (v) => setText({ ...text, limitations: v }))}
            {textArea("causes", "Nguyên nhân", text.causes, (v) => setText({ ...text, causes: v }))}
            {textArea("plan", "Phương hướng khắc phục", text.remediationPlan, (v) => setText({ ...text, remediationPlan: v }))}
          </div>
        </section>
      )}

      {form !== "M08" && (
        <section className={styles.section}>
          <h3 className={styles.legend}>{form === "M07" ? "B. Kết quả chấm điểm các tiêu chí" : "Chấm điểm và tự xếp loại"}</h3>
          <div className="row g-2">
            {useFramework ? (
              <>
                <div className="col-12">
                  <div className="table-responsive">
                    <table className="table table-sm table-bordered align-middle mb-0 small">
                      <thead className="table-light">
                        <tr>
                          <th style={{ width: 64 }}>Mã</th>
                          <th>Tiêu chí</th>
                          <th className="text-center" style={{ width: 72 }}>Tối đa</th>
                          <th className="text-center" style={{ width: 104 }}>Điểm</th>
                        </tr>
                      </thead>
                      <tbody>
                        {groups.map((group) => (
                          <React.Fragment key={group.code}>
                            <tr className="table-light">
                              <td className="fw-semibold">{group.code}</td>
                              <td colSpan={3} className="fw-semibold">{group.name}</td>
                            </tr>
                            {group.items.map((item) => {
                              const long = item.text.length > TEXT_PREVIEW_LENGTH;
                              const open = !!expanded[item.code];
                              const raw = criteriaInput[item.code] ?? "";
                              const value = itemScore(item);
                              const invalid = raw.trim() !== "" && (value === null || value < 0 || value > item.maxScore);
                              return (
                                <tr key={item.code}>
                                  <td className="text-nowrap">{item.code}</td>
                                  <td>
                                    <span style={{ whiteSpace: "pre-line" }}>{long && !open ? item.text.slice(0, TEXT_PREVIEW_LENGTH) + "…" : item.text}</span>
                                    {long && (
                                      <button
                                        type="button"
                                        className="btn btn-link btn-sm p-0 ms-1 align-baseline"
                                        style={{ fontSize: "inherit" }}
                                        onClick={() => setExpanded({ ...expanded, [item.code]: !open })}
                                      >
                                        {open ? "Thu gọn" : "Xem đầy đủ"}
                                      </button>
                                    )}
                                  </td>
                                  <td className="text-center">{formatScore(item.maxScore)}</td>
                                  <td>
                                    <input
                                      type="number"
                                      min={0}
                                      max={item.maxScore}
                                      step={0.25}
                                      inputMode="decimal"
                                      aria-label={`Điểm tiêu chí ${item.code}`}
                                      className={`form-control form-control-sm text-end${invalid ? " is-invalid" : ""}`}
                                      value={raw}
                                      onChange={(e) => setCriteriaInput({ ...criteriaInput, [item.code]: e.target.value })}
                                    />
                                  </td>
                                </tr>
                              );
                            })}
                            <tr>
                              <td colSpan={2} className="text-end fw-semibold">Cộng {group.code}</td>
                              <td className="text-center fw-semibold">{formatScore(groupMax(group.items))}</td>
                              <td className="text-end fw-semibold pe-3">{formatScore(groupSum(group.items))}</td>
                            </tr>
                          </React.Fragment>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
                <div className="col-12">
                  <div className={styles.scoreSummary}>
                    <span>Nhóm tiêu chí chung: <strong className="num">{formatScore(generalScore)}</strong>/{formatScore(generalMax)}</span>
                    <span>Nhóm kết quả thực hiện nhiệm vụ: <strong className="num">{formatScore(taskScore)}</strong>/{formatScore(taskMax)}</span>
                    <span>Tổng điểm: <strong className="num">{formatScore(generalScore + taskScore)}</strong>/{formatScore(generalMax + taskMax)}</span>
                  </div>
                </div>
              </>
            ) : (
              <>
                <div className="col-6 col-md-4">
                  <label className="form-label" htmlFor="cr-general">Nhóm tiêu chí chung (/{formatScore(generalMax)})</label>
                  <input id="cr-general" type="number" min={0} max={generalMax} step={0.5} inputMode="decimal"
                    className={`form-control form-control-sm${generalInvalid ? " is-invalid" : ""}`} value={general} onChange={(e) => setGeneral(Number(e.target.value))} />
                  {generalInvalid && <div className="invalid-feedback">Trong khoảng 0–{formatScore(generalMax)}.</div>}
                </div>
                <div className="col-6 col-md-4">
                  <label className="form-label" htmlFor="cr-task">Nhóm kết quả nhiệm vụ (/{formatScore(taskMax)})</label>
                  <input id="cr-task" type="number" min={0} max={taskMax} step={0.5} inputMode="decimal"
                    className={`form-control form-control-sm${taskInvalid ? " is-invalid" : ""}`} value={task} onChange={(e) => setTask(Number(e.target.value))} />
                  {taskInvalid && <div className="invalid-feedback">Trong khoảng 0–{formatScore(taskMax)}.</div>}
                </div>
                <div className="col-12 col-md-4">
                  <label className="form-label" htmlFor="cr-total">Tổng điểm (/{formatScore(generalMax + taskMax)})</label>
                  <input id="cr-total" className="form-control form-control-sm" readOnly value={formatScore(generalScore + taskScore)} />
                </div>
              </>
            )}
            {form === "M06" && (
              <div className="col-md-6">
                <label className="form-label" htmlFor="cr-grade">Mức tự xếp loại của tập thể</label>
                <select id="cr-grade" className="form-select form-select-sm" value={grade} onChange={(e) => setGrade(e.target.value)}>
                  <option value="HoanThanhXuatSac">Hoàn thành xuất sắc nhiệm vụ</option>
                  <option value="HoanThanhTot">Hoàn thành tốt nhiệm vụ</option>
                  <option value="HoanThanh">Hoàn thành nhiệm vụ</option>
                  <option value="KhongHoanThanh">Không hoàn thành nhiệm vụ</option>
                </select>
              </div>
            )}
          </div>
        </section>
      )}

      {form === "M08" && (
        <section className={styles.section}>
          <h3 className={styles.legend}>Nhiệm vụ theo nhóm nội dung</h3>
          {items.map((item, index) => (
            <div key={index} className={styles.itemCard}>
              <div className="row g-1">
                <div className="col-10 col-md-5">
                  <select className="form-select form-select-sm" aria-label="Nhóm nội dung" value={item.category} onChange={(e) => setItem(index, { category: e.target.value })}>
                    {(catalog?.form08Categories ?? []).map((c) => (
                      <option key={c.code} value={c.code}>{c.code}. {c.title.length > 70 ? c.title.slice(0, 70) + "…" : c.title}</option>
                    ))}
                  </select>
                </div>
                <div className="col-2 col-md-1 order-md-last text-end">
                  <button type="button" className={`btn btn-sm btn-outline-danger ${styles.iconBtn}`} aria-label="Bỏ nhiệm vụ" title="Bỏ nhiệm vụ"
                    onClick={() => setItems(items.filter((_, i) => i !== index))}>
                    <i className="bi bi-x-lg" aria-hidden="true" />
                  </button>
                </div>
                <div className="col-12 col-md-6">
                  <input className="form-control form-control-sm" placeholder="Nhiệm vụ" aria-label="Nhiệm vụ" value={item.taskName} onChange={(e) => setItem(index, { taskName: e.target.value })} />
                </div>
                <div className="col-md-6">
                  <textarea className="form-control form-control-sm" rows={2} placeholder="Kế hoạch hoặc sự chỉ đạo, yêu cầu của lãnh đạo" aria-label="Kế hoạch hoặc sự chỉ đạo" value={item.planOrDirection} onChange={(e) => setItem(index, { planOrDirection: e.target.value })} />
                </div>
                <div className="col-md-6">
                  <textarea className="form-control form-control-sm" rows={2} placeholder="Kết quả thực hiện trong kỳ đánh giá" aria-label="Kết quả thực hiện" value={item.result} onChange={(e) => setItem(index, { result: e.target.value })} />
                </div>
                <div className="col-md-8">
                  <input className="form-control form-control-sm" placeholder="Tồn tại, hạn chế hoặc thành tích được ghi nhận" aria-label="Tồn tại, hạn chế hoặc thành tích" value={item.limitations} onChange={(e) => setItem(index, { limitations: e.target.value })} />
                </div>
                <div className="col-md-4">
                  <input className="form-control form-control-sm" placeholder="Ghi chú" aria-label="Ghi chú" value={item.notes} onChange={(e) => setItem(index, { notes: e.target.value })} />
                </div>
              </div>
            </div>
          ))}
          <Button type="button" size="sm" variant="outline-secondary" icon="bi-plus-lg"
            onClick={() => setItems([...items, emptyItem(catalog?.form08Categories[0]?.code ?? "1", items.length + 1)])}>
            Thêm nhiệm vụ
          </Button>
        </section>
      )}

      {catalogState === "error" && (
        <div className="alert alert-danger small mb-0 mt-3">Chưa tải được khung tiêu chí của kỳ. Hãy tải lại trang trước khi lưu.</div>
      )}
      <div className={styles.footer}>
        {catalogState === "loading" && (
          <span className={styles.footerHint}><span className="spinner-border spinner-border-sm me-2" />Đang tải khung tiêu chí...</span>
        )}
        <Button type="button" variant="outline-secondary" onClick={cancel} disabled={saving}>Hủy</Button>
        <Button
          type="submit"
          variant="primary"
          icon="bi-check-lg"
          loading={saving}
          loadingText="Đang lưu..."
          disabled={catalogState !== "ready" || (!!editing && !dirty)}
          title={editing && !dirty ? "Chưa có thay đổi" : undefined}
        >
          Lưu
        </Button>
      </div>
    </form>
  );
}

export default CollectiveRecordEditor;
