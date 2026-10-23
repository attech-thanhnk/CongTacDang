"use client";

import React, { useEffect, useMemo, useState } from "react";
import { PageHeader } from "@/components/common/PageHeader";
import { ChangePasswordForm } from "@/components/account/ChangePasswordForm";
import { InfoSection, InfoSections } from "@/components/account/InfoSections";
import { initialsOf } from "@/components/layout/AccountMenu";
import { useAuth } from "@/contexts/AuthContext";
import { MyProfile, userService } from "@/services/userService";
import styles from "./account.module.css";

type Section = "profile" | "roles" | "security";

const SECTIONS: { key: Section; label: string; icon: string }[] = [
  { key: "profile", label: "Thông tin cá nhân", icon: "bi-person" },
  { key: "roles", label: "Vai trò", icon: "bi-shield-check" },
  { key: "security", label: "Mật khẩu", icon: "bi-key" },
];

/** Tài khoản của người đang đăng nhập: thông tin cá nhân, vai trò, đổi mật khẩu. */
export default function AccountPage() {
  const { user } = useAuth();
  const [profile, setProfile] = useState<MyProfile | null>(null);
  const [section, setSection] = useState<Section>("profile");

  useEffect(() => {
    userService.getMyProfile().then(setProfile).catch(() => setProfile(null));
    const hash = typeof window !== "undefined" ? window.location.hash.slice(1) : "";
    if (SECTIONS.some((s) => s.key === hash)) setSection(hash as Section);
  }, []);

  const scopes = useMemo(
    () => Array.from(new Set((user?.grants ?? []).map((g) => g.scopeName).filter(Boolean))),
    [user]
  );

  if (!user) return null;

  const choose = (key: Section) => {
    setSection(key);
    if (typeof window !== "undefined") window.history.replaceState(null, "", `#${key}`);
  };

  const sections: InfoSection[] = [
    {
      title: "Công tác",
      icon: "bi-briefcase",
      items: [
        { label: "Chức danh", value: profile?.adminTitle },
        { label: "Phòng / đơn vị", value: profile?.adminDeptName },
      ],
    },
    {
      title: "Đảng",
      icon: "bi-star",
      items: [{ label: "Chi bộ", value: profile?.partyBranchName, wide: true }],
    },
  ];

  return (
    <div className="page-wrapper">
      <PageHeader title="Tài khoản" />
      <div className="page-body">
      <div className={styles.layout}>
        <nav className={styles.nav} aria-label="Mục tài khoản">
          {SECTIONS.map((s) => (
            <button
              key={s.key}
              type="button"
              className={section === s.key ? styles.on : undefined}
              aria-current={section === s.key ? "page" : undefined}
              onClick={() => choose(s.key)}
            >
              <i className={`bi ${s.icon}`} />
              {s.label}
            </button>
          ))}
        </nav>

        <div className={styles.panel}>
          {section === "profile" && (
            <section>
              <div className={styles.who}>
                <span className="initials" style={{ width: 56, height: 56, fontSize: 18 }}>{initialsOf(user.fullName)}</span>
                <div className={styles.whoText}>
                  <div className={styles.name}>{user.fullName}</div>
                  <div className="text-secondary">{user.userName}</div>
                </div>
              </div>
              <InfoSections sections={sections} className={styles.info} />
            </section>
          )}

          {section === "roles" && (
            <section className={styles.block}>
              <h2 className={styles.h}>Vai trò</h2>
              <div className="d-flex flex-wrap gap-2">
                {user.roles.length === 0
                  ? <span className="text-secondary">Chưa được phân công vai trò.</span>
                  : user.roles.map((r) => <span key={r} className="tag t-info">{r}</span>)}
              </div>
              {scopes.length > 0 && (
                <>
                  <h2 className={styles.h}>Phạm vi</h2>
                  <div className="d-flex flex-wrap gap-2">{scopes.map((s) => <span key={s} className="tag">{s}</span>)}</div>
                </>
              )}
            </section>
          )}

          {section === "security" && (
            <section className={styles.block}>
              <h2 className={styles.h}>Đổi mật khẩu</h2>
              <ChangePasswordForm className={styles.form} />
            </section>
          )}
        </div>
      </div>
      </div>
    </div>
  );
}
