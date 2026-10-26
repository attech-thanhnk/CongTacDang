"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { PageHeader } from "@/components/common";
import { OrgTreeTab } from "@/components/admin/OrgTreeTab";
import { HolidaysTab } from "@/components/admin/HolidaysTab";
import { FormLayoutsTab } from "@/components/admin/FormLayoutsTab";
import { PositionsTab } from "@/components/admin/PositionsTab";
import { UnitTypesTab } from "@/components/admin/UnitTypesTab";
import { errorMessage, errorTitle } from "@/components/admin/adminUtils";
import { catalogService, OrgUnitType } from "@/services/catalogService";

type Tab = "departments" | "branches" | "unit-types" | "positions" | "form-layouts" | "holidays";

const TABS: { key: Tab; label: string; icon: string }[] = [
  { key: "departments", label: "Đơn vị chính quyền", icon: "bi-building" },
  { key: "branches", label: "Tổ chức Đảng", icon: "bi-flag" },
  { key: "unit-types", label: "Loại đơn vị", icon: "bi-tags" },
  { key: "positions", label: "Chức vụ", icon: "bi-person-badge" },
  { key: "form-layouts", label: "Kiểu trình bày biểu mẫu", icon: "bi-file-earmark-text" },
  { key: "holidays", label: "Ngày nghỉ", icon: "bi-calendar-x" },
];

/** Danh mục tổ chức: mọi người đã đăng nhập xem được; sửa cần quyền "Quản lý danh mục". */
export default function CatalogPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canManage = hasPermission("catalog.manage");
  const [tab, setTab] = useState<Tab>("departments");
  const [unitTypes, setUnitTypes] = useState<OrgUnitType[]>([]);
  const [typesLoading, setTypesLoading] = useState(true);

  const loadUnitTypes = useCallback(async () => {
    setTypesLoading(true);
    try {
      setUnitTypes(await catalogService.listUnitTypes());
    } catch (err) {
      toast.error(errorMessage(err, "Tải danh mục loại đơn vị không thành công."), errorTitle(err));
    } finally {
      setTypesLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    loadUnitTypes();
  }, [loadUnitTypes]);

  return (
    <div className="page-wrapper">
      <PageHeader title="Danh mục tổ chức" />

      <div className="page-body">
        <div className="d-flex flex-column gap-3">
        <ul className="nav nav-tabs flex-nowrap" style={{ overflowX: "auto", overflowY: "hidden" }}>
          {TABS.map((t) => (
            <li className="nav-item" key={t.key}>
              <button type="button" className={`nav-link text-nowrap ${tab === t.key ? "active" : ""}`} onClick={() => setTab(t.key)}>
                <i className={`bi ${t.icon} me-1`} />
                {t.label}
              </button>
            </li>
          ))}
        </ul>

        {tab === "departments" && <OrgTreeTab key="departments" kind="departments" unit="đơn vị" canManage={canManage} unitTypes={unitTypes} />}
        {tab === "branches" && <OrgTreeTab key="branches" kind="branches" unit="tổ chức Đảng" canManage={canManage} unitTypes={unitTypes} />}
        {tab === "unit-types" && <UnitTypesTab unitTypes={unitTypes} loading={typesLoading} canManage={canManage} onChanged={loadUnitTypes} />}
        {tab === "positions" && <PositionsTab canManage={canManage} />}
        {tab === "form-layouts" && <FormLayoutsTab canManage={canManage} />}
        {tab === "holidays" && <HolidaysTab canManage={canManage} />}
        </div>
      </div>
    </div>
  );
}
