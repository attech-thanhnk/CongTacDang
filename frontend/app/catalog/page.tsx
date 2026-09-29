"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { PageHeader } from "@/components/common";
import { OrgTreeTab } from "@/components/admin/OrgTreeTab";
import { PositionsTab } from "@/components/admin/PositionsTab";
import { UnitTypesTab } from "@/components/admin/UnitTypesTab";
import { errorMessage, errorTitle } from "@/components/admin/adminUtils";
import { catalogService, OrgUnitType } from "@/services/catalogService";

type Tab = "departments" | "branches" | "unit-types" | "positions";

const TABS: { key: Tab; label: string; icon: string }[] = [
  { key: "departments", label: "Đơn vị chính quyền", icon: "bi-building" },
  { key: "branches", label: "Tổ chức Đảng", icon: "bi-flag" },
  { key: "unit-types", label: "Loại đơn vị", icon: "bi-tags" },
  { key: "positions", label: "Chức vụ", icon: "bi-person-badge" },
];

/**
 * Danh mục tổ chức (task 14): cây đơn vị chính quyền, cây tổ chức Đảng, loại đơn vị, chức vụ.
 * Xem: mọi người đã đăng nhập; sửa: quyền "Quản lý danh mục".
 */
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
      toast.error(errorMessage(err, "Không tải được danh mục loại đơn vị."), errorTitle(err));
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
      <PageHeader
        title="Danh mục tổ chức"
        subTitle={
          canManage
            ? "Cây đơn vị chính quyền và tổ chức Đảng (bao nhiêu cấp cũng được), loại đơn vị, danh mục chức vụ."
            : "Cây đơn vị, loại đơn vị và danh mục chức vụ (chỉ xem)."
        }
      />

      <div className="page-body">
        <ul className="nav nav-tabs mb-3">
          {TABS.map((t) => (
            <li className="nav-item" key={t.key}>
              <button type="button" className={`nav-link ${tab === t.key ? "active fw-semibold" : ""}`} onClick={() => setTab(t.key)}>
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
      </div>
    </div>
  );
}
