"use client";

import React from "react";
import { RowAction, RowActions } from "@/components/common/RowActions";
import styles from "./HeaderMenu.module.css";

/** Menu ⋯ trên header trang: thao tác ít dùng và nguy hiểm (nằm cuối, chữ đỏ). */
export function HeaderMenu({ actions, itemLabel, disabled }: { actions: RowAction[]; itemLabel?: string; disabled?: boolean }) {
  if (actions.every((a) => a.hidden)) return null;
  return (
    <span className={styles.wrap}>
      <RowActions menu={actions} itemLabel={itemLabel} disabled={disabled} />
    </span>
  );
}

export default HeaderMenu;
