"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { evaluationService, EvaluationRecordDto, WorkQueueDto } from "@/services/evaluationService";
import { organizationService } from "@/services/organizationService";
import { userService } from "@/services/userService";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { MyTasksSection } from "@/components/dashboard/MyTasksSection";
import { MyRecordSection } from "@/components/dashboard/MyRecordSection";
import { ScopeProgressSection } from "@/components/dashboard/ScopeProgressSection";
import { AdminSection, AdminStats } from "@/components/dashboard/AdminSection";
import { isWorkflowStep } from "@/components/dashboard/taskUtils";

/** Tổng quan theo quyền; việc chờ tôi lấy ở mọi kỳ đang mở, phần còn lại theo kỳ đang chọn. */
export default function DashboardPage() {
  const { user, hasPermission } = useAuth();
  const { periods, periodId, loading: periodsLoading } = usePeriod();
  const [queue, setQueue] = useState<WorkQueueDto | null>(null);
  const [queueLoading, setQueueLoading] = useState(true);
  const [myRecord, setMyRecord] = useState<EvaluationRecordDto | null>(null);
  const [records, setRecords] = useState<EvaluationRecordDto[]>([]);
  const [adminStats, setAdminStats] = useState<AdminStats | null>(null);
  const [loading, setLoading] = useState(true);

  const canSelf = hasPermission("evaluation.self");
  const canReadRecords = hasPermission("evaluation.read");
  const canManageUsers = hasPermission("system.users.manage") || hasPermission("catalog.manage");
  const canReadUsers = hasPermission("system.users.read");
  // Cùng điều kiện với mục menu "Việc cần xử lý": có thao tác nghiệp vụ đánh giá nào đó.
  const hasTasks = (user?.permissions ?? []).some((code) => code.startsWith("evaluation.") && code !== "evaluation.read");

  useEffect(() => {
    if (!user || !canManageUsers) {
      setAdminStats(null);
      return;
    }
    let mounted = true;
    Promise.all([
      canReadUsers ? userService.search({ pageSize: 1 }).then((r) => r.totalCount).catch(() => null) : Promise.resolve(null),
      organizationService.getDepartments().then((r) => r.length).catch(() => 0),
      organizationService.getBranches().then((r) => r.length).catch(() => 0),
    ]).then(([accounts, departments, cells]) => {
      if (mounted) setAdminStats({ accounts, departments, cells });
    });
    return () => {
      mounted = false;
    };
  }, [user, canManageUsers, canReadUsers]);

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
  // Hồ sơ của tôi đang chờ tôi ở một bước luồng thì không hiện thêm khối riêng; việc sau công bố (kế hoạch) vẫn cần thấy kết quả.
  const mineInQueue =
    !!myRecord && !!queue?.groups.some((g) => isWorkflowStep(g.step) && g.items.some((i) => i.recordId === myRecord.id));
  const showMine = canSelf && !loading && !queueLoading && !!myRecord && !mineInQueue;

  return (
    <div className="page-wrapper">
      <PageHeader title="Tổng quan" />

      <div className="page-body">
        {noPeriod ? (
          <EmptyState card
            icon="bi-calendar-x"
            title="Chưa có kỳ đánh giá"
            description="Khi có kỳ đánh giá, tiến độ và việc cần xử lý sẽ hiện ở đây."
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
            {hasTasks && <MyTasksSection queue={queue} loading={queueLoading} periodId={periodId} />}
            {canReadRecords && <ScopeProgressSection records={records} loading={loading} />}
          </>
        )}
        {adminStats && <AdminSection stats={adminStats} />}
      </div>
    </div>
  );
}
