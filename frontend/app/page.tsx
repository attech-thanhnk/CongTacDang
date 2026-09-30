"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import {
  evaluationService,
  EvaluationPeriodDto,
  EvaluationRecordDto,
  WorkQueueDto,
} from "@/services/evaluationService";
import { organizationService } from "@/services/organizationService";
import { userService } from "@/services/userService";
import { useAuth } from "@/contexts/AuthContext";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { MyTasksSection } from "@/components/dashboard/MyTasksSection";
import { MyRecordSection } from "@/components/dashboard/MyRecordSection";
import { ScopeProgressSection } from "@/components/dashboard/ScopeProgressSection";
import { AdminSection, AdminStats } from "@/components/dashboard/AdminSection";

/**
 * Tổng quan theo quyền, ưu tiên từ trên xuống: việc đang chờ tôi → hồ sơ của tôi (evaluation.self)
 * → tiến độ trong phạm vi (evaluation.read) → quản trị (system.users.manage).
 */
export default function DashboardPage() {
  const { user, hasPermission } = useAuth();
  const [periods, setPeriods] = useState<EvaluationPeriodDto[]>([]);
  const [periodsLoaded, setPeriodsLoaded] = useState(false);
  const [periodId, setPeriodId] = useState("");
  const [queue, setQueue] = useState<WorkQueueDto | null>(null);
  const [myRecord, setMyRecord] = useState<EvaluationRecordDto | null>(null);
  const [records, setRecords] = useState<EvaluationRecordDto[]>([]);
  const [adminStats, setAdminStats] = useState<AdminStats | null>(null);
  const [loading, setLoading] = useState(true);

  const canSelf = hasPermission("evaluation.self");
  const canReadRecords = hasPermission("evaluation.read");
  const canManageUsers = hasPermission("system.users.manage") || hasPermission("catalog.manage");
  const canReadUsers = hasPermission("system.users.read");

  useEffect(() => {
    evaluationService
      .getPeriods()
      .then((list) => {
        setPeriods(list);
        const active = list.find((p) => p.isActive) || list[0];
        if (active) setPeriodId(active.id);
        else setLoading(false);
      })
      .catch(() => setLoading(false))
      .finally(() => setPeriodsLoaded(true));
  }, []);

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

  useEffect(() => {
    if (!periodId || !user) return;
    let mounted = true;
    setLoading(true);
    Promise.all([
      evaluationService.getWorkQueue(periodId).catch(() => null),
      canSelf ? evaluationService.getMyRecord(periodId).catch(() => null) : Promise.resolve(null),
      canReadRecords ? evaluationService.getRecordsByPeriod(periodId).catch(() => []) : Promise.resolve([]),
    ])
      .then(([q, mine, list]) => {
        if (!mounted) return;
        setQueue(q);
        setMyRecord(mine);
        setRecords(list);
      })
      .finally(() => mounted && setLoading(false));
    return () => {
      mounted = false;
    };
  }, [periodId, user, canSelf, canReadRecords]);

  const period = periods.find((p) => p.id === periodId) || null;

  return (
    <div className="page-wrapper">
      <PageHeader
        title="Tổng quan"
        subTitle={period ? `${period.name} · ${period.statusDisplayName}` : undefined}
        actions={
          periods.length > 0 ? (
            <select
              className="form-select form-select-sm w-auto"
              aria-label="Kỳ đánh giá"
              value={periodId}
              onChange={(e) => setPeriodId(e.target.value)}
            >
              {periods.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name} ({p.statusDisplayName})
                </option>
              ))}
            </select>
          ) : undefined
        }
      />

      <div className="page-body d-flex flex-column gap-3">
        {periodsLoaded && periods.length === 0 ? (
          <EmptyState
            icon="bi-calendar-x"
            title="Chưa có kỳ đánh giá"
            action={
              hasPermission("period.manage") ? (
                <Link href="/periods" className="btn btn-primary btn-sm">
                  Tạo kỳ đánh giá
                </Link>
              ) : undefined
            }
          />
        ) : (
          <>
            <MyTasksSection queue={queue} loading={loading} />
            {canSelf && !loading && myRecord && <MyRecordSection record={myRecord} />}
            {canReadRecords && <ScopeProgressSection records={records} loading={loading} />}
          </>
        )}
        {adminStats && <AdminSection stats={adminStats} />}
      </div>
    </div>
  );
}
