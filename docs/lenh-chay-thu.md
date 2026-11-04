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
| Kiểm soát viên | `da.dc` |
| Trưởng TT Bảo đảm kỹ thuật (duyệt, đề xuất cho Đội trưởng) | `tuan.nm` |
| Đội trưởng, Bí thư chi bộ | `cuong.nt` |
| Phó Bí thư chi bộ (xác nhận cho `cuong.nt`) | `hung.nx` |
| Phó phòng KT-AT-CL (Chưa xếp loại) | `nghia.tv` |

## Khởi động

```
dotnet run --project backend/src/CongTacDang.Api --urls http://localhost:5000
cd frontend && npm run dev
```

## Tạo dữ liệu thử tới một giai đoạn

```
powershell -File scripts/tao-du-lieu-thu.ps1 -GiaiDoan tham-dinh
```

Tạo kỳ Quý IV/2026 với 7 hồ sơ, đủ các trường hợp:

| Người | Trường hợp (diện quyết định) | Luồng |
|---|---|---|
| Lê Mai Duy (`duy.lm`) | Trưởng phòng KH-KD — Đảng ủy cơ sở | Giám đốc duyệt danh mục, tập thể phòng đề xuất, thẩm định, Giám đốc nhận xét, Mẫu 20, Đảng ủy quyết định |
| Nguyễn Thị Anh Đào (`dao.nta`), Nguyễn Bình Trung (`trung.nb`) | Phó phòng KH-KD — Đảng ủy cơ sở | Như trên, Trưởng phòng duyệt danh mục |
| Nguyễn Tuấn Cường (`cuong.nt`) | Đội trưởng, Bí thư chi bộ là nhân viên | Trưởng trung tâm (`tuan.nm`) duyệt và đề xuất mức, thẩm định, Đảng ủy quyết định |
| Nguyễn Như Thành (`thanh.nn`) | Phó Giám đốc | Chủ tịch duyệt, tập thể Công ty đề xuất, Chủ tịch nhận xét, cấp trên quyết định (Văn phòng ghi nhận) |
| Đỗ Công Đà (`da.dc`) | Kiểm soát viên — BTV Đảng ủy Tổng công ty | Cấp trên duyệt, thẩm định, nhận xét, quyết định (Văn phòng ghi nhận); tập thể Công ty đề xuất |
| Trịnh Văn Nghĩa (`nghia.tv`) | Chưa xếp loại | Đánh dấu ngay khi mở kỳ, chỉ còn chờ công bố |

Cuối lệnh in ra ai đăng nhập làm tiếp cho từng hồ sơ.

| `-GiaiDoan` | Hồ sơ dừng ở | Ai làm tiếp |
|---|---|---|
| `dang-ky` | Chờ đăng ký sản phẩm | Chính chủ hồ sơ |
| `tu-cham` | Chờ tự chấm | Chính chủ hồ sơ |
| `tap-the` | Chờ tập thể đề xuất (Đội trưởng: chờ Trưởng trung tâm đề xuất) | Thư ký: `dao.nta`, `long.nc`; `tuan.nm` |
| `tham-dinh` | Chờ thẩm định (Phó Giám đốc: chờ Chủ tịch nhận xét; Kiểm soát viên: chờ ghi nhận thẩm định cấp trên) | `loan.ttp`; `thinh.lt`; `long.nc` |
| `nhan-xet` | Chờ cấp trực tiếp sử dụng nhận xét | `giang.nh`; `thinh.lt` |
| `quyet-dinh` | Chờ quyết định (đã chốt Mẫu 20) | `long.nc` |
| `cong-bo` | Chờ công bố | `long.nc` |

Đã có kỳ thử thì lệnh đẩy tiếp các hồ sơ đang có tới giai đoạn chọn (phần đã làm tay được giữ):

```
powershell -File scripts/tao-du-lieu-thu.ps1 -GiaiDoan cong-bo
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
