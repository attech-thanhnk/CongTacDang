"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { evaluationService, EvaluationRecordDto, WorkQueueDto } from "@/services/evaluationService";
import { roleService } from "@/services/roleService";
import { userService } from "@/services/userService";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { MyTasksSection } from "@/components/dashboard/MyTasksSection";
import { MyRecordSection } from "@/components/dashboard/MyRecordSection";
import { ScopeProgressSection } from "@/components/dashboard/ScopeProgressSection";
import { AdminSection, AdminShortcut } from "@/components/dashboard/AdminSection";

/** Tổng quan theo quyền; việc chờ tôi lấy ở mọi kỳ đang mở, phần còn lại theo kỳ đang chọn. */
export default function DashboardPage() {
  const { user, hasPermission } = useAuth();
  const { periods, periodId, loading: periodsLoading } = usePeriod();
  const [queue, setQueue] = useState<WorkQueueDto | null>(null);
  const [queueLoading, setQueueLoading] = useState(true);
  const [myRecord, setMyRecord] = useState<EvaluationRecordDto | null>(null);
  const [records, setRecords] = useState<EvaluationRecordDto[]>([]);
  const [adminItems, setAdminItems] = useState<AdminShortcut[]>([]);
  const [loading, setLoading] = useState(true);

  const canSelf = hasPermission("evaluation.self");
  const canReadRecords = hasPermission("evaluation.read");
  const canReadUsers = hasPermission("system.users.read");
  const canManageRoles = hasPermission("system.roles.manage");
  const canAssign = hasPermission("system.assignments.manage");
  const canImport = hasPermission("system.import");
  // Cùng điều kiện với mục menu "Việc cần xử lý": có thao tác nghiệp vụ đánh giá nào đó.
  const hasTasks = (user?.permissions ?? []).some((code) => code.startsWith("evaluation.") && code !== "evaluation.read");

  // Lối tắt quản trị chỉ cho người quản trị hệ thống (vai trò, phân công, nhập dữ liệu); người chỉ xem tài khoản không cần.
  useEffect(() => {
    if (!user || !(canManageRoles || canAssign || canImport)) {
      setAdminItems([]);
      return;
    }
    let mounted = true;
    Promise.all([
      canReadUsers ? userService.search({ pageSize: 1 }).then((r) => r.totalCount).catch(() => null) : Promise.resolve(null),
      canManageRoles ? roleService.listRoles().then((r) => r.length).catch(() => null) : Promise.resolve(null),
    ]).then(([accounts, roles]) => {
      if (!mounted) return;
      const items: (AdminShortcut | false)[] = [
        canReadUsers && { href: "/admin/users", label: "Tài khoản", icon: "bi-people", count: accounts },
        canManageRoles && { href: "/admin/roles", label: "Vai trò", icon: "bi-shield-lock", count: roles },
        canAssign && { href: "/admin/performers", label: "Người thực hiện", icon: "bi-person-check", count: null },
        canImport && { href: "/admin/imports", label: "Nhập dữ liệu", icon: "bi-file-earmark-arrow-up", count: null },
      ];
      setAdminItems(items.filter((i): i is AdminShortcut => !!i));
    });
    return () => {
      mounted = false;
    };
  }, [user, canReadUsers, canManageRoles, canAssign, canImport]);

  // Việc cần xử lý: mọi kỳ đang mở (không lọc theo kỳ đang chọn để không giấu việc).
  useEffect(() => {
    if (!user) return;
    let mounted = true;
    setQueueLoading(true);
    evaluationService
      .getWorkQueue()
      .catch(() => null)
      .then((q) => mounted && setQueue(q))
      .finally(() => mounted && setQueueLoading(false));
    return () => {
      mounted = false;
    };
  }, [user]);

  useEffect(() => {
    if (!user) return;
    if (!periodId) {
      setMyRecord(null);
      setRecords([]);
      setLoading(periodsLoading);
      return;
    }
    let mounted = true;
    setLoading(true);
    Promise.all([
      canSelf ? evaluationService.getMyRecord(periodId).catch(() => null) : Promise.resolve(null),
      canReadRecords ? evaluationService.getRecordsByPeriod(periodId).catch(() => []) : Promise.resolve([]),
    ])
      .then(([mine, list]) => {
        if (!mounted) return;
        setMyRecord(mine);
        setRecords(list);
      })
      .finally(() => mounted && setLoading(false));
    return () => {
      mounted = false;
    };
  }, [periodId, periodsLoading, user, canSelf, canReadRecords]);

  const noPeriod = !periodsLoading && periods.length === 0;
  // Thẻ hồ sơ của tôi luôn ở đầu trang, kể cả khi hồ sơ đang chờ chính tôi (việc cần xử lý ở dưới).
  const showMine = canSelf && !loading && !!myRecord;

  return (
    <div className="page-wrapper">
      <PageHeader title="Tổng quan" />

      <div className="page-body">
        {noPeriod ? (
          <EmptyState card
            icon="bi-calendar-x"
            title="Chưa có kỳ đánh giá"
            action={
              hasPermission("period.manage") ? (
                <Link href="/periods" className="btn btn-primary">
                  <i className="bi bi-plus-lg me-1" />
                  Tạo kỳ đánh giá
                </Link>
              ) : undefined
            }
          />
        ) : (
          <>
            {showMine && <MyRecordSection record={myRecord!} />}
            {hasTasks && <MyTasksSection queue={queue} loading={queueLoading} periodId={periodId} excludeRecordId={showMine ? myRecord!.id : undefined} />}
            {canReadRecords && <ScopeProgressSection records={records} loading={loading} />}
          </>
        )}
        <AdminSection items={adminItems} />
      </div>
    </div>
  );
}
