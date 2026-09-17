"use client";

import React from "react";

export type ButtonVariant =
  | "primary"
  | "secondary"
  | "outline-primary"
  | "outline-secondary"
  | "outline-danger"
  | "danger"
  | "success";

export type ButtonSize = "sm" | "md" | "lg";

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  icon?: string;
  iconPosition?: "left" | "right";
  loading?: boolean;
  loadingText?: string;
  children?: React.ReactNode;
}

export const Button: React.FC<ButtonProps> = ({
  variant = "primary",
  size = "md",
  icon,
  iconPosition = "left",
  loading = false,
  loadingText,
  children,
  className = "",
  disabled,
  ...props
}) => {
  const getVariantClass = () => {
    switch (variant) {
      case "primary":
        return "btn-primary";
      case "secondary":
        return "btn-secondary";
      case "outline-primary":
        return "btn-outline-primary";
      case "outline-secondary":
        return "btn-outline-secondary";
      case "outline-danger":
        return "btn-outline-danger";
      case "danger":
        return "btn-danger";
      case "success":
        return "btn-success";
      default:
        return "btn-primary";
    }
  };

  const getSizeClass = () => {
    switch (size) {
      case "sm":
        return "btn-sm py-1 px-2.5";
      case "lg":
        return "btn-lg py-2 px-4";
      default:
        return "py-1.5 px-3.5";
    }
  };

  const isDisabled = disabled || loading;

  return (
    <button
      className={`btn ${getVariantClass()} ${getSizeClass()} ${className}`}
      disabled={isDisabled}
      {...props}
    >
      {loading ? (
        <>
          <span
            className="spinner-border spinner-border-sm"
            role="status"
            aria-hidden="true"
          ></span>
          <span>{loadingText || children || "Đang xử lý..."}</span>
        </>
      ) : (
        <>
          {icon && iconPosition === "left" && <i className={`bi ${icon}`}></i>}
          {children && <span>{children}</span>}
          {icon && iconPosition === "right" && <i className={`bi ${icon}`}></i>}
        </>
      )}
    </button>
  );
};

export default Button;
