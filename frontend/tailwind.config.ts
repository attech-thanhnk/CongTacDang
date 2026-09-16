import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./app/**/*.{js,ts,jsx,tsx,mdx}",
    "./components/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        dang: {
          crimson: "#881337", // Đỏ thắm cờ Đảng cao cấp
          red: "#991B1B",     // Đỏ tươi chính quy
          light: "#FFF1F2",   // Hồng nhạt nền
          border: "#FECDD3",
          gold: "#D97706",    // Vàng búa liềm sang trọng
          goldlight: "#FEF3C7",
        },
        attech: {
          navy: "#0F172A",    // Xanh đen hàng không
          slate: "#1E293B",
          blue: "#1D4ED8",    // Xanh kỹ thuật bay
          accent: "#0284C7",
          cyan: "#06B6D4",
        }
      },
      boxShadow: {
        'paper': '0 4px 20px -2px rgba(0, 0, 0, 0.08), 0 2px 6px -1px rgba(0, 0, 0, 0.04)',
        'paper-hover': '0 10px 30px -4px rgba(0, 0, 0, 0.12), 0 4px 10px -2px rgba(0, 0, 0, 0.06)',
        'gold-glow': '0 0 15px rgba(217, 119, 6, 0.3)',
      }
    },
  },
  plugins: [],
};
export default config;
