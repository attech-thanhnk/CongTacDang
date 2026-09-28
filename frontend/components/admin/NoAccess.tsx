"use client";

import React from "react";
import { Card, CardBody, PageHeader } from "@/components/common";

/** Màn hình khi người dùng không có quyền vào trang quản trị. */
export function NoAccess({ title, permissionName }: { title: string; permissionName: string }) {
  return (
    <div className="page-wrapper">
      <PageHeader title={title} />
      <div className="page-body">
        <Card>
          <CardBody className="text-center py-5">
            <i className="bi bi-shield-lock text-secondary" style={{ fontSize: 34 }} />
            <h2 className="h6 fw-bold mt-3">Không có quyền truy cập</h2>
            <p className="text-secondary small mb-0">
              Bạn chưa được cấp quyền &quot;{permissionName}&quot;. Hãy liên hệ quản trị hệ thống nếu cần.
            </p>
          </CardBody>
        </Card>
      </div>
    </div>
  );
}

export default NoAccess;
