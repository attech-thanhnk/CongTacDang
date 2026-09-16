# HƯỚNG DẪN CÀI ĐẶT & VẬN HÀNH HỆ THỐNG CÔNG TÁC ĐẢNG ATTECH
**Căn cứ pháp lý:** Hướng dẫn số **03-HD/TVĐU ngày 10/9/2026** của Ban Thường vụ Đảng ủy Tổng công ty Quản lý bay Việt Nam (VATM).  
**Đơn vị áp dụng:** Đảng ủy Công ty TNHH Kỹ thuật Quản lý bay (**ATTECH**).  
**Quy mô áp dụng:** ~68 Cán bộ quản lý; 4 Chi bộ trực thuộc; 4 Phòng/Xưởng nghiệp vụ.

---

## I. TỔNG QUAN KIẾN TRÚC HỆ THỐNG
Hệ thống được phát triển chuyên biệt cho công tác Đảng và đánh giá cán bộ quản lý Công ty ATTECH theo mô hình On-Premise trong mạng LAN nội bộ, chuẩn hóa và số hóa trọn vẹn quy trình 5 bước và **16 Biểu mẫu (Mẫu 01 -> 16)**:

```
[Bước 1: Đăng ký & Giao việc (Mẫu 01, 04, 05)] 
                        │
[Bước 2: Tự chấm điểm & Đánh giá (Mẫu 02, 09A, 09B, 09C, 09D)] 
                        │
[Bước 3: Hội nghị Chi bộ & Bỏ phiếu kín (Mẫu 06, 07, 08, 11 -> Mẫu 12, 13)] 
                        │
[Bước 4: Tổ Thẩm định Đối soát & Áp trần 20% Xuất sắc (Mẫu 03, Mẫu 10)] 
                        │
[Bước 5: Ban Thường vụ Phê duyệt & Kết xuất Báo cáo (Mẫu 14, 15A, 15B, 16)]
```

---

## II. HỆ SINH THÁI 16 BIỂU MẪU ĐÃ SỐ HÓA TRONG SOURCE CODE

| Nhóm nghiệp vụ | Mã Biểu mẫu | Tên gọi chuẩn trong Hướng dẫn 03-HD/TVĐU | Đường dẫn trên Web & Chức năng |
|---|---|---|---|
| **Nhóm I: Giao việc & Tự chấm** | **Mẫu 01** | Phiếu giao / Đăng ký sản phẩm, công việc chuyên môn hàng quý | `/mau-01`: Đăng ký 3-7 việc, tự động kiểm tra chốt đúng 70đ việc. Tích hợp nút đính kèm minh chứng. |
| | **Mẫu 02** | Phiếu tự đánh giá kết quả thực hiện sản phẩm, công việc hàng quý | `/mau-02`: Tự động tính điểm tiêu chí A-B-C-D theo tỷ trọng % khối KT. Kiểm tra điều kiện >= 30% vượt chuẩn. |
| | **Mẫu 09A** | Phiếu tự chấm điểm cán bộ giữ chức vụ lãnh đạo, quản lý | Đã tích hợp cấu trúc 30đ chung + 70đ việc chuyên môn vào Mẫu 02. |
| | **Mẫu 09B** | Phiếu tự chấm điểm áp dụng riêng cho kỳ chuyển tiếp Q3/2026 | Tự chấm theo 6 trục công việc cụ thể hóa. |
| | **Mẫu 09C, 09D** | Bản tự đánh giá & Phụ lục kê khai kết quả thực hiện nhiệm vụ | Bảng kê chi tiết việc hoàn thành và minh chứng dẫn chiếu. |
| **Nhóm II: Điều chỉnh & Đột xuất** | **Mẫu 03** | Phiếu ý kiến chuyên môn độc lập / Đối soát dữ liệu chuyên ngành | Nằm trong module Thẩm định chuyên môn. |
| | **Mẫu 04** | Phiếu đề nghị điều chỉnh, chuyển tiếp, loại trừ / ngoại lệ sản phẩm | Tích hợp Modal Mẫu 04 tại `/mau-01`: Trình phê duyệt sự cố bất khả kháng. |
| | **Mẫu 05** | Phiếu ghi nhận nhiệm vụ bổ sung, khẩn cấp, đột xuất trong kỳ | Tích hợp Modal Mẫu 05 tại `/mau-01`: Tự cân đối bảo toàn tổng 70đ. |
| **Nhóm III: Tập thể & Bỏ phiếu** | **Mẫu 06, 07, 08** | Báo cáo kết quả tập thể Chi ủy, Chi bộ & Đơn vị trực thuộc | Tổng hợp kết quả thực hiện nhiệm vụ chính trị để liên kết trách nhiệm. |
| | **Mẫu 11** | Phiếu đánh giá, xếp loại cán bộ quý (Bỏ phiếu kín) | `/bo-phieu`: Bỏ phiếu kín điện tử nặc danh tuyệt đối không lưu User ID. |
| | **Mẫu 12** | Biên bản Hội nghị đánh giá, xếp loại chất lượng cán bộ quý | `/bo-phieu`: Tự động lập biên bản số lượng triệu tập, có mặt, vắng mặt. |
| | **Mẫu 13** | Biên bản kiểm phiếu đánh giá, xếp loại chất lượng cán bộ quý | `/bo-phieu`: Tự động kiểm phiếu ngay khi kết thúc phiên bầu. |
| **Nhóm IV: Thẩm định & Báo cáo** | **Mẫu 10** | Phiếu thẩm định, nhận xét, đề xuất xếp loại và ghi nhận giải trình | `/tham-dinh`: Tự phát hiện chênh lệch >= 5đ và ghi nhận ý kiến giải trình. |
| | **Mẫu 14** | Danh sách đánh giá và đề xuất xếp loại quý đối với cán bộ | Tải trực tiếp file Excel `.xlsx` chuẩn mẫu qua API ClosedXML. |
| | **Mẫu 15A, 15B** | Tổng hợp kết quả đánh giá xếp loại cán bộ phê duyệt các cấp | `/tham-dinh`: Tự động tính Cột 10 (% Xuất sắc) và cảnh báo đỏ trần 20%. |
| | **Mẫu 16** | Báo cáo về kết quả đánh giá, xếp loại cán bộ quý gửi cấp trên | Báo cáo tổng kết toàn Đảng bộ ATTECH gửi Đảng ủy Tổng công ty. |

---

## III. TÍNH NĂNG UPLOAD FILE MINH CHỨNG
1. **Mục đích:** Cung cấp minh chứng cho tiêu chuẩn vượt chuẩn (tiến độ, chất lượng, sáng kiến) để xét duyệt danh hiệu "Hoàn thành xuất sắc nhiệm vụ" (yêu cầu >= 30% số việc có minh chứng vượt chuẩn).
2. **Cơ chế lưu trữ:**
   - Lưu trữ an toàn trong mạng LAN nội bộ tại thư mục `backend/storage/attachments/` hoặc qua **MinIO S3 Container**.
   - Chống tấn công Path Traversal, tự động đổi tên tệp theo GUID và băm định danh.
3. **Định dạng & Dung lượng:**
   - Hỗ trợ đa định dạng: PDF (`.pdf`), Microsoft Word (`.docx`, `.doc`), Microsoft Excel (`.xlsx`, `.xls`), hình ảnh minh chứng (`.png`, `.jpg`, `.jpeg`).
   - Giới hạn dung lượng: Tối đa 25MB cho mỗi tệp đính kèm.
4. **API Backend:**
   - `POST /api/Attachment/upload`: Tải lên tệp gắn kèm `TaskId`, `RecordId`, `FormCode` (M01, M02, M04, M10).
   - `GET /api/Attachment/by-task/{taskId}`: Lấy danh sách tệp của nhiệm vụ.
   - `GET /api/Attachment/download/{id}`: Tải xuống tệp minh chứng.
   - `DELETE /api/Attachment/{id}`: Xóa tệp minh chứng.
5. **Giao diện Frontend:**
   - Component `EvidenceUploadModal.tsx` kéo - thả mượt mà.
   - Nút `📎 Minh chứng (x)` hiển thị trực tiếp trên từng dòng nhiệm vụ của Mẫu 01, Mẫu 02, Mẫu 04 và Mẫu 10.

---

## IV. TÍNH NĂNG KẾT XUẤT BÁO CÁO (EXPORT EXCEL & IN ẤN A4)
1. **Kết xuất Excel bằng thư viện mã nguồn mở ClosedXML (Hoàn toàn miễn phí, không phát sinh chi phí bản quyền):**
   - **Mẫu 14 (`/api/ExportReport/mau-14`):** Danh sách đánh giá và đề xuất xếp loại quý chuẩn phông chữ, quốc hiệu, tiêu ngữ Đảng ủy VATM & ATTECH.
   - **Mẫu 15 (`/api/ExportReport/mau-15`):** Bảng tổng hợp tỷ lệ cán bộ hoàn thành xuất sắc nhiệm vụ, kiểm soát chặt chẽ trần 20% cho 4 Chi bộ.
   - **Mẫu 13 (`/api/Voting/export-minutes/{sessionId}`):** Biên bản kiểm phiếu tự động theo thời gian thực.
2. **In ấn chuẩn văn bản hành chính A4:**
   - Toàn bộ 16 Biểu mẫu tại Trung tâm Biểu mẫu (`/bieu-mau`) và trang Biên bản kiểm phiếu (`/bo-phieu`) hỗ trợ xem trước (Print Preview) và in ấn trực tiếp chuẩn khổ giấy A4 (`paper-sheet`).

---

## V. CƠ CHẾ PHÂN QUYỀN (RBAC) 5 VAI TRÒ
Theo Hướng dẫn 03-HD/TVĐU, hệ thống phân chia 5 vai trò độc lập bảo đảm tính khách quan:

| Mã Vai trò | Tên Vai trò | Trách nhiệm & Quyền hạn |
|---|---|---|
| `CAN_BO` | **Cán bộ Lãnh đạo / Quản lý** | Nhập Mẫu 01 (Giao việc), Mẫu 02 (Tự chấm); bỏ phiếu kín Mẫu 11 tại Chi bộ; đính kèm file minh chứng. |
| `BI_THU_CHI_BO` | **Bí thư / Cấp ủy Chi bộ** | Xem toàn bộ hồ sơ Mẫu 01, 02 của Chi bộ; chủ trì hội nghị bỏ phiếu kín Mẫu 11; ký Biên bản kiểm phiếu Mẫu 12, 13; đôn đốc tiến độ. |
| `TO_THAM_DINH` | **Tổ Thẩm định Đảng ủy** | Thẩm định điểm Mẫu 03; lập Phiếu Mẫu 10 khi chênh lệch >= 5.0 điểm; đối soát trần 20% Mẫu 15; đề xuất mức xếp loại. |
| `BAN_THUONG_VU` | **Ban Thường vụ Đảng ủy** | Xem xét toàn bộ hồ sơ & biên bản kiểm phiếu; chuẩn y kết quả xếp loại chính thức Mẫu 14; ký phát hành Báo cáo Mẫu 16 gửi Đảng ủy Tổng công ty. |
| `ADMIN_HE_THONG` | **Quản trị viên Hệ thống** | Quản trị 68 hồ sơ cán bộ 2 vai; quản lý 4 Chi bộ & 4 Phòng/Xưởng; cấu hình thang điểm & trần tỷ lệ; phân quyền người dùng. |

*Lưu ý:* Header hệ thống tích hợp sẵn **Role Switcher (Góc nhìn vai trò)** giúp người dùng giả lập và trải nghiệm đầy đủ giao diện dưới góc nhìn của từng vai trò.

---

## VI. MÀN HÌNH QUẢN TRỊ & NHÂN SỰ 2 VAI (`/quan-tri`)
- **Quản trị 68 Cán bộ Quản lý ATTECH:**
  - Đồng bộ 2 vai: Vai 1 (Đảng viên, Chi bộ, Chức vụ Đảng, Số thẻ Đảng) và Vai 2 (Chính quyền, Phòng ban/Xưởng, Chức danh quản lý, Khung công việc, Cấp thẩm quyền duyệt: ATTECH hay Tổng công ty).
  - Tìm kiếm nhanh, lọc theo từng Chi bộ (Kỹ thuật, Sản xuất, Dịch vụ, Cơ quan Văn phòng).
- **Quản lý Cơ cấu Tổ chức:**
  - 4 Chi bộ trực thuộc: Khối Kỹ thuật (18 đ/c), Khối Sản xuất (16 đ/c), Khối Dịch vụ (14 đ/c), Cơ quan Văn phòng (20 đ/c).
  - 4 Đơn vị chuyên môn: Phòng Kế hoạch KD, Xưởng Sản xuất thiết bị, Trung tâm Dịch vụ Kỹ thuật, Phòng TCCB - LĐ.
- **Ma trận Phân quyền:**
  - Kiểm soát chi tiết quyền hạn của từng nhóm vai trò trên từng biểu mẫu và nút chức năng.

---

## VII. HƯỚNG DẪN CÀI ĐẶT & TRIỂN KHAI

### TÙY CHỌN 1: Triển khai Containerized Modular Services (Khuyến nghị cho Môi trường Sản xuất)
Mô hình cô lập tài nguyên: Tách riêng **Export Service (Giới hạn 1GB RAM)**, **MinIO S3 (Lưu trữ tệp)**, **Auth Service**, **Core API**, và **Nginx API Gateway**.

```bash
cd docker

# Cấp quyền thực thi và chạy kịch bản tự động:
chmod +x deploy-modular.sh
./deploy-modular.sh

# Hoặc khởi động trực tiếp bằng Docker Compose:
docker compose -f docker-compose.modular.yml up -d --build
```

**Các cổng dịch vụ sau khi khởi động:**
- **Cổng vào duy nhất (Nginx Gateway):** `http://<IP_MAY_CHU>:80`
  - Giao diện Web: `http://<IP_MAY_CHU>/`
  - Core API: `http://<IP_MAY_CHU>/api/`
  - Auth & User API: `http://<IP_MAY_CHU>/api/auth/`
  - Export Report API: `http://<IP_MAY_CHU>/api/export/`
- **MinIO S3 Web Console:** `http://<IP_MAY_CHU>:9001` (Tài khoản: `minio_admin` / `MinioAttech2026!Key`)
- **PostgreSQL Database:** Cổng nội bộ `5432`, Database: `congtacdang_db`.

---

### TÙY CHỌN 2: Triển khai Tiêu chuẩn All-in-One (Gọn nhẹ cho Máy trạm / Thử nghiệm)
Mô hình gộp: 1 Web Frontend + 1 Backend API + 1 PostgreSQL + pgAdmin:

```bash
cd docker
chmod +x deploy-ubuntu.sh
./deploy-ubuntu.sh
```

**Các cổng dịch vụ:**
- Frontend Web: `http://<IP_MAY_CHU>:3001`
- Backend Swagger API: `http://<IP_MAY_CHU>:5000/swagger`
- pgAdmin Web: `http://<IP_MAY_CHU>:5050` (`admin@attech.com.vn` / `AttechAdmin2026!`)
- PostgreSQL: Cổng `5434`.
