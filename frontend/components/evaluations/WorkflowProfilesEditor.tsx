"use client";

import React, { useEffect, useState } from "react";
import {
  DEFAULT_STEP_PERMISSIONS,
  GATE_ALLOWED_MODES,
  GATE_CODES,
  GATE_MODE_LABELS,
  GATE_NAMES,
  GateCode,
  GateMode,
  defaultGateMode,
  MANDATORY_STEPS,
  NEVER_EXTERNAL_STEPS,
  OWNER_STEPS,
  PeriodSettings,
  ProfileStepSetting,
  STEP_MODE_LABELS,
  STEP_NAMES,
  STEP_ORDER,
  StepMode,
  StepPermissionOptionDto,
  WorkflowProfile,
  WorkflowStepCode,
} from "@/services/evaluationService";
import { DEADLINE_RULE_LABELS, DEADLINE_RULE_LIMITS, DeadlineRuleKind } from "@/services/deadlineRules";

interface Props {
  settings: PeriodSettings;
  onChange: (settings: PeriodSettings) => void;
  /** Sửa diện quyết định, chế độ, quyền (chỉ khi kỳ còn dự thảo). */
  editable: boolean;
  /** Sửa thời hạn (kỳ chưa đóng). */
  deadlinesEditable: boolean;
  /** Mã quyền chọn được làm quyền thực hiện bước nội bộ (từ máy chủ). */
  permissionOptions: StepPermissionOptionDto[];
  /** Mã diện quyết định đã lưu (không đổi mã được vì hồ sơ đánh giá có thể đang tham chiếu). */
  savedCodes: string[];
  /** Điền thời hạn các bước theo quy tắc (máy chủ tính và lưu); ngày sau đó vẫn sửa tự do. */
  onApplyRules?: () => void;
}

const AUTHORITIES: { key: string; label: string }[] = [
  { key: "CoSo", label: "Người thuộc diện Đảng ủy cơ sở quyết định" },
  { key: "CapTren", label: "Người thuộc diện cấp trên quyết định" },
];

/** Diện quyết định của kỳ: bước × chế độ × quyền thực hiện × thời hạn; máy chủ kiểm tra lại toàn bộ khi lưu. */
export function WorkflowProfilesEditor({ settings, onChange, editable, deadlinesEditable, permissionOptions, savedCodes, onApplyRules }: Props) {
  const [selected, setSelected] = useState(settings.profiles[0]?.code ?? "");

  useEffect(() => {
    if (!settings.profiles.some((p) => p.code === selected)) setSelected(settings.profiles[0]?.code ?? "");
  }, [settings.profiles, selected]);

  const profile = settings.profiles.find((p) => p.code === selected);

  const updateProfile = (code: string, change: (p: WorkflowProfile) => WorkflowProfile) =>
    onChange({ ...settings, profiles: settings.profiles.map((p) => (p.code === code ? change(p) : p)) });

  const setStep = (step: WorkflowStepCode, changes: Partial<ProfileStepSetting>) => {
    if (!profile) return;
    updateProfile(profile.code, (p) => {
      const current = p.steps[step] ?? { mode: "Internal" as StepMode };
      const next: ProfileStepSetting = { ...current, ...changes };
      if (changes.mode) {
        next.permission = changes.mode === "Internal" && !OWNER_STEPS.includes(step) ? current.permission || DEFAULT_STEP_PERMISSIONS[step] : null;
      }
      return { ...p, steps: { ...p.steps, [step]: next } };
    });
  };

  const addProfile = () => {
    const base = profile ?? settings.profiles[0];
    let index = settings.profiles.length + 1;
    let code = `nhom-${index}`;
    while (settings.profiles.some((p) => p.code === code)) code = `nhom-${++index}`;
    const copy: WorkflowProfile = base
      ? { ...JSON.parse(JSON.stringify(base)), code, name: `${base.name} (bản sao)` }
      : { code, name: "Diện quyết định mới", description: null, steps: {} };
    onChange({ ...settings, profiles: [...settings.profiles, copy] });
    setSelected(code);
  };

  const removeProfile = () => {
    if (!profile) return;
    onChange({ ...settings, profiles: settings.profiles.filter((p) => p.code !== profile.code) });
  };

  const renameCode = (value: string) => {
    if (!profile) return;
    const code = value.toLowerCase();
    const defaults = Object.fromEntries(Object.entries(settings.defaultProfiles).map(([k, v]) => [k, v === profile.code ? code : v]));
    onChange({
      ...settings,
      defaultProfiles: defaults,
      profiles: settings.profiles.map((p) => (p.code === profile.code ? { ...p, code } : p)),
    });
    setSelected(code);
  };

  const gateMode = (gate: GateCode): GateMode => {
    const set = profile?.gates?.[gate]?.mode;
    return set && GATE_ALLOWED_MODES[gate].includes(set) ? set : defaultGateMode(gate, profile?.steps.B4_DECISION?.mode ?? "Internal");
  };

  const setGate = (gate: GateCode, mode: GateMode) => {
    if (!profile) return;
    updateProfile(profile.code, (p) => ({ ...p, gates: { ...(p.gates ?? {}), [gate]: { mode } } }));
  };

  const setRule = (step: WorkflowStepCode, kind: DeadlineRuleKind | "") => {
    if (!profile) return;
    const current = profile.steps[step]?.rule;
    setStep(step, {
      rule: kind
        ? { kind, value: Math.min(Math.max(current?.value ?? 1, DEADLINE_RULE_LIMITS[kind].min), DEADLINE_RULE_LIMITS[kind].max) }
        : null,
    });
  };

  const setRuleValue = (step: WorkflowStepCode, value: number) => {
    const rule = profile?.steps[step]?.rule;
    if (rule) setStep(step, { rule: { ...rule, value } });
  };

  const usedAsDefault = !!profile && Object.values(settings.defaultProfiles).includes(profile.code);
  const permissionName = (code?: string | null) => permissionOptions.find((o) => o.code === code)?.name || code || "—";

  return (
    <div className="d-flex flex-column gap-3">
      <div className="row g-2">
        {AUTHORITIES.map((a) => (
          <div className="col-md-6" key={a.key}>
            <label className="form-label mb-0" style={{ fontSize: 13 }} htmlFor={`default-${a.key}`}>Diện quyết định mặc định — {a.label}</label>
            <select id={`default-${a.key}`} className="form-select form-select-sm" value={settings.defaultProfiles[a.key] || ""} disabled={!editable}
              onChange={(e) => onChange({ ...settings, defaultProfiles: { ...settings.defaultProfiles, [a.key]: e.target.value } })}>
              <option value="">— Chọn —</option>
              {settings.profiles.map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}
            </select>
          </div>
        ))}
      </div>

      <div className="d-flex flex-wrap gap-2 align-items-center border-bottom pb-2" role="tablist" aria-label="Diện quyết định">
        {settings.profiles.map((p) => (
          <button key={p.code} type="button" role="tab" aria-selected={p.code === selected}
            className={`btn btn-sm rounded-pill px-3 ${p.code === selected ? "btn-primary" : "btn-outline-primary"}`} onClick={() => setSelected(p.code)}>
            {p.name || p.code}
          </button>
        ))}
        {editable && (
          <button type="button" className="btn btn-sm btn-outline-success" onClick={addProfile}>
            <i className="bi bi-plus-lg me-1" />Thêm diện quyết định (sao chép)
          </button>
        )}
      </div>

      {profile && (
        <div className="border rounded-3 p-3">
          <div className="row g-2 mb-2">
            <div className="col-md-3">
              <label className="form-label mb-0" style={{ fontSize: 13 }} htmlFor="profile-code">Mã</label>
              <input id="profile-code" className="form-control form-control-sm" value={profile.code} maxLength={50}
                disabled={!editable || savedCodes.includes(profile.code)} onChange={(e) => renameCode(e.target.value)} />
            </div>
            <div className="col-md-4">
              <label className="form-label mb-0" style={{ fontSize: 13 }} htmlFor="profile-name">Tên diện quyết định</label>
              <input id="profile-name" className="form-control form-control-sm" value={profile.name} maxLength={200} disabled={!editable}
                onChange={(e) => updateProfile(profile.code, (p) => ({ ...p, name: e.target.value }))} />
            </div>
            <div className="col-md-5">
              <label className="form-label mb-0" style={{ fontSize: 13 }} htmlFor="profile-desc">Mô tả</label>
              <input id="profile-desc" className="form-control form-control-sm" value={profile.description || ""} maxLength={1000} disabled={!editable}
                onChange={(e) => updateProfile(profile.code, (p) => ({ ...p, description: e.target.value }))} />
            </div>
          </div>

          <div className="table-responsive">
            <table className="table table-sm align-middle mb-2">
              <thead>
                <tr className="text-secondary" style={{ fontSize: 13 }}><th>#</th><th>Bước</th><th style={{ width: 190 }}>Chế độ</th><th>Người thực hiện (quyền)</th><th style={{ width: 170 }}>Thời hạn</th></tr>
              </thead>
              <tbody>
                {STEP_ORDER.map((step, index) => {
                  const setting = profile.steps[step] ?? { mode: "Internal" as StepMode, permission: DEFAULT_STEP_PERMISSIONS[step] };
                  const owner = OWNER_STEPS.includes(step);
                  const modes = (["Internal", "External", "Off"] as StepMode[]).filter(
                    (m) => !(m === "Off" && MANDATORY_STEPS.includes(step)) && !(m === "External" && NEVER_EXTERNAL_STEPS.includes(step))
                  );
                  return (
                    <tr key={step} style={{ fontSize: 14, opacity: setting.mode === "Off" ? 0.6 : 1 }}>
                      <td>{index + 1}</td>
                      <td>{STEP_NAMES[step]}{MANDATORY_STEPS.includes(step) && <span className="text-secondary"> (bắt buộc)</span>}</td>
                      <td>
                        <select className="form-select form-select-sm" value={setting.mode} disabled={!editable || modes.length === 1}
                          aria-label={`Chế độ — ${STEP_NAMES[step]}`} onChange={(e) => setStep(step, { mode: e.target.value as StepMode })}>
                          {modes.map((m) => <option key={m} value={m}>{STEP_MODE_LABELS[m]}</option>)}
                        </select>
                      </td>
                      <td>
                        {setting.mode === "Off" ? (
                          <span className="text-secondary">—</span>
                        ) : setting.mode === "External" ? (
                          <span className="text-secondary">—</span>
                        ) : owner ? (
                          <span className="text-secondary">Chủ hồ sơ</span>
                        ) : editable ? (
                          <select className="form-select form-select-sm" value={setting.permission || DEFAULT_STEP_PERMISSIONS[step]}
                            aria-label={`Quyền thực hiện — ${STEP_NAMES[step]}`} onChange={(e) => setStep(step, { permission: e.target.value })}>
                            {permissionOptions.length === 0 && <option value={setting.permission || DEFAULT_STEP_PERMISSIONS[step]}>{setting.permission || DEFAULT_STEP_PERMISSIONS[step]}</option>}
                            {permissionOptions.map((o) => <option key={o.code} value={o.code} title={o.description}>{o.name}</option>)}
                          </select>
                        ) : (
                          <span>{permissionName(setting.permission || DEFAULT_STEP_PERMISSIONS[step])}</span>
                        )}
                      </td>
                      <td>
                        <div className="d-flex flex-column gap-1">
                          <div className="d-flex gap-1">
                            <select className="form-select form-select-sm" value={setting.rule?.kind ?? ""}
                              disabled={!deadlinesEditable || setting.mode === "Off"} aria-label={`Cách tính thời hạn — ${STEP_NAMES[step]}`}
                              onChange={(e) => setRule(step, e.target.value as DeadlineRuleKind | "")}>
                              <option value="">Nhập tay</option>
                              {(Object.keys(DEADLINE_RULE_LABELS) as DeadlineRuleKind[]).map((k) => <option key={k} value={k}>{DEADLINE_RULE_LABELS[k]}</option>)}
                            </select>
                            {setting.rule && (
                              <input type="number" className="form-control form-control-sm" style={{ width: 64 }} value={setting.rule.value}
                                min={DEADLINE_RULE_LIMITS[setting.rule.kind].min} max={DEADLINE_RULE_LIMITS[setting.rule.kind].max}
                                disabled={!deadlinesEditable || setting.mode === "Off"} aria-label={`Số ngày — ${STEP_NAMES[step]}`}
                                onChange={(e) => setRuleValue(step, Number(e.target.value))} />
                            )}
                          </div>
                          <input type="date" className="form-control form-control-sm" value={setting.deadline || ""}
                            disabled={!deadlinesEditable || setting.mode === "Off"} aria-label={`Thời hạn — ${STEP_NAMES[step]}`}
                            onChange={(e) => setStep(step, { deadline: e.target.value || null })} />                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <div className="mb-3">
            <div className="fw-semibold mb-1" style={{ fontSize: 14 }}>Kiểm tra trước quyết định</div>
            <div className="row g-2">
              {GATE_CODES.map((gate) => (
                <div className="col-12 col-md-4" key={gate}>
                  <label className="form-label mb-0" style={{ fontSize: 13 }} htmlFor={`gate-${gate}`}>{GATE_NAMES[gate]}</label>
                  <select id={`gate-${gate}`} className="form-select form-select-sm" value={gateMode(gate)} disabled={!editable}
                    onChange={(e) => setGate(gate, e.target.value as GateMode)}>
                    {GATE_ALLOWED_MODES[gate].map((m) => <option key={m} value={m}>{GATE_MODE_LABELS[m]}</option>)}
                  </select>
                </div>
              ))}
            </div>
          </div>

          {deadlinesEditable && onApplyRules && (
            <div className="d-flex justify-content-end align-items-center mb-2">
              <button type="button" className="btn btn-outline-primary btn-sm" onClick={onApplyRules}>Tính lại thời hạn</button>
            </div>
          )}

          {editable && (
            <div className="d-flex justify-content-end align-items-center">
              <button type="button" className="btn btn-outline-danger btn-sm" disabled={settings.profiles.length <= 1 || usedAsDefault}
                title={usedAsDefault ? "Đang là diện quyết định mặc định — hãy đổi mặc định trước" : undefined} onClick={removeProfile}>
                Xóa diện quyết định
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default WorkflowProfilesEditor;
