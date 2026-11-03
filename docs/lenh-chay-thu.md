# Lệnh chạy thử trên máy dev

Chạy từ thư mục gốc repo (`D:\repos\CongTacDang`). Các bước làm trên giao diện: [Quy trình thao tác chuẩn](quy-trinh-thao-tac-chuan.md).

## Đăng nhập

- Giao diện: http://localhost:3001
- Mật khẩu mọi tài khoản trên CSDL dev: `Thu12345a`

| Vai trò | Tài khoản |
|---|---|
| Quản trị hệ thống | `quantri` |
| Cơ quan thẩm định (quản lý kỳ, thẩm định, lập Mẫu 20/19) | `loan.ttp`, `loan.ctp` |
| Văn phòng Đảng ủy (chốt Mẫu 20, quyết định, công bố) | `long.nc`, `hung.dt`, `thao.nt` |
| Chủ tịch (duyệt, nhận xét Phó Giám đốc) | `thinh.lt` |
| Giám đốc (duyệt, nhận xét Trưởng phòng) | `giang.nh` |
| Trưởng phòng KH-KD, Bí thư Chi bộ | `duy.lm` |
| Phó phòng KH-KD, Phó Bí thư, thư ký phòng | `dao.nta` |
| Phó phòng KH-KD | `trung.nb` |
| Phó Giám đốc | `thanh.nn` |

## Khởi động

```
dotnet run --project backend/src/CongTacDang.Api --urls http://localhost:5000
cd frontend && npm run dev
```

## Tạo dữ liệu thử tới một giai đoạn

```
powershell -File scripts/tao-du-lieu-thu.ps1 -GiaiDoan tham-dinh
```

Tạo kỳ Quý IV/2026 với 4 hồ sơ: Lê Mai Duy (Trưởng phòng KH-KD), Nguyễn Thị Anh Đào và Nguyễn Bình Trung (Phó phòng), Nguyễn Như Thành (Phó Giám đốc, để thử nhánh Phó Giám đốc). Cuối lệnh in ra ai đăng nhập làm tiếp.

| `-GiaiDoan` | Hồ sơ dừng ở | Ai làm tiếp |
|---|---|---|
| `dang-ky` | Chờ đăng ký sản phẩm | Chính chủ hồ sơ |
| `tu-cham` | Chờ tự chấm | Chính chủ hồ sơ |
| `tap-the` | Chờ tập thể đề xuất | Thư ký: `dao.nta`, `long.nc` |
| `tham-dinh` | Chờ thẩm định (Phó Giám đốc: chờ Chủ tịch nhận xét) | `loan.ttp`; `thinh.lt` |
| `nhan-xet` | Chờ cấp trực tiếp sử dụng nhận xét | `giang.nh`; `thinh.lt` |
| `quyet-dinh` | Chờ quyết định (đã chốt Mẫu 20) | `long.nc` |
| `cong-bo` | Chờ công bố | `long.nc` |

Đã có kỳ thử, muốn đẩy tiếp tới giai đoạn sau:

```
powershell -File scripts/tao-du-lieu-thu.ps1 -GiaiDoan cong-bo -TiepTuc
```

## Làm sạch để chạy lại từ đầu

```
powershell -File scripts/tao-du-lieu-thu.ps1 -LamSach
```

Xóa toàn bộ kỳ, hồ sơ, biên bản, phiếu, nhật ký trên CSDL dev. Giữ nguyên tài khoản, danh mục, vai trò, bộ tiêu chí. Tải lại trang trên trình duyệt sau khi chạy.

## Kiểm thử tự động

```
dotnet test backend/CongTacDang.slnx
powershell -File frontend/e2e/chay-e2e.ps1 -SourceDb congtacdang_dev
```

Kịch bản giao diện chạy trên CSDL thử riêng (tự xóa khi xong); cần tắt backend dev trước. Chi tiết: [Kiểm thử tự động](kiem-thu-tu-dong.md).
