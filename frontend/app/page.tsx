"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { evaluationService, EvaluationRecordDto, WorkQueueDto } from "@/services/evaluationService";
import { AdminSummaryDto, dashboardService, DashboardOverviewDto } from "@/services/dashboardService";
import { auditService, AuditLogDto } from "@/services/auditService";
import { useAuth } from "@/contexts/AuthContext";
import { usePeriod } from "@/contexts/PeriodContext";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { MyTasksSection } from "@/components/dashboard/MyTasksSection";
import { MyRecordSection } from "@/components/dashboard/MyRecordSection";
import { PeriodTimeline } from "@/components/dashboard/PeriodTimeline";
import { ScopeProgressSection } from "@/components/dashboard/ScopeProgressSection";
import { AdminSection } from "@/components/dashboard/AdminSection";

/** Số dòng nhật ký ở khối Quản trị. */
const RECENT_LOGS = 5;

/** Tổng quan theo quyền; việc chờ tôi lấy ở mọi kỳ đang mở, phần còn lại theo kỳ đang chọn. */
export default function DashboardPage() {
  const { user, hasPermission, hasPermissionIn } = useAuth();
  const { periods, periodId, loading: periodsLoading } = usePeriod();
  const [queue, setQueue] = useState<WorkQueueDto | null>(null);
  const [queueLoading, setQueueLoading] = useState(true);
  const [myRecord, setMyRecord] = useState<EvaluationRecordDto | null>(null);
  const [overview, setOverview] = useState<DashboardOverviewDto | null>(null);
  const [adminSummary, setAdminSummary] = useState<AdminSummaryDto | null>(null);
  const [logs, setLogs] = useState<AuditLogDto[] | null>(null);
  const [loading, setLoading] = useState(true);

  const permissions = user?.permissions ?? [];
  const canSelf = hasPermission("evaluation.self");
  // Cùng điều kiện với mục menu "Việc cần xử lý": có thao tác nghiệp vụ đánh giá nào đó.
  const hasTasks = permissions.some((code) => code.startsWith("evaluation.") && code !== "evaluation.read");
  // Chỉ người xử lý được mới thấy: quản lý tài khoản, hoặc quản lý kỳ toàn công ty (bước chưa có người thực hiện).
  const canAdminSummary = hasPermission("system.users.manage") || hasPermissionIn("period.manage", "Global");
  const canAudit = hasPermission("system.audit.read");

  // Khối Quản trị không phụ thuộc kỳ đang chọn.
  useEffect(() => {
    if (!user) return;
    let mounted = true;
    Promise.all([
      canAdminSummary ? dashboardService.getAdmin().catch(() => null) : Promise.resolve(null),
      canAudit ? auditService.getLogs({ pageSize: RECENT_LOGS, excludeEntityType: "RefreshToken" }).then((p) => p.items).catch(() => null) : Promise.resolve(null),
    ]).then(([summary, recent]) => {
      if (!mounted) return;
      setAdminSummary(summary);
      setLogs(recent);
    });
    return () => {
      mounted = false;
    };
  }, [user, canAdminSummary, canAudit]);

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
      setOverview(null);
      setLoading(periodsLoading);
      return;
    }
    let mounted = true;
    setLoading(true);
    Promise.all([
      canSelf ? evaluationService.getMyRecord(periodId).catch(() => null) : Promise.resolve(null),
      dashboardService.getOverview(periodId).catch(() => null),
    ])
      .then(([mine, data]) => {
        if (!mounted) return;
        setMyRecord(mine);
        setOverview(data);
      })
      .finally(() => mounted && setLoading(false));
    return () => {
      mounted = false;
    };
  }, [periodId, periodsLoading, user, canSelf]);

  const noPeriod = !periodsLoading && periods.length === 0;
  // Thẻ hồ sơ của tôi luôn ở đầu trang, kể cả khi hồ sơ đang chờ chính tôi (việc cần xử lý ở dưới).
  const showMine = canSelf && !loading && !!myRecord;
  const scope = !loading ? overview?.scope : null;

  return (
    <div className="page-wrapper">
      <PageHeader title="Tổng quan" />

      <div className="page-body">
        {noPeriod ? (
          <EmptyState card
            icon="fa-calendar-xmark"
            title="Chưa có kỳ đánh giá"
            action={
              hasPermission("period.manage") ? (
                <Link href="/periods" className="btn btn-primary">
                  <i className="fa-solid fa-plus me-1" />
                  Tạo kỳ đánh giá
                </Link>
              ) : undefined
            }
          />
        ) : (
          <>
            {showMine && <MyRecordSection record={myRecord!} lastResult={overview?.lastResult} />}
            {hasTasks && <MyTasksSection queue={queue} loading={queueLoading} periodId={periodId} excludeRecordId={showMine ? myRecord!.id : undefined} />}
            {!loading && overview && <PeriodTimeline timeline={overview.timeline} today={overview.today} />}
            {scope && <ScopeProgressSection scope={scope} />}
          </>
        )}
        <AdminSection summary={adminSummary} logs={logs} />
      </div>
    </div>
  );
}
