# Thuật ngữ thống nhất

Một khái niệm chỉ có một tên gọi. Tên gốc lấy từ Hướng dẫn 03-HD/TVĐU và biểu mẫu gốc
(`docs/2.03-HD.TVDU … (Bieu mau).docx`, trích tại `docs/nghiep-vu/hd03-trich-xuat.md`); giao diện, tên vai trò/quyền, tên file xuất,
thông báo và tài liệu dùng đúng tên ở cột "Tên trên giao diện". Mã trong code **không đổi** khi đổi tên hiển thị.

## 1. Biểu mẫu

Tên biểu mẫu trên giao diện và file xuất có dạng "Mẫu NN — tên"; bảng này ghi phần tên sau dấu gạch.

| Mẫu | Tên trong biểu mẫu gốc (viết hoa trong bản gốc) | Tên trên giao diện | Mã trong code |
|---|---|---|---|
| 01 | Phiếu giao / đăng ký sản phẩm, công việc chuyên môn hằng quý | Phiếu giao / đăng ký sản phẩm, công việc chuyên môn hằng quý | `01`, `MAU_01` |
| 02 | Phiếu tự đánh giá kết quả thực hiện sản phẩm, công việc hằng quý | Phiếu tự đánh giá kết quả thực hiện sản phẩm, công việc hằng quý | `02`, `MAU_02` |
| 03 | Phiếu ý kiến chuyên môn độc lập / đối soát dữ liệu chuyên ngành | Phiếu ý kiến chuyên môn độc lập / đối soát dữ liệu chuyên ngành; gọi ngắn: **phiếu đối soát** | `03`, `MAU_03` |
| 04 | Phiếu đề nghị điều chỉnh, chuyển tiếp, loại trừ / ngoại lệ sản phẩm | giữ nguyên | `04`, `MAU_04` |
| 05 | Phiếu ghi nhận nhiệm vụ bổ sung, khẩn cấp, đột xuất trong kỳ | giữ nguyên | `05`, `MAU_05` |
| 06 | Báo cáo kết quả tập thể / lĩnh vực phục vụ liên kết trách nhiệm cán bộ lãnh đạo | giữ nguyên | `06`, `MAU_06` |
| 07 | Báo cáo tự đánh giá, xếp loại chất lượng của tập thể Đảng ủy (Chi ủy, Chi bộ) | giữ nguyên | `07`, `MAU_07` |
| 08 | Báo cáo tổng hợp kết quả thực hiện các nhiệm vụ của cơ quan, đơn vị | giữ nguyên | `08`, `MAU_08` |
| 09A | Phiếu tự chấm điểm đánh giá, xếp loại chất lượng hằng quý (đối với cá nhân giữ chức vụ lãnh đạo, quản lý) | Phiếu tự chấm điểm đánh giá, xếp loại chất lượng hằng quý; gọi ngắn: **phiếu tự chấm** | `09A`, `MAU_09A` |
| 09B | Cùng tên 09A, chỉ áp dụng Quý III/2026 thay 09A | Chưa có trong hệ thống (dùng 09A) | — |
| 09C | Bản tự đánh giá, xếp loại của cá nhân | giữ nguyên | `09C`, `MAU_09C` |
| 9D | Phụ lục kết quả thực hiện nhiệm vụ, công việc được giao trong quý | giữ nguyên | `9D`, `MAU_9D` |
| 10 | Phiếu thẩm định, nhận xét, đề xuất xếp loại và ghi nhận giải trình | giữ nguyên | `10`, `MAU_10` |
| 11 | Phiếu đánh giá, xếp loại cán bộ (phiếu kín) | giữ nguyên | `11`, `MAU_11` |
| 12 | Biên bản hội nghị đánh giá, xếp loại chất lượng cán bộ quý | giữ nguyên | `12`, `MAU_12` |
| 13 | Biên bản kiểm phiếu hội nghị đánh giá, xếp loại chất lượng cán bộ quý | giữ nguyên | `13`, `MAU_13` |
| 14 | Danh sách đánh giá và đề xuất xếp loại | giữ nguyên | `14` |
| 15A | Tổng hợp kết quả đánh giá, xếp loại cán bộ (đối tượng đề nghị cấp trên quyết định, mã chức danh M1–M16) | giữ nguyên | `15A` |
| 15B | Tổng hợp kết quả đánh giá, xếp loại cán bộ (đối tượng thuộc diện Đảng ủy/Chi ủy cơ sở quyết định, mã M17–M26) | giữ nguyên | `15B` |
| 16 | Báo cáo về kết quả đánh giá, xếp loại chất lượng cán bộ quý | giữ nguyên | `16`, `MAU_16` |
| 17 | Kế hoạch hỗ trợ, khắc phục và phát triển 30-60-90 ngày | giữ nguyên | `17`, `MAU_17` |
| 18 | Bảng kiểm hồ sơ, quyền truy cập và quản lý phiên bản | Không xuất thành file; ma trận quyền truy cập thể hiện bằng vai trò và quyền (mục 4, 5) | — |
| 19 | Phiếu kiểm soát tính hợp lệ, công bằng và nhất quán | Phiếu kiểm soát (gọi đầy đủ ở tiêu đề phiếu) | `19`, `MAU_19` |
| 20 | Biên bản hiệu chuẩn kết quả đánh giá | Biên bản hiệu chuẩn | `20`, `MAU_20` |
| — | Quyết định xếp loại (kèm Phụ lục kết quả) | Quyết định xếp loại cán bộ quý của Đảng ủy (Chi ủy), kèm Phụ lục kết quả | `MAU_QD` |

Ở "Tên trên giao diện", **phiếu** = các mẫu "Phiếu …"; **biên bản** = Mẫu 12, 13, 20; **báo cáo** = Mẫu 06, 07, 08, 16.

## 2. Quy trình 5 bước và trạng thái hồ sơ

Hồ sơ đánh giá đang ở trạng thái "Chờ …" của bước kế tiếp. Nguồn: Mục IV của Hướng dẫn.

| Bước (tài liệu gốc) | Tên trong tài liệu gốc | Tên trên giao diện (bước) | Trạng thái chờ bước đó | Mã bước / trạng thái |
|---|---|---|---|---|
| 1 | Giao, nhận và phê duyệt sản phẩm/nhiệm vụ chuyên môn đầu kỳ | Đăng ký sản phẩm, nhiệm vụ | Chờ đăng ký sản phẩm | `B1_REGISTER` / `AwaitingRegistration` |
| 1 | (phê duyệt) | Duyệt danh mục sản phẩm | Chờ duyệt danh mục | `B1_APPROVE` / `AwaitingTaskApproval` |
| 2 | Cá nhân tự chấm điểm, đánh giá và đề xuất mức xếp loại | Tự chấm điểm, đề xuất mức | Chờ tự chấm | `B2_SELF_SCORE` / `AwaitingSelfScore` |
| 2 | Xác nhận của Chi bộ (trên Mẫu 09A–9D) | Chi bộ xác nhận phiếu tự chấm | Chờ Chi bộ xác nhận | `B2_CELL_CONFIRM` / `AwaitingCellConfirm` |
| 3a | Tập thể lãnh đạo, quản lý đề xuất bằng phiếu kín | Đề xuất của tập thể lãnh đạo | Chờ ghi nhận đề xuất tập thể | `B3A_COLLECTIVE` / `AwaitingCollective` |
| 3b | Tổng hợp, thẩm định | Thẩm định | Chờ thẩm định | `B3B_APPRAISAL` / `AwaitingAppraisal` |
| 3c | Cấp trực tiếp sử dụng nhận xét, đề xuất | Nhận xét của cấp trực tiếp sử dụng | Chờ cấp trực tiếp sử dụng | `B3C_DIRECTOR` / `AwaitingDirectorReview` |
| 4 | Quyết định mức xếp loại | Quyết định mức xếp loại | Chờ quyết định | `B4_DECISION` / `AwaitingDecision` |
| 5 | Thông báo và lưu trữ kết quả | Công bố và lưu trữ kết quả | Chờ công bố; sau đó **Đã công bố** (khóa hồ sơ) | `B5_PUBLISH` / `AwaitingPublish`, `Published` |

Chế độ của một bước trong một diện quyết định: **Nội bộ** (làm trong hệ thống), **Cấp trên thực hiện** (cấp trên làm ngoài hệ thống, hệ thống chỉ ghi nhận kết quả), **Không áp dụng** (`Internal`, `External`, `Off`).

Hành động trên hồ sơ: Duyệt danh mục, Trả lại, Nộp phiếu tự chấm, Chi bộ xác nhận, Ghi nhận đề xuất của tập thể, Thẩm định, Nhận xét/đề xuất, Ghi nhận quyết định, Công bố kết quả, Mở lại hồ sơ, Ghi nhận kết quả của cấp trên.

Trạng thái kỳ đánh giá: **Dự thảo** (cấu hình) → **Đang mở** → **Khóa dữ liệu** (sau điểm chốt dữ liệu; chỉ các bước từ thẩm định trở đi) → **Đã đóng** (`Draft`, `Open`, `Locked`, `Closed`).

Trạng thái phiếu: phiếu đối soát — Chờ lập phiếu, Đang lập phiếu, Chờ xác nhận, Đã xác nhận, Đã hủy; phiếu kiểm soát — Đang soạn, Chờ xác nhận, Đã xác nhận, Đã hủy; biên bản hiệu chuẩn — Đang soạn, Đã chốt, Đã hủy.

## 3. Mức xếp loại

| Tên trong tài liệu gốc (Mục III.5) | Tên trên giao diện | Viết tắt trong biểu mẫu | Mã trong code |
|---|---|---|---|
| Hoàn thành xuất sắc nhiệm vụ (90–100 điểm) | giữ nguyên | HTXS | `HoanThanhXuatSac` |
| Hoàn thành tốt nhiệm vụ (70 đến dưới 90) | giữ nguyên | HTT | `HoanThanhTot` |
| Hoàn thành nhiệm vụ (50 đến dưới 70) | giữ nguyên | HTNV; "Mức C" trong Mẫu 17 | `HoanThanh` |
| Không hoàn thành nhiệm vụ (dưới 50 hoặc thuộc trường hợp bắt buộc) | giữ nguyên | KHT; "Mức D" trong Mẫu 17 | `KhongHoanThanh` |
| Chưa xếp loại (Mục III.9) | giữ nguyên | — | `ChuaXepLoai` |

Giao diện luôn viết đủ tên mức, không dùng chữ viết tắt. Các chữ A, B, C, D trong công thức là bốn thành phần điểm, không phải tên mức.

## 4. Vai trò

Tên vai trò là dữ liệu trong CSDL, quản trị sửa được ở menu Hệ thống, Vai trò. **Mã vai trò không đổi.** Tên gọi tương ứng trong Mẫu 18 (cột "Phân quyền truy cập") ghi ở cột 3.
Cột "Tên mặc định hiện hành" là tên các vai trò mặc định mang trên CSDL mới; cột "Tên cũ" là tên các bản trước đây dùng. CSDL đã vận hành được đổi sang tên mới bằng migration `DoiTenVaiTroMacDinhTheoThuatNgu` khi nâng cấp: chỉ đổi vai trò còn đúng tên cũ mặc định (vai trò quản trị đã tự đổi tên giữ nguyên), bỏ qua nếu tên mới đã có vai trò khác dùng, và chỉ đổi mô tả khi còn đúng mô tả cũ mặc định.

| Mã | Tên cũ | Tên trong Mẫu 18 / tài liệu gốc | Tên mặc định hiện hành |
|---|---|---|---|
| `NGUOI_DUOC_DANH_GIA` | Người được đánh giá | Cá nhân | Người được đánh giá |
| `LANH_DAO_PHONG` | Lãnh đạo Phòng | Lãnh đạo; thủ trưởng đơn vị | Lãnh đạo đơn vị |
| `THU_KY_TAP_THE` | Thư ký tập thể lãnh đạo | Thư ký (Mẫu 12) | Thư ký hội nghị |
| `CHI_UY_CHI_BO` | Chi ủy / Bí thư Chi bộ | Chi bộ (xác nhận của Chi bộ) | Chi bộ |
| `CO_QUAN_THAM_DINH` | Phòng TCCB-LĐ (mở kỳ, thẩm định) | Thẩm định; TCCB; cơ quan thẩm định | Cơ quan thẩm định |
| `CAP_TRUC_TIEP_SU_DUNG` | Cấp trực tiếp sử dụng (Giám đốc/Chủ tịch) | Cấp trực tiếp sử dụng cán bộ | Cấp trực tiếp sử dụng |
| `CAP_UY_VIEN` | Cấp ủy viên Đảng ủy | Cấp ủy viên | Cấp ủy viên |
| `VAN_PHONG_DANG_UY` | Văn phòng Đảng ủy (quyết định, công bố) | BTV Đảng ủy (quyết định) | Văn phòng Đảng ủy |
| `CO_QUAN_CHUYEN_MON` | (chưa có trong CSDL cũ) | Cơ quan chuyên môn | Cơ quan chuyên môn |
| `QUAN_TRI_HE_THONG` | Quản trị hệ thống | Đầu mối quản trị dữ liệu / IT | Quản trị hệ thống |

Phần trong ngoặc của tên cũ (mở kỳ, thẩm định; quyết định, công bố; Giám đốc/Chủ tịch) nay nằm ở mục "Mô tả" của vai trò, không nằm trong tên.
Mã cũ `CO_QUAN_CHUYEN_MON_DOI_SOAT` chưa từng có trên CSDL nào; mã `CO_QUAN_CHUYEN_MON` là mã duy nhất.

## 5. Quyền

Quyền hiển thị theo nhóm (phân hệ) và tên bên dưới; **mã quyền không đổi**.

| Nhóm quyền (mã) | Tên nhóm trên giao diện | Việc |
|---|---|---|
| `system` | Quản trị hệ thống | tài khoản, vai trò, gán vai trò, nhật ký, thông tin đơn vị, file mẫu biểu mẫu, nhập dữ liệu |
| `catalog` | Danh mục | đơn vị chính quyền, tổ chức Đảng, loại đơn vị, chức vụ |
| `period` | Kỳ đánh giá | tạo kỳ, cấu hình, mở/khóa kỳ |
| `criteria` | Bộ tiêu chí | bộ tiêu chí và thang điểm |
| `evaluation` | Đánh giá cá nhân | các bước trên hồ sơ, kiến nghị, kế hoạch 30-60-90 ngày, phiếu đối soát (Mẫu 03), biên bản hiệu chuẩn (Mẫu 20), phiếu kiểm soát (Mẫu 19) |
| `collective` | Đánh giá tập thể | hồ sơ tập thể (Mẫu 06–08) |
| `meeting` | Hội nghị, kiểm phiếu | biên bản hội nghị, kiểm phiếu (Mẫu 12–13) |
| `report` | Báo cáo | xuất báo cáo tổng hợp (Mẫu 14–16) |

Quyền theo cặp lập và xác nhận: **Lập** phiếu/biên bản và **Xác nhận** (hoặc **Chốt**) là hai quyền riêng; người xác nhận phải khác người lập.

## 6. Cổng trước quyết định

Đặt theo diện quyết định; thứ tự kiểm tra: hiệu chuẩn, kiểm soát. Đối soát chỉ chặn thẩm định khi còn phiếu đang mở.

| Cổng | Biểu mẫu (tài liệu gốc) | Tên trên giao diện | Chế độ | Mã |
|---|---|---|---|---|
| Hiệu chuẩn | Mẫu 20 — Biên bản hiệu chuẩn kết quả đánh giá ("trước khi phê duyệt") | Hiệu chuẩn kết quả | Bắt buộc / Chỉ ghi nhận / Tắt | `CALIBRATION` |
| Kiểm soát | Mẫu 19 — Phiếu kiểm soát tính hợp lệ, công bằng và nhất quán ("trước khi trình phê duyệt") | Kiểm soát trước phê duyệt | Bắt buộc / Chỉ ghi nhận / Tắt | `CONTROL` |
| Đối soát | Mẫu 03 — Phiếu ý kiến chuyên môn độc lập / đối soát dữ liệu chuyên ngành ("khi cần") | Đối soát chuyên ngành | Tùy chọn / Tắt | `CROSSCHECK` |

Menu: **Hiệu chuẩn và kiểm soát** (biên bản hiệu chuẩn, phiếu kiểm soát) và **Đối soát chuyên ngành** (phiếu đối soát).

## 7. Tên trong biểu mẫu gốc và đơn vị tại ATTECH

Hướng dẫn 03-HD/TVĐU và biểu mẫu gốc viết cho Tổng công ty; các "Ban" trong đó là cơ quan của Tổng công ty, không phải của ATTECH (ATTECH không có "Ban An toàn"). Chữ in sẵn trong file Word mẫu giữ nguyên theo biểu mẫu gốc; giao diện và tài liệu gọi đúng đơn vị tại ATTECH. Đơn vị cụ thể lấy theo cây đơn vị trong cài đặt, không ghi cứng trong code.

| Tên trong biểu mẫu gốc | Tại ATTECH |
|---|---|
| Ban TCCB-LĐ (Ban Tổ chức cán bộ - Lao động) | Phòng Tổ chức cán bộ - Lao động (cơ quan thẩm định) |
| Ban chuyên môn / đơn vị đối soát (Mẫu 03) | Phòng, đơn vị chuyên môn, ví dụ Phòng Kỹ thuật - An toàn - Chất lượng, Phòng Tài chính - Kế toán, Phòng Kế hoạch - Kinh doanh, Phòng Nghiên cứu - Phát triển, Ban Quản lý dự án ĐT&XDCN, các Trung tâm, Văn phòng Công ty |
| Lãnh đạo Ban chuyên môn (ô xác nhận Mẫu 03) | Lãnh đạo phòng, đơn vị chuyên môn |
| Ban Tổ chức Đảng ủy Tổng công ty (Ban TCĐU), Thường vụ Đảng ủy Tổng công ty, HĐTV | Cấp trên, ngoài hệ thống; ATTECH chỉ ghi nhận kết quả của cấp trên (bước chế độ "Cấp trên thực hiện") |

Vai trò "Cơ quan chuyên môn" giữ nguyên tên; mô tả của vai trò nêu rõ là phòng, đơn vị chuyên môn của ATTECH.

## 8. Khái niệm khác

| Khái niệm | Tên trong tài liệu gốc | Tên trên giao diện | Mã trong code |
|---|---|---|---|
| Kỳ đánh giá | Kỳ đánh giá: Quý … / Năm … | Kỳ đánh giá | `EvaluationPeriod` |
| Hồ sơ của một người trong một kỳ | Hồ sơ đánh giá (Mục V.1) | Hồ sơ đánh giá | `EvaluationRecord` |
| Người được đánh giá | Cán bộ được đánh giá | Người được đánh giá | `PartyMemberProfile` |
| Diện quyết định: bộ cấu hình các bước (ai làm, nội bộ hay cấp trên, thời hạn, cổng) áp cho một nhóm cán bộ | "cán bộ thuộc diện … quyết định, phê duyệt mức xếp loại" (Mục I.5, Mẫu 14, 15A/15B) | Diện quyết định | `WorkflowProfile` (`profiles` trong cấu hình kỳ) |
| Thẩm quyền quyết định của người: Đảng ủy cơ sở hay cấp trên; suy ra từ chức vụ, ghi đè được kèm lý do; chọn diện quyết định mặc định | Bảng thẩm quyền quyết định, phê duyệt (Mục I.5) | Đảng ủy cơ sở quyết định / Cấp ủy cấp trên quyết định | `ApprovalAuthority` (`CoSo`, `CapTren`) |
| Bộ tiêu chí | Khung tiêu chí, thang điểm (Phụ lục I, Mục III) | Bộ tiêu chí (có phiên bản: Nháp, Đã xuất bản, Lưu trữ) | `CriteriaSet` |
| Sản phẩm, nhiệm vụ đăng ký đầu kỳ | Sản phẩm công việc (Mẫu 01) | Danh mục sản phẩm | `EvaluationTask` |
| Hội nghị đánh giá | Hội nghị tập thể lãnh đạo, quản lý hoặc Hội nghị Đảng ủy/Chi ủy cơ sở | Hội nghị | `EvaluationMeeting` |
| Kiến nghị sau công bố | Khiếu nại, kiến nghị (Phụ lục II, III.2) | Kiến nghị | `EvaluationAppeal` |
| Mở lại hồ sơ đã công bố | — | Mở lại hồ sơ | `Reopen` |
| Mở riêng hồ sơ khi kỳ đã khóa dữ liệu | — | Mở riêng hồ sơ | `GrantUnlock` |
| Điểm chốt dữ liệu | Mục II.2 | Khóa dữ liệu (trạng thái kỳ) | `Locked` |

### Phạm vi gán vai trò

Một lần gán = người + vai trò + phạm vi + thời hạn. Phạm vi bao trùm mọi đơn vị con trong cây.

| Phạm vi | Tên trên giao diện | Ý nghĩa | Mã |
|---|---|---|---|
| Toàn bộ | Toàn công ty | mọi đơn vị, mọi tổ chức Đảng | `Global` |
| Một đơn vị chính quyền | Đơn vị chính quyền | đơn vị được chọn và các đơn vị con | `Department` |
| Một tổ chức Đảng | Tổ chức Đảng | tổ chức Đảng được chọn và các tổ chức con | `PartyCell` |

Quyền "không áp dụng phạm vi" (ví dụ quyền của Quản trị hệ thống) chỉ gán được ở phạm vi **Toàn công ty**.
