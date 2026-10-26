"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { Badge, BadgeTone } from "@/components/common/StatusBadge";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { CriteriaSummary } from "@/components/evaluations/CriteriaSummary";
import { HeaderMenu } from "@/components/common/HeaderMenu";
import { NoAccess } from "@/components/admin/NoAccess";
import { errorMessage, errorStatus } from "@/components/admin/adminUtils";
import {
  COLLECTIVE_KIND_LABELS,
  CollectiveCriteriaGroup,
  CollectiveScoreKind,
  CriteriaGroup,
  CriteriaSetContent,
  CriteriaSetDto,
  EXCELLENT_QUOTA_PERCENT,
  excellentQuotaInvalid,
  FORM_CONFIG_LIMITS,
  GRADE_LABELS,
  RECORD_FORMS,
  ROUNDING_LABELS,
  RoundingRule,
  ScoreRoundingMode,
  SelfAssessmentSection,
  collectiveMax,
  collectiveTotal,
  criteriaService,
  fmt,
  MAX_FRAME_ADJUST_LIMIT,
  quickChecks,
} from "@/services/criteriaService";

type Rounding = CriteriaSetContent["parameters"]["rounding"];

type EditorSection = "info" | "personal" | "collective" | "forms" | "grading" | "params";

// Trình soạn dài nên chia tab theo nhóm nội dung.
const EDITOR_SECTIONS: { key: EditorSection; label: string }[] = [
  { key: "info", label: "Thông tin chung" },
  { key: "personal", label: "Tiêu chí cá nhân" },
  { key: "collective", label: "Tiêu chí tập thể" },
  { key: "forms", label: "Biểu mẫu" },
  { key: "grading", label: "Tỷ trọng và xếp loại" },
  { key: "params", label: "Tham số" },
];

const STATUS_TONE: Record<string, BadgeTone> = {
  Draft: "warning",
  Published: "success",
  Archived: "neutral",
};

/** Xem / sửa bản nháp bộ tiêu chí (bảng sửa trực tiếp, kiểm tra tổng điểm tức thì), xuất bản, nhân bản, lưu trữ. */
export default function CriteriaSetPage() {
  const params = useParams<{ id: string }>();
  const id = params?.id;
  const router = useRouter();
  const { hasPermission } = useAuth();
  const canManage = hasPermission("criteria.manage");
  const canView = canManage || hasPermission("period.manage");
  const { toast, confirm } = useToast();
  const rowKey = useRowKeys();
  const [set, setSet] = useState<CriteriaSetDto | null>(null);
  const [content, setContent] = useState<CriteriaSetContent | null>(null);
  const [info, setInfo] = useState({ code: "", name: "", notes: "" });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState<string | null>(null);
  const [editorTab, setEditorTab] = useState<EditorSection>("info");
  const [saved, setSaved] = useState("");

  const load = useCallback(async () => {
    if (!id || !canView) return;
    try {
      const loaded = await criteriaService.get(id);
      setSet(loaded);
      const copy: CriteriaSetContent = JSON.parse(JSON.stringify(loaded.content));
      copy.requiredForms = copy.requiredForms ?? [];
      copy.selfAssessmentSections = copy.selfAssessmentSections ?? [];
      copy.taskLevels = copy.taskLevels ?? [];
      copy.collectiveGroups = copy.collectiveGroups ?? [];
      setContent(copy);
      const loadedInfo = { code: loaded.code, name: loaded.name, notes: loaded.notes || "" };
      setInfo(loadedInfo);
      setSaved(JSON.stringify({ info: loadedInfo, content: copy }));
      setConflict(null);
    } catch (err: unknown) {
      setError(errorMessage(err, "Tải bộ tiêu chí không thành công."));
    }
  }, [id, canView]);

  useEffect(() => {
    load();
  }, [load]);

  const checks = useMemo(() => (content ? quickChecks(content) : []), [content]);
  const dirty = useMemo(() => !!content && JSON.stringify({ info, content }) !== saved, [info, content, saved]);

  if (!canView) return <NoAccess title="Bộ tiêu chí" permissionName="Quản lý bộ tiêu chí" />;
  if (error) return <div className="page-wrapper"><div className="alert alert-danger m-4">{error}</div></div>;
  if (!set || !content) return <div className="page-wrapper"><div className="text-secondary p-4 loading-delay"><span className="spinner-border spinner-border-sm me-2" />Đang tải...</div></div>;

  const editable = canManage && set.status === "Draft";
  const p = content.parameters;

  const run = async (work: () => Promise<unknown>, success: string) => {
    setBusy(true);
    try {
      await work();
      toast.success(success);
      await load();
    } catch (err: unknown) {
      const message = errorMessage(err, "Thực hiện thao tác không thành công.");
      // Xung đột: giữ nguyên nội dung đang sửa, để người dùng chủ động tải lại.
      if (errorStatus(err) === 409) setConflict(message);
      else toast.error(message);
    } finally {
      setBusy(false);
    }
  };

  const save = () =>
    run(() => criteriaService.update(set.id, { version: set.version, ...info, content }), "Đã lưu bản nháp.");

  const publish = () =>
    confirm({
      title: "Xuất bản bộ tiêu chí",
      message: "Sau khi xuất bản, bộ không sửa được nữa.",
      confirmText: "Xuất bản",
      isDanger: false,
      onConfirm: () => run(() => criteriaService.publish(set.id, set.version), "Đã xuất bản bộ tiêu chí."),
    });

  const clone = () =>
    run(async () => {
      const created = await criteriaService.clone(set.id);
      router.push(`/criteria/${created.id}`);
    }, "Đã nhân bản thành bản nháp mới.");

  const update = (change: Partial<CriteriaSetContent>) => setContent({ ...content, ...change });
  const setParam = <K extends keyof CriteriaSetContent["parameters"]>(key: K, value: CriteriaSetContent["parameters"][K]) =>
    update({ parameters: { ...p, [key]: value } });
  const setGroup = (index: number, change: Partial<CriteriaGroup>) =>
    update({ generalGroups: content.generalGroups.map((g, i) => (i === index ? rowKey.carry(g, { ...g, ...change }) : g)) });
  const num = (value: string) => (value === "" ? 0 : Number(value));
  const toggleForm = (code: string, on: boolean) =>
    update({ requiredForms: on ? [...content.requiredForms.filter((c) => c !== code), code] : content.requiredForms.filter((c) => c !== code) });
  const setSection = (index: number, change: Partial<SelfAssessmentSection>) =>
    update({ selfAssessmentSections: content.selfAssessmentSections.map((s, i) => (i === index ? { ...s, ...change } : s)) });

  const setCollective = (index: number, change: Partial<CollectiveCriteriaGroup>) =>
    update({ collectiveGroups: content.collectiveGroups.map((g, i) => (i === index ? rowKey.carry(g, { ...g, ...change }) : g)) });
  const fillCollectiveDefaults = async () => {
    setBusy(true);
    try {
      const groups = await criteriaService.defaultCollectiveGroups();
      update({ collectiveGroups: groups });
    } catch (err: unknown) {
      toast.error(errorMessage(err, "Tải khung tiêu chí tập thể mặc định không thành công."));
    } finally {
      setBusy(false);
    }
  };

  const generalTotal = content.generalGroups.reduce((s, g) => s + g.items.reduce((t, i) => t + (Number(i.maxScore) || 0), 0), 0);

  return (
    <div className="page-wrapper">
      <PageHeader
        title={set.name}
        subTitle={`Mã ${set.code}`}
        badge={<Badge tone={STATUS_TONE[set.status] ?? "neutral"}>{set.statusDisplayName}</Badge>}
        actions={
          canManage ? (
            <>
              {!editable && (
                <button type="button" className="btn btn-outline-secondary" disabled={busy} onClick={clone}>
                  <i className="bi bi-copy me-1" />Nhân bản
                </button>
              )}
              {editable && (
                <button type="button" className="btn btn-outline-secondary" disabled={busy || !dirty} onClick={save}>
                  {busy ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-check2 me-1" />}Lưu
                </button>
              )}
              {editable && (
                <button type="button" className="btn btn-primary" disabled={busy || dirty} onClick={publish}
                  title={dirty ? "Lưu thay đổi trước khi xuất bản" : undefined}>
                  <i className="bi bi-send me-1" />Xuất bản
                </button>
              )}
              <HeaderMenu
                itemLabel={set.name}
                disabled={busy}
                actions={[
                  { key: "clone", label: "Nhân bản", icon: "bi-copy", hidden: !editable, onClick: clone },
                  {
                    key: "archive", label: "Lưu trữ", icon: "bi-archive", danger: true, hidden: set.status !== "Published",
                    onClick: () => confirm({ title: "Lưu trữ bộ tiêu chí", message: "Kỳ đã dùng bộ này không bị ảnh hưởng; bộ không chọn được cho kỳ mới.", confirmText: "Lưu trữ", isDanger: true, onConfirm: () => run(() => criteriaService.archive(set.id, set.version), "Đã lưu trữ bộ tiêu chí.") }),
                  },
                ]}
              />
            </>
          ) : undefined
        }
      />

      <div className="page-body">
        {conflict && (
          <div className="alert alert-warning d-flex flex-wrap justify-content-between align-items-center gap-2 mb-0" role="alert">
            <span>{conflict} Nội dung đang sửa vẫn được giữ trên màn hình.</span>
            <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy}
              onClick={() => confirm({ title: "Tải lại bộ tiêu chí", message: "Các thay đổi chưa lưu trên màn hình sẽ bị bỏ.", confirmText: "Tải lại", isDanger: true, onConfirm: load })}>
              Tải lại (bỏ thay đổi)
            </button>
          </div>
        )}
        {set.usedByPeriods.length > 0 && (
          <div className="d-flex flex-wrap align-items-center gap-2">
            <span className="text-secondary">Kỳ đang dùng</span>
            {set.usedByPeriods.map((name) => <span key={name} className="tag t-ok">{name}</span>)}
          </div>
        )}
        {set.notes && !editable && <div className="alert alert-light border mb-0">{set.notes}</div>}
        {set.validationErrors.length > 0 && (
          <div className="alert alert-warning mb-0">
            <div className="fw-semibold mb-1">Chưa xuất bản được — lỗi kiểm tra (theo lần lưu gần nhất):</div>
            <ul className="mb-0">{set.validationErrors.map((e) => <li key={e}>{e}</li>)}</ul>
          </div>
        )}

        {!editable ? (
          <CriteriaSummary content={content} navigation />
        ) : (
          <>
            {checks.length > 0 && (
              <div className="alert alert-warning small mb-0"><ul className="mb-0">{checks.map((c) => <li key={c}>{c}</li>)}</ul></div>
            )}

            <ul className="nav nav-tabs flex-nowrap" style={{ overflowX: "auto", overflowY: "hidden" }}>
              {EDITOR_SECTIONS.map((t) => (
                <li className="nav-item" key={t.key}>
                  <button type="button" className={`nav-link text-nowrap ${editorTab === t.key ? "active" : ""}`} onClick={() => setEditorTab(t.key)}>
                    {t.label}
                  </button>
                </li>
              ))}
            </ul>

            {editorTab === "info" && (
            <section className="card">
              <div className="card-body row g-2">
                <div className="col-md-4"><label className="form-label" htmlFor="cs-code">Mã bộ</label><input id="cs-code" className="form-control form-control-sm" maxLength={50} value={info.code} onChange={(e) => setInfo({ ...info, code: e.target.value })} /></div>
                <div className="col-md-8"><label className="form-label" htmlFor="cs-name">Tên bộ</label><input id="cs-name" className="form-control form-control-sm" maxLength={200} value={info.name} onChange={(e) => setInfo({ ...info, name: e.target.value })} /></div>
                <div className="col-12"><label className="form-label" htmlFor="cs-notes">Ghi chú</label><textarea id="cs-notes" className="form-control form-control-sm" rows={2} maxLength={4000} value={info.notes} onChange={(e) => setInfo({ ...info, notes: e.target.value })} /></div>
              </div>
            </section>
            )}

            {editorTab === "personal" && (
            <section className="card">
              <div className="card-header d-flex justify-content-between align-items-center gap-2">
                <h2 className="mb-0">Nhóm tiêu chí chung — tổng <span className={Math.abs(generalTotal - p.generalMaxScore) > 1e-6 ? "text-danger" : "text-success"}>{fmt(generalTotal)}</span> / {fmt(p.generalMaxScore)}</h2>
                <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => update({ generalGroups: [...content.generalGroups, { code: String(content.generalGroups.length + 1), name: "", scoringMode: "Binary", items: [] }] })}><i className="bi bi-plus-lg me-1" />Thêm nhóm</button>
              </div>
              <div className="card-body">
                {content.generalGroups.map((g, gi) => (
                  <div className="border rounded-3 p-2 mb-2" key={rowKey(g)}>
                    <div className="row g-2 align-items-end mb-2">
                      <div className="col-md-1"><label className="form-label mb-1">Mã</label><input className="form-control form-control-sm" aria-label="Mã nhóm" value={g.code} onChange={(e) => setGroup(gi, { code: e.target.value })} /></div>
                      <div className="col-md-6"><label className="form-label mb-1">Tên nhóm</label><input className="form-control form-control-sm" aria-label="Tên nhóm" value={g.name} onChange={(e) => setGroup(gi, { name: e.target.value })} /></div>
                      <div className="col-md-3"><label className="form-label mb-1">Cách chấm</label>
                        <select className="form-select form-select-sm" aria-label="Cách chấm" value={g.scoringMode} onChange={(e) => setGroup(gi, { scoringMode: e.target.value as CriteriaGroup["scoringMode"] })}>
                          <option value="Binary">Đảm bảo / Không đảm bảo</option>
                          <option value="Range">Nhập điểm 0..tối đa</option>
                        </select>
                      </div>
                      <div className="col-md-2 small text-secondary">Cộng nhóm: <strong>{fmt(g.items.reduce((s, i) => s + (Number(i.maxScore) || 0), 0))}</strong>
                        <button type="button" className="btn btn-link btn-sm text-danger p-0 ms-2" onClick={() => update({ generalGroups: content.generalGroups.filter((_, i) => i !== gi) })}>Xóa nhóm</button>
                      </div>
                    </div>
                    <div className="table-responsive"><table className="table table-sm mb-1">
                      <thead><tr><th style={{ width: 80 }}>Mã</th><th>Nội dung tiêu chí con</th><th style={{ width: 90 }}>Tối đa</th><th style={{ width: 30 }} /></tr></thead>
                      <tbody>
                        {g.items.map((item, ii) => {
                          const setItem = (change: Partial<typeof item>) => setGroup(gi, { items: g.items.map((x, i) => (i === ii ? rowKey.carry(x, { ...x, ...change }) : x)) });
                          return (
                            <tr key={rowKey(item)}>
                              <td><input className="form-control form-control-sm" aria-label="Mã tiêu chí con" value={item.code} onChange={(e) => setItem({ code: e.target.value })} /></td>
                              <td>
                                <textarea className="form-control form-control-sm" rows={1} value={item.text} aria-label="Nội dung tiêu chí con" onChange={(e) => setItem({ text: e.target.value })} />
                                <textarea className="form-control form-control-sm mt-1" rows={1} maxLength={4000} value={item.formText ?? ""} aria-label="Nội dung in trên phiếu"
                                  placeholder="Nội dung in trên phiếu (trống = in như trên)" onChange={(e) => setItem({ formText: e.target.value || null })} />
                              </td>
                              <td><input type="number" min={0} step={0.5} className="form-control form-control-sm" aria-label="Điểm tối đa" value={item.maxScore} onChange={(e) => setItem({ maxScore: num(e.target.value) })} /></td>
                              <td><button type="button" className="btn btn-link btn-sm text-danger p-0" aria-label="Xóa tiêu chí" onClick={() => setGroup(gi, { items: g.items.filter((_, i) => i !== ii) })}><i className="bi bi-x-lg" /></button></td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table></div>
                    <button type="button" className="btn btn-link btn-sm p-0" onClick={() => setGroup(gi, { items: [...g.items, { code: `${g.code}.${g.items.length + 1}`, text: "", maxScore: 1 }] })}><i className="bi bi-plus-lg me-1" />Thêm tiêu chí con</button>
                  </div>
                ))}
              </div>
            </section>
            )}

            {editorTab === "collective" && (
            <section className="card">
              <div className="card-header d-flex justify-content-between align-items-center gap-2 flex-wrap">
                <h2 className="mb-0">Khung tiêu chí tập thể (Mẫu 06, 07)</h2>
                <div className="d-flex gap-2">
                  {content.collectiveGroups.length === 0 && (
                    <button type="button" className="btn btn-outline-primary btn-sm" disabled={busy} onClick={fillCollectiveDefaults}>Điền theo mặc định</button>
                  )}
                  <button type="button" className="btn btn-outline-secondary btn-sm"
                    onClick={() => update({ collectiveGroups: [...content.collectiveGroups, { code: `N${content.collectiveGroups.length + 1}`, name: "", kind: "General", items: [] }] })}>
                    Thêm nhóm
                  </button>
                </div>
              </div>
              <div className="card-body">
                {content.collectiveGroups.length === 0 ? (
                  <div className="text-secondary">Chưa có khung: hồ sơ tập thể nhập thẳng điểm hai nhóm.</div>
                ) : (
                  <div className="mb-2 d-flex gap-3 flex-wrap">
                    {(["General", "Task"] as CollectiveScoreKind[]).map((kind) => {
                      const sum = collectiveTotal(content, kind);
                      const max = collectiveMax(content, kind);
                      return (
                        <span key={kind}>
                          {COLLECTIVE_KIND_LABELS[kind]}: <span className={Math.abs(sum - max) > 1e-6 ? "text-danger fw-semibold" : "text-success fw-semibold"}>{fmt(sum)}</span> / {fmt(max)}
                        </span>
                      );
                    })}
                  </div>
                )}
                {content.collectiveGroups.map((g, gi) => (
                  <div className="border rounded-3 p-2 mb-2" key={rowKey(g)}>
                    <div className="row g-2 align-items-end mb-2">
                      <div className="col-md-1"><label className="form-label mb-1">Mã</label><input className="form-control form-control-sm" maxLength={20} aria-label="Mã nhóm" value={g.code} onChange={(e) => setCollective(gi, { code: e.target.value })} /></div>
                      <div className="col-md-6"><label className="form-label mb-1">Tên nhóm</label><input className="form-control form-control-sm" maxLength={FORM_CONFIG_LIMITS.title} aria-label="Tên nhóm" value={g.name} onChange={(e) => setCollective(gi, { name: e.target.value })} /></div>
                      <div className="col-md-3"><label className="form-label mb-1">Cộng vào</label>
                        <select className="form-select form-select-sm" aria-label="Cộng vào" value={g.kind} onChange={(e) => setCollective(gi, { kind: e.target.value as CollectiveScoreKind })}>
                          <option value="General">{COLLECTIVE_KIND_LABELS.General}</option>
                          <option value="Task">{COLLECTIVE_KIND_LABELS.Task}</option>
                        </select>
                      </div>
                      <div className="col-md-2 small text-secondary">Cộng nhóm: <strong>{fmt(g.items.reduce((s, i) => s + (Number(i.maxScore) || 0), 0))}</strong>
                        <button type="button" className="btn btn-link btn-sm text-danger p-0 ms-2" onClick={() => update({ collectiveGroups: content.collectiveGroups.filter((_, i) => i !== gi) })}>Xóa nhóm</button>
                      </div>
                    </div>
                    <div className="table-responsive"><table className="table table-sm mb-1">
                      <thead><tr><th style={{ width: 80 }}>Mã</th><th>Nội dung tiêu chí</th><th style={{ width: 90 }}>Tối đa</th><th style={{ width: 30 }} /></tr></thead>
                      <tbody>
                        {g.items.map((item, ii) => {
                          const setItem = (change: Partial<typeof item>) => setCollective(gi, { items: g.items.map((x, i) => (i === ii ? rowKey.carry(x, { ...x, ...change }) : x)) });
                          return (
                            <tr key={rowKey(item)}>
                              <td><input className="form-control form-control-sm" maxLength={20} aria-label="Mã tiêu chí" value={item.code} onChange={(e) => setItem({ code: e.target.value })} /></td>
                              <td>
                                <textarea className="form-control form-control-sm" rows={2} maxLength={2000} value={item.text} aria-label="Nội dung tiêu chí tập thể" onChange={(e) => setItem({ text: e.target.value })} />
                                <textarea className="form-control form-control-sm mt-1" rows={1} maxLength={4000} value={item.formText ?? ""} aria-label="Nội dung in trên phiếu"
                                  placeholder="Nội dung in trên phiếu (trống = in như trên)" onChange={(e) => setItem({ formText: e.target.value || null })} />
                              </td>
                              <td><input type="number" min={0} max={100} step={0.5} className="form-control form-control-sm" aria-label="Điểm tối đa" value={item.maxScore} onChange={(e) => setItem({ maxScore: num(e.target.value) })} /></td>
                              <td><button type="button" className="btn btn-link btn-sm text-danger p-0" aria-label="Xóa tiêu chí" onClick={() => setCollective(gi, { items: g.items.filter((_, i) => i !== ii) })}><i className="bi bi-x-lg" /></button></td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table></div>
                    <button type="button" className="btn btn-link btn-sm p-0" onClick={() => setCollective(gi, { items: [...g.items, { code: `${g.code}.${g.items.length + 1}`, text: "", maxScore: 1 }] })}><i className="bi bi-plus-lg me-1" />Thêm tiêu chí con</button>
                  </div>
                ))}
              </div>
            </section>
            )}

            {editorTab === "personal" && (
            <section className="card">
              <div className="card-header d-flex justify-content-between align-items-center gap-2">
                <h2 className="mb-0">Trục kết quả</h2>
                <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => update({ axes: [...content.axes, { code: `T${content.axes.length + 1}`, name: "", description: "" }] })}><i className="bi bi-plus-lg me-1" />Thêm trục</button>
              </div>
              <div className="card-body">
                <div className="table-responsive"><table className="table table-sm mb-0">
                  <thead><tr><th style={{ width: 80 }}>Mã</th><th>Tên trục</th><th>Nội dung áp dụng</th><th style={{ width: 30 }} /></tr></thead>
                  <tbody>
                    {content.axes.map((a, ai) => {
                      const setAxis = (change: Partial<typeof a>) => update({ axes: content.axes.map((x, i) => (i === ai ? { ...x, ...change } : x)) });
                      return (
                        <tr key={ai}>
                          <td><input className="form-control form-control-sm" aria-label="Mã trục" value={a.code} onChange={(e) => setAxis({ code: e.target.value })} /></td>
                          <td><input className="form-control form-control-sm" aria-label="Tên trục" value={a.name} onChange={(e) => setAxis({ name: e.target.value })} /></td>
                          <td><textarea className="form-control form-control-sm" rows={1} aria-label="Nội dung áp dụng" value={a.description || ""} onChange={(e) => setAxis({ description: e.target.value })} /></td>
                          <td><button type="button" className="btn btn-link btn-sm text-danger p-0" aria-label="Xóa trục" onClick={() => update({ axes: content.axes.filter((_, i) => i !== ai) })}><i className="bi bi-x-lg" /></button></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table></div>
              </div>
            </section>
            )}

            {editorTab === "forms" && (
            <section className="card">
              <div className="card-header"><h2 className="mb-0">Biểu mẫu cá nhân áp dụng cho hồ sơ</h2></div>
              <div className="card-body">
                <div className="d-flex flex-column gap-1 mb-3">
                  {RECORD_FORMS.map((form) => {
                    const always = form.code === "09A";
                    return (
                      <label key={form.code} className="form-check small mb-0">
                        <input type="checkbox" className="form-check-input" checked={always || content.requiredForms.includes(form.code)} disabled={always}
                          onChange={(e) => toggleForm(form.code, e.target.checked)} />{" "}
                        <span className="form-check-label">{form.name}{always && " (luôn áp dụng)"}</span>
                      </label>
                    );
                  })}
                </div>
                <div className="d-flex justify-content-between align-items-center mb-2">
                  <h3 className="form-label mb-0 fw-semibold">Mục tự đánh giá của Mẫu 09C</h3>
                  <button type="button" className="btn btn-outline-secondary btn-sm"
                    onClick={() => update({ selfAssessmentSections: [...content.selfAssessmentSections, { code: `M${content.selfAssessmentSections.length + 1}`, title: "", guidance: null, note: null, maxLength: 6000, required: false }] })}>
                    Thêm mục
                  </button>
                </div>
                {content.selfAssessmentSections.map((section, si) => (
                  <div className="border rounded-3 p-2 mb-2" key={si}>
                    <div className="row g-2 align-items-end">
                      <div className="col-md-2"><label className="form-label mb-1">Mã</label><input className="form-control form-control-sm" maxLength={20} value={section.code} onChange={(e) => setSection(si, { code: e.target.value })} /></div>
                      <div className="col-md-6"><label className="form-label mb-1">Tiêu đề in trên biểu mẫu</label><input className="form-control form-control-sm" maxLength={FORM_CONFIG_LIMITS.title} value={section.title} onChange={(e) => setSection(si, { title: e.target.value })} /></div>
                      <div className="col-md-2"><label className="form-label mb-1">Tối đa (ký tự)</label>
                        <input type="number" min={FORM_CONFIG_LIMITS.sectionMin} max={FORM_CONFIG_LIMITS.sectionMax} step={100} className="form-control form-control-sm" value={section.maxLength} onChange={(e) => setSection(si, { maxLength: num(e.target.value) })} />
                      </div>
                      <div className="col-md-2 d-flex flex-column">
                        <label className="form-check small mb-0"><input type="checkbox" className="form-check-input" checked={section.required} onChange={(e) => setSection(si, { required: e.target.checked })} /> <span className="form-check-label">Bắt buộc khi nộp</span></label>
                        <button type="button" className="btn btn-link btn-sm text-danger p-0 text-start" onClick={() => update({ selfAssessmentSections: content.selfAssessmentSections.filter((_, i) => i !== si) })}>Xóa mục</button>
                      </div>
                      <div className="col-md-6"><label className="form-label mb-1">Câu dẫn (in dưới tiêu đề)</label><textarea className="form-control form-control-sm" rows={2} maxLength={FORM_CONFIG_LIMITS.guidance} value={section.guidance || ""} onChange={(e) => setSection(si, { guidance: e.target.value || null })} /></div>
                      <div className="col-md-6"><label className="form-label mb-1">Lưu ý (in sau nội dung)</label><textarea className="form-control form-control-sm" rows={2} maxLength={FORM_CONFIG_LIMITS.guidance} value={section.note || ""} onChange={(e) => setSection(si, { note: e.target.value || null })} /></div>
                    </div>
                  </div>
                ))}

                <div className="d-flex justify-content-between align-items-center mt-3 mb-2">
                  <h3 className="form-label mb-0 fw-semibold">Mức tự đánh giá của từng nhiệm vụ</h3>
                  <button type="button" className="btn btn-outline-secondary btn-sm"
                    onClick={() => update({ taskLevels: [...content.taskLevels, { code: Math.max(0, ...content.taskLevels.map((l) => l.code)) + 1, label: "" }] })}>
                    Thêm mức
                  </button>
                </div>
                {content.taskLevels.map((level, li) => (
                  <div className="row g-2 align-items-end mb-1" key={li}>
                    <div className="col-md-2"><label className="form-label mb-1">Mức</label>
                      <input type="number" min={1} max={99} className="form-control form-control-sm" value={level.code}
                        onChange={(e) => update({ taskLevels: content.taskLevels.map((l, i) => (i === li ? { ...l, code: num(e.target.value) } : l)) })} />
                    </div>
                    <div className="col-md-9"><label className="form-label mb-1">Nhãn mô tả</label>
                      <input className="form-control form-control-sm" maxLength={FORM_CONFIG_LIMITS.title} value={level.label}
                        onChange={(e) => update({ taskLevels: content.taskLevels.map((l, i) => (i === li ? { ...l, label: e.target.value } : l)) })} />
                    </div>
                    <div className="col-md-1">
                      <button type="button" className="btn btn-link btn-sm text-danger p-0" onClick={() => update({ taskLevels: content.taskLevels.filter((_, i) => i !== li) })}>Xóa</button>
                    </div>
                  </div>
                ))}
                <div className="row g-2 mt-1">
                  <div className="col-md-4"><label className="form-label mb-1">Tiền tố mỗi nhiệm vụ trên Mẫu 09C ({"{n}"} = số thứ tự)</label>
                    <input className="form-control form-control-sm" maxLength={100} value={content.taskLineLabel || ""} placeholder="Nhiệm vụ {n}:"
                      onChange={(e) => update({ taskLineLabel: e.target.value || null })} />
                  </div>
                  <div className="col-md-8"><label className="form-label mb-1">Câu nối mức trên Mẫu 09C ({"{level}"} = mã mức)</label>
                    <input className="form-control form-control-sm" maxLength={200} value={content.taskLevelPhrase || ""} placeholder="Tự đánh giá mức {level}."
                      onChange={(e) => update({ taskLevelPhrase: e.target.value || null })} />
                  </div>
                </div>
              </div>
            </section>
            )}

            {editorTab === "grading" && (
            <section className="card">
              <div className="card-header d-flex justify-content-between align-items-center gap-2">
                <h2 className="mb-0">Khung tỷ trọng A-B-C-D (%, tổng 100)</h2>
                <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => update({ weightFrames: [...content.weightFrames, { code: `K${content.weightFrames.length + 1}`, name: "", a: 0.25, b: 0.25, c: 0.25, d: 0.25 }] })}><i className="bi bi-plus-lg me-1" />Thêm khung</button>
              </div>
              <div className="card-body">
                <div className="table-responsive"><table className="table table-sm mb-2">
                  <thead><tr><th style={{ width: 80 }}>Mã</th><th>Tên khung</th><th style={{ width: 80 }}>A</th><th style={{ width: 80 }}>B</th><th style={{ width: 80 }}>C</th><th style={{ width: 80 }}>D</th><th style={{ width: 70 }}>Tổng</th><th style={{ width: 30 }} /></tr></thead>
                  <tbody>
                    {content.weightFrames.map((f, fi) => {
                      const setFrame = (change: Partial<typeof f>) => update({ weightFrames: content.weightFrames.map((x, i) => (i === fi ? { ...x, ...change } : x)) });
                      const sum = (f.a + f.b + f.c + f.d) * 100;
                      return (
                        <tr key={fi}>
                          <td><input className="form-control form-control-sm" value={f.code} onChange={(e) => setFrame({ code: e.target.value })} /></td>
                          <td><input className="form-control form-control-sm" value={f.name} onChange={(e) => setFrame({ name: e.target.value })} /></td>
                          {(["a", "b", "c", "d"] as const).map((k) => (
                            <td key={k}><input type="number" min={0} max={100} step={1} className="form-control form-control-sm" value={Math.round(f[k] * 10000) / 100}
                              onChange={(e) => setFrame({ [k]: num(e.target.value) / 100 } as Partial<typeof f>)} aria-label={`Tỷ trọng ${k.toUpperCase()} khung ${f.code}`} /></td>
                          ))}
                          <td className={`small ${Math.abs(sum - 100) > 1e-4 ? "text-danger" : "text-success"}`}>{fmt(sum)}%</td>
                          <td><button type="button" className="btn btn-link btn-sm text-danger p-0" aria-label="Xóa khung" onClick={() => update({ weightFrames: content.weightFrames.filter((_, i) => i !== fi) })}><i className="bi bi-x-lg" /></button></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table></div>
                <div className="row g-2">
                  <div className="col-md-4"><label className="form-label mb-1">Khung mặc định (cán bộ chưa có khung)</label>
                    <select className="form-select form-select-sm" value={p.defaultWeightFrameCode || ""} onChange={(e) => setParam("defaultWeightFrameCode", e.target.value || null)}>
                      <option value="">— Không (báo thiếu khi mở kỳ) —</option>
                      {content.weightFrames.map((f) => <option key={f.code} value={f.code}>{f.code} — {f.name}</option>)}
                    </select>
                  </div>
                </div>
              </div>
            </section>
            )}

            {editorTab === "grading" && (
            <section className="card">
              <div className="card-header d-flex justify-content-between align-items-center gap-2">
                <h2 className="mb-0">Thang quy đổi % A-B-C-D</h2>
                <button type="button" className="btn btn-outline-secondary btn-sm" onClick={() => update({ conversionScale: [...content.conversionScale, { minPercent: 0, label: "", description: "" }] })}><i className="bi bi-plus-lg me-1" />Thêm mức</button>
              </div>
              <div className="card-body">
                <div className="table-responsive"><table className="table table-sm mb-0">
                  <thead><tr><th style={{ width: 110 }}>Từ (%)</th><th style={{ width: 180 }}>Nhãn</th><th>Cách hiểu</th><th style={{ width: 30 }} /></tr></thead>
                  <tbody>
                    {content.conversionScale.map((b, bi) => {
                      const setBand = (change: Partial<typeof b>) => update({ conversionScale: content.conversionScale.map((x, i) => (i === bi ? { ...x, ...change } : x)) });
                      return (
                        <tr key={bi}>
                          <td><input type="number" min={0} max={100} className="form-control form-control-sm" value={b.minPercent} onChange={(e) => setBand({ minPercent: num(e.target.value) })} /></td>
                          <td><input className="form-control form-control-sm" value={b.label} onChange={(e) => setBand({ label: e.target.value })} /></td>
                          <td><textarea className="form-control form-control-sm" rows={1} value={b.description || ""} onChange={(e) => setBand({ description: e.target.value })} /></td>
                          <td><button type="button" className="btn btn-link btn-sm text-danger p-0" aria-label="Xóa mức" onClick={() => update({ conversionScale: content.conversionScale.filter((_, i) => i !== bi) })}><i className="bi bi-x-lg" /></button></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table></div>
              </div>
            </section>
            )}

            {editorTab === "grading" && (
            <section className="card">
              <div className="card-header"><h2 className="mb-0">Mức xếp loại</h2></div>
              <div className="card-body">
                <div className="table-responsive"><table className="table table-sm mb-0">
                  <thead><tr><th style={{ width: 230 }}>Mức</th><th style={{ width: 100 }}>Từ điểm</th><th style={{ width: 130 }}>% vượt chuẩn tối thiểu</th><th>Điều kiện kèm theo</th></tr></thead>
                  <tbody>
                    {content.grades.map((g, gi) => {
                      const setGrade = (change: Partial<typeof g>) => update({ grades: content.grades.map((x, i) => (i === gi ? { ...x, ...change } : x)) });
                      return (
                        <tr key={g.grade}>
                          <td className="small">{GRADE_LABELS[g.grade] ?? g.grade}</td>
                          <td><input type="number" min={0} max={100} step={0.5} className="form-control form-control-sm" value={g.minScore} onChange={(e) => setGrade({ minScore: num(e.target.value) })} /></td>
                          <td><input type="number" min={0} max={100} step={1} className="form-control form-control-sm" placeholder="Không đòi"
                            value={g.minExceedStandardRatio != null ? Math.round(g.minExceedStandardRatio * 100) : ""}
                            onChange={(e) => setGrade({ minExceedStandardRatio: e.target.value === "" ? null : num(e.target.value) / 100 })} /></td>
                          <td><textarea className="form-control form-control-sm" rows={2} value={g.conditions || ""} onChange={(e) => setGrade({ conditions: e.target.value })} /></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table></div>
              </div>
            </section>
            )}

            {editorTab === "params" && (
            <section className="card">
              <div className="card-header"><h2 className="mb-0">Tham số</h2></div>
              <div className="card-body">
                <div className="row g-2">
                  {([
                    ["minTasks", "Số sản phẩm tối thiểu", 1],
                    ["maxTasks", "Số sản phẩm tối đa", 1],
                    ["totalTaskWeight", "Tổng trọng số = điểm tối đa nhóm nhiệm vụ", 0.5],
                    ["taskWeightTolerance", "Sai số tổng trọng số", 0.01],
                    ["generalMaxScore", "Điểm tối đa nhóm tiêu chí chung", 0.5],
                    ["explanationThreshold", "Ngưỡng chênh lệch cần giải trình", 0.5],
                    ["collectiveGeneralMaxScore", "Tập thể: tối đa tiêu chí chung", 0.5],
                    ["collectiveTaskMaxScore", "Tập thể: tối đa nhiệm vụ", 0.5],
                  ] as [keyof CriteriaSetContent["parameters"], string, number][]).map(([key, label, step]) => (
                    <div className="col-6 col-md-3" key={key}>
                      <label className="form-label mb-1">{label}</label>
                      <input type="number" step={step} className="form-control form-control-sm" value={p[key] as number} onChange={(e) => setParam(key, num(e.target.value) as never)} />
                    </div>
                  ))}
                  <div className="col-6 col-md-3">
                    <label className="form-label mb-1">Giảm từ … điểm phải nêu lý do</label>
                    <input type="number" step={0.5} min={0} className="form-control form-control-sm" placeholder="Không bắt buộc" value={p.deductionReasonMinPoints ?? ""}
                      onChange={(e) => setParam("deductionReasonMinPoints", e.target.value === "" ? null : num(e.target.value))} />
                  </div>
                  <div className="col-6 col-md-3">
                    <label className="form-label small mb-0">Điều chỉnh tỷ lệ A-B-C-D (± điểm %)</label>
                    <input type="number" step={1} min={0} max={MAX_FRAME_ADJUST_LIMIT} className="form-control form-control-sm" placeholder="Không cho điều chỉnh" value={p.frameAdjustLimit ?? ""}
                      onChange={(e) => setParam("frameAdjustLimit", e.target.value === "" ? null : num(e.target.value))} />
                  </div>
                  <div className="col-6 col-md-3 d-flex flex-column justify-content-end">
                    <label className="form-check small mb-0"><input type="checkbox" className="form-check-input" checked={p.allowNotApplicable} onChange={(e) => setParam("allowNotApplicable", e.target.checked)} /> <span className="form-check-label">Cho phép Không áp dụng (K/AD), có lý do</span></label>
                    <label className="form-check small mb-0"><input type="checkbox" className="form-check-input" checked={p.explanationOnGradeChange} onChange={(e) => setParam("explanationOnGradeChange", e.target.checked)} /> <span className="form-check-label">Giải trình cả khi đổi mức</span></label>
                  </div>
                  <div className="col-12 col-md-6">
                    <label className="form-check small mb-1"><input type="checkbox" className="form-check-input" checked={!!p.publishScores} onChange={(e) => setParam("publishScores", e.target.checked)} /> <span className="form-check-label">Công khai kết quả kèm điểm chính thức</span></label>
                    <div className="small mb-0">Bắt buộc lập kế hoạch 30-60-90 ngày (Mẫu 17) với mức:</div>
                    <div className="d-flex flex-wrap gap-2">
                      {([["HoanThanhXuatSac", "Hoàn thành xuất sắc"], ["HoanThanhTot", "Hoàn thành tốt"], ["HoanThanh", "Hoàn thành (Mức C)"], ["KhongHoanThanh", "Không hoàn thành (Mức D)"]] as [string, string][]).map(([grade, label]) => {
                        const required = p.improvementPlanRequiredGrades ?? ["HoanThanh", "KhongHoanThanh"];
                        return (
                          <label key={grade} className="form-check small mb-0"><input type="checkbox" className="form-check-input" checked={required.includes(grade)}
                            onChange={(e) => setParam("improvementPlanRequiredGrades", e.target.checked ? [...required, grade] : required.filter((g) => g !== grade))} /> <span className="form-check-label">{label}</span></label>
                        );
                      })}
                    </div>
                    <label className="form-label mb-1 mt-2" htmlFor="cs-alert-days">Cảnh báo "kế hoạch cần lập" còn xét kỳ đã đóng trong (ngày)</label>
                    <input id="cs-alert-days" type="number" min={1} max={FORM_CONFIG_LIMITS.alertDaysMax} step={1} className="form-control form-control-sm" style={{ maxWidth: 140 }}
                      value={p.improvementPlanAlertDays ?? 90} onChange={(e) => setParam("improvementPlanAlertDays", Math.trunc(num(e.target.value)))} />
                  </div>
                  <div className="col-6 col-md-3">
                    <label className="form-label mb-1">Xử lý điểm khi Không áp dụng (K/AD)</label>
                    <select className="form-select form-select-sm" value={p.notApplicableRule} onChange={(e) => setParam("notApplicableRule", e.target.value as typeof p.notApplicableRule)}>
                      <option value="ExcludeAndRescale">Bỏ khỏi mẫu số rồi quy đổi</option>
                      <option value="GrantFull">Tính như đạt tối đa</option>
                    </select>
                  </div>
                  <div className="col-12 col-md-6">
                    <label className="form-label mb-1" htmlFor="cs-excellent-quota">Trần xuất sắc (% số &quot;Hoàn thành tốt&quot; của nhóm chức danh)</label>
                    <input id="cs-excellent-quota" type="number" min={EXCELLENT_QUOTA_PERCENT.min} max={EXCELLENT_QUOTA_PERCENT.max} step={1} style={{ maxWidth: 140 }}
                      className={`form-control form-control-sm${excellentQuotaInvalid(p.excellentQuota.ratio) ? " is-invalid" : ""}`}
                      value={Math.round(p.excellentQuota.ratio * 10000) / 100}
                      onChange={(e) => setParam("excellentQuota", { ...p.excellentQuota, ratio: num(e.target.value) / 100 })} />
                    <div className={excellentQuotaInvalid(p.excellentQuota.ratio) ? "invalid-feedback d-block" : "form-text"}>
                      Từ {EXCELLENT_QUOTA_PERCENT.min}% đến {EXCELLENT_QUOTA_PERCENT.max}%; làm tròn từ 0,5 trở lên thành 1; không tính người đứng đầu.
                    </div>
                  </div>
                </div>
                <h3 className="form-label fw-semibold mt-3">Làm tròn theo loại điểm</h3>
                <div className="row g-2">
                  {([
                    ["taskScore", "Điểm từng sản phẩm"],
                    ["tasksTotal", "Tổng điểm nhiệm vụ"],
                    ["generalTotal", "Tổng tiêu chí chung"],
                    ["total", "Tổng điểm"],
                  ] as [keyof Rounding, string][]).map(([key, label]) => {
                    const rule: RoundingRule = p.rounding[key];
                    const setRule = (change: Partial<RoundingRule>) => setParam("rounding", { ...p.rounding, [key]: { ...rule, ...change } });
                    return (
                      <div className="col-6 col-md-3" key={key}>
                        <label className="form-label mb-1">{label}</label>
                        <div className="input-group input-group-sm">
                          <input type="number" min={0} max={4} className="form-control" value={rule.decimals} onChange={(e) => setRule({ decimals: num(e.target.value) })} aria-label={`Số chữ số thập phân — ${label}`} />
                          <RoundingModeSelect value={rule.mode} onChange={(mode) => setRule({ mode })} />
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            </section>
            )}
          </>
        )}
      </div>
    </div>
  );
}

function RoundingModeSelect({ value, onChange }: { value: ScoreRoundingMode; onChange: (mode: ScoreRoundingMode) => void }) {
  return (
    <select className="form-select form-select-sm" value={value} onChange={(e) => onChange(e.target.value as ScoreRoundingMode)}>
      {(Object.keys(ROUNDING_LABELS) as ScoreRoundingMode[]).map((mode) => <option key={mode} value={mode}>{ROUNDING_LABELS[mode]}</option>)}
    </select>
  );
}

/** Khóa ổn định cho dòng sửa trực tiếp: nội dung không có id, dùng chỉ số mảng sẽ lẫn ô nhập khi thêm/xóa dòng. */
function useRowKeys() {
  const keys = useRef(new WeakMap<object, string>());
  const next = useRef(0);
  return useMemo(() => {
    const keyOf = (row: object): string => {
      let key = keys.current.get(row);
      if (!key) {
        next.current += 1;
        key = `r${next.current}`;
        keys.current.set(row, key);
      }
      return key;
    };
    const carry = <T extends object>(from: object, to: T): T => {
      keys.current.set(to, keyOf(from));
      return to;
    };
    return Object.assign(keyOf, { carry });
  }, []);
}
