"use client";

import React from "react";

export interface CardProps {
  children: React.ReactNode;
  className?: string;
  style?: React.CSSProperties;
}

export const Card: React.FC<CardProps> = ({ children, className = "", style }) => {
  return (
    <div
      className={`card shadow-sm border bg-white ${className}`}
      style={{ borderColor: "#e2e8f0", ...style }}
    >
      {children}
    </div>
  );
};

export interface CardHeaderProps {
  children: React.ReactNode;
  className?: string;
  actions?: React.ReactNode;
}

export const CardHeader: React.FC<CardHeaderProps> = ({ children, className = "", actions }) => {
  return (
    <div
      className={`card-header bg-white border-bottom py-2.5 px-3.5 d-flex justify-content-between align-items-center ${className}`}
      style={{ borderColor: "#e2e8f0" }}
    >
      <div className="d-flex align-items-center gap-2">{children}</div>
      {actions && <div className="d-flex align-items-center gap-2">{actions}</div>}
    </div>
  );
};

export interface CardBodyProps {
  children: React.ReactNode;
  className?: string;
  noPadding?: boolean;
}

export const CardBody: React.FC<CardBodyProps> = ({
  children,
  className = "",
  noPadding = false,
}) => {
  return <div className={`card-body ${noPadding ? "p-0" : "p-3.5"} ${className}`}>{children}</div>;
};

export interface CardFooterProps {
  children: React.ReactNode;
  className?: string;
}

export const CardFooter: React.FC<CardFooterProps> = ({ children, className = "" }) => {
  return (
    <div
      className={`card-footer bg-white border-top py-2.5 px-3.5 d-flex justify-content-between align-items-center ${className}`}
      style={{ borderColor: "#e2e8f0" }}
    >
      {children}
    </div>
  );
};

export default Card;
