"use client";

import React, { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { PageHeader } from "@/components/common/PageHeader";
import { initialsOf } from "@/components/layout/AccountMenu";
import { useAuth } from "@/contexts/AuthContext";
import { MyProfile, userService } from "@/services/userService";

/** Thông tin tài khoản của chính người đang đăng nhập. */
export default function AccountPage() {
  const { user } = useAuth();
  const [profile, setProfile] = useState<MyProfile | null>(null);

  useEffect(() => {
    userService.getMyProfile().then(setProfile).catch(() => setProfile(null));
  }, []);

  const scopes = useMemo(
    () => Array.from(new Set((user?.grants ?? []).map((g) => g.scopeName).filter(Boolean))),
    [user]
  );

  if (!user) return null;

  const rows: [string, React.ReactNode][] = [
    ["Họ và tên", user.fullName],
    ["Tên đăng nhập", user.userName],
    ["Chức danh", profile?.adminTitle || "—"],
    ["Phòng / đơn vị", profile?.adminDeptName || "—"],
    ["Chi bộ", profile?.partyBranchName || "—"],
  ];

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Thông tin tài khoản"
        actions={<Link href="/change-password" className="btn btn-outline-secondary btn-sm"><i className="bi bi-key me-1" />Đổi mật khẩu</Link>}
      />
      <div className="page-body d-flex flex-column" style={{ gap: 24 }}>
        <section className="card">
          <div className="card-body d-flex align-items-center gap-3">
            <span className="initials" style={{ width: 52, height: 52, fontSize: 17 }}>{initialsOf(user.fullName)}</span>
            <div className="min-w-0">
              <div className="fw-semibold" style={{ fontSize: 18 }}>{user.fullName}</div>
              <div className="text-secondary">{profile?.adminTitle || user.userName}</div>
            </div>
          </div>
          <dl className="card-body border-top m-0 row g-3">
            {rows.map(([label, value]) => (
              <React.Fragment key={label}>
                <dt className="col-sm-3 col-lg-2 mb-0 fw-normal text-secondary">{label}</dt>
                <dd className="col-sm-9 col-lg-10 mb-0 fw-medium">{value}</dd>
              </React.Fragment>
            ))}
          </dl>
        </section>

        <section className="card">
          <div className="card-header"><h2 className="h6 m-0">Vai trò</h2></div>
          <div className="card-body d-flex flex-column gap-3">
            <div className="d-flex flex-wrap gap-2">
              {user.roles.length === 0 ? <span className="text-secondary">Chưa được gán vai trò.</span> : user.roles.map((r) => <span key={r} className="tag t-info">{r}</span>)}
            </div>
            {scopes.length > 0 && (
              <div>
                <div className="text-secondary small mb-1">Phạm vi</div>
                <div className="d-flex flex-wrap gap-2">{scopes.map((s) => <span key={s} className="tag">{s}</span>)}</div>
              </div>
            )}
          </div>
        </section>
      </div>
    </div>
  );
}
