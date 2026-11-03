# TÀI LIỆU HƯỚNG DẪN QUẢN TRỊ PHÂN QUYỀN, VAI TRÒ NGHIỆP VỤ VÀ XỬ LÝ BIẾN ĐỘNG CÔNG TÁC CÁN BỘ

**Đơn vị áp dụng:** Đảng bộ Công ty TNHH Kỹ thuật Quản lý bay (ATTECH)  
**Căn cứ nghiệp vụ:**
- Hướng dẫn số 03-HD/TVĐU của Ban Thường vụ Đảng ủy Tổng công ty Quản lý bay Việt Nam về việc đánh giá định kỳ hằng quý đối với cán bộ lãnh đạo, quản lý;
- Kế hoạch đánh giá, xếp loại chất lượng cán bộ hằng quý của Đảng ủy Công ty TNHH Kỹ thuật Quản lý bay.

---

## PHẦN I. QUY ĐỊNH CHUNG VỀ MÔ HÌNH QUẢN LÝ

Hệ thống thông tin đánh giá cán bộ quản lý tách biệt độc lập ba nhóm dữ liệu của cán bộ nhằm bảo đảm tính chính xác của hồ sơ nhân sự, báo cáo thống kê và phân quyền thực thi quy trình:

### 1. Chức vụ bổ nhiệm (`Position` / `MemberPosition`)
- **Định nghĩa:** Là chức danh chính quyền hoặc chức vụ Đảng được cấp có thẩm quyền bổ nhiệm, chuẩn y theo quyết định chính thức (ví dụ: *Chủ tịch Công ty*, *Giám đốc*, *Phó Giám đốc*, *Trưởng phòng*, *Bí thư Chi bộ*...).
- **Phạm vi sử dụng trên hệ thống:**
  1. Hiển thị chức danh chính thức của cán bộ trên các biểu mẫu kết xuất (Mẫu 01, Mẫu 02, Mẫu 09A–09D, biên bản và thông báo kết quả).
  2. Xác định nhóm chức danh thống kê trên báo cáo tổng hợp Mẫu 15A, Mẫu 15B (`M1` đến `M26`) và đối tượng người đứng đầu (`IsHead`) khi tính tỷ lệ trần xếp loại *"Hoàn thành xuất sắc nhiệm vụ"* (không quá 20%).
  3. Xác định thẩm quyền quyết định mặc định (`ApprovalAuthority`) và diện quyết định mặc định (`DefaultWorkflowProfileCode`) khi đưa cán bộ vào danh sách đánh giá của kỳ.
- **Nguyên tắc quản lý:** Chỉ cập nhật chức vụ khi có quyết định bổ nhiệm, miễn nhiệm hoặc giao Quyền chính thức của cấp có thẩm quyền. Trường hợp lãnh đạo cấp trên được phân công phụ trách, chỉ đạo đơn vị mà không có quyết định bổ nhiệm kiêm nhiệm chức danh cấp trưởng đơn vị đó thì không khai báo thêm chức vụ bổ nhiệm tại đơn vị được phụ trách.

### 2. Vai trò nghiệp vụ và Phạm vi phân quyền (`UserRoleAssignment`)
- **Định nghĩa:** Là tập hợp các quyền thao tác nghiệp vụ trên hệ thống được gán cho tài khoản người dùng gắn với một phạm vi tổ chức xác định (*Toàn công ty*, *Đơn vị chính quyền*, hoặc *Tổ chức Đảng*) và thời hạn hiệu lực (*Từ ngày* – *Đến hết ngày*).
- **Phạm vi sử dụng trên hệ thống:** Xác định thẩm quyền truy cập hồ sơ và thực hiện các bước trong quy trình đánh giá (phê duyệt danh mục sản phẩm, xác nhận của Chi bộ, ghi nhận kết quả tập thể lãnh đạo, thẩm định, nhận xét, quyết định và công bố).
- **Nguyên tắc kế thừa theo cấp tổ chức:** Phân công vai trò tại một đơn vị cấp trên đương nhiên có hiệu lực đối với toàn bộ các đơn vị trực thuộc cấp dưới trong cây tổ chức.

### 3. Diện quyết định (`WorkflowProfile`)
- **Định nghĩa:** Là cấu hình luồng quy trình áp dụng cho từng nhóm đối tượng cán bộ được đánh giá trong kỳ (bao gồm các bước áp dụng, chế độ thực hiện nội bộ hoặc ghi nhận kết quả cấp trên, quyền thực hiện từng bước và thời hạn hoàn thành).
- **Nguyên tắc kiểm soát xung đột lợi ích (COI):** Hệ thống tự động kiểm soát nguyên tắc một cá nhân không được thực hiện quyền phê duyệt, xác nhận, thẩm định, nhận xét, quyết định hoặc giải quyết kiến nghị trên hồ sơ đánh giá của chính mình.

---

## PHẦN II. DANH MỤC VAI TRÒ, THẨM QUYỀN VÀ NHIỆM VỤ TRÊN HỆ THỐNG

| TT | Tên vai trò (`Mã vai trò`) | Phạm vi phân công | Mã quyền hệ thống (`PermissionCodes`) | Nhiệm vụ và thẩm quyền thực hiện trên hệ thống |
| :-: | :--- | :--- | :--- | :--- |
| **1** | **Người được đánh giá**<br>(`NGUOI_DUOC_DANH_GIA`) | Toàn công ty | `evaluation.self`<br>`evaluation.results.view`<br>`evaluation.appeal.submit` | - Lập và trình phê duyệt Bản đăng ký sản phẩm, nhiệm vụ đầu quý (Mẫu 01); lập Phiếu đề xuất điều chỉnh, bổ sung nhiệm vụ trong quý (Mẫu 02).<br>- Thực hiện tự đánh giá, chấm điểm, đính kèm tài liệu minh chứng và tự nhận mức xếp loại cuối quý (Mẫu 09A–09D).<br>- Tra cứu kết quả đánh giá sau khi công bố; gửi đơn kiến nghị về kết quả đánh giá (nếu có). |
| **2** | **Lãnh đạo đơn vị**<br>(`LANH_DAO_PHONG`) | Đơn vị chính quyền | `evaluation.read`<br>`evaluation.tasks.approve`<br>`evaluation.unit.review`<br>`evaluation.improvement.manage`<br>`evaluation.opinion.record` | - Phê duyệt hoặc yêu cầu hoàn thiện lại danh mục sản phẩm, nhiệm vụ (Mẫu 01, Mẫu 02) của cán bộ thuộc phạm vi đơn vị quản lý (Bước 1).<br>- Nhận xét, đánh giá và đề xuất mức xếp loại đối với cán bộ thuộc diện Bí thư, Phó Bí thư Chi bộ là nhân viên tại Bước 3c.<br>- Lập, phê duyệt và theo dõi Kế hoạch khắc phục hạn chế 30-60-90 ngày (Mẫu 17/21) đối với cán bộ thuộc quyền quản lý. |
| **3** | **Phó Giám đốc phụ trách**<br>(`PHO_GIAM_DOC_PHU_TRACH`) | Đơn vị chính quyền | `evaluation.read`<br>`evaluation.opinion.record` | - Theo dõi hồ sơ đánh giá của cán bộ thuộc các phòng, đơn vị được phân công phụ trách.<br>- Ghi ý kiến nhận xét của Phó Giám đốc phụ trách trên hồ sơ Trưởng phòng, Phó Trưởng phòng và tương đương trước khi Giám đốc Công ty phê duyệt danh mục nhiệm vụ (Bước 1) hoặc nhận xét, đề xuất xếp loại (Bước 3c). |
| **4** | **Cấp trực tiếp sử dụng**<br>(`CAP_TRUC_TIEP_SU_DUNG`) | Đơn vị chính quyền | `evaluation.read`<br>`evaluation.director.review`<br>`evaluation.improvement.manage`<br>`report.export` | - Chủ tịch Công ty phê duyệt danh mục sản phẩm, nhiệm vụ (Bước 1) và nhận xét, đề xuất mức xếp loại (Bước 3c) đối với các Phó Giám đốc Công ty.<br>- Giám đốc Công ty nhận xét, đánh giá và đề xuất mức xếp loại (Bước 3c) đối với cán bộ lãnh đạo, quản lý thuộc diện Đảng ủy cơ sở quyết định. |
| **5** | **Chi bộ**<br>(`CHI_UY_CHI_BO`) | Tổ chức Đảng | `evaluation.read`<br>`evaluation.cell.confirm`<br>`collective.manage`<br>`meeting.read` | - Xác nhận kết quả rà soát phiếu tự chấm của đảng viên sinh hoạt tại Chi bộ (Bước 2).<br>- Lập và quản lý hồ sơ tự đánh giá định kỳ của tập thể Chi bộ (Mẫu 06, Mẫu 07, Mẫu 08). |
| **6** | **Thư ký hội nghị**<br>(`THU_KY_TAP_THE`) | Đơn vị chính quyền / Toàn công ty | `evaluation.read`<br>`evaluation.collective.record`<br>`meeting.read`, `meeting.manage`<br>`evaluation.opinion.record` | - Lập biên bản hội nghị và biên bản kiểm phiếu tín nhiệm của tập thể lãnh đạo (Mẫu 12, Mẫu 13, Mẫu 17).<br>- Ghi nhận kết quả bỏ phiếu đề xuất mức xếp loại của tập thể lãnh đạo đơn vị hoặc tập thể lãnh đạo Công ty vào hồ sơ đánh giá (Bước 3a). |
| **7** | **Cơ quan chuyên môn**<br>(`CO_QUAN_CHUYEN_MON`) | Đơn vị chính quyền (`KTATCL`, `TCKT`, `KHKD`, `TCCB`, `VP`) | `evaluation.crosscheck.respond`<br>`evaluation.crosscheck.confirm` | - Tiếp nhận yêu cầu, lập và xác nhận Phiếu đối soát dữ liệu chuyên ngành (Mẫu 03) đối với các chỉ tiêu thuộc lĩnh vực chuyên môn của đơn vị mình (người xác nhận độc lập với người lập phiếu). |
| **8** | **Cơ quan thẩm định**<br>(`CO_QUAN_THAM_DINH`) | Toàn công ty | `period.manage`, `criteria.manage`<br>`evaluation.read`, `evaluation.appraise`<br>`evaluation.crosscheck.request`<br>`evaluation.calibration.manage`<br>`evaluation.control.perform`<br>`evaluation.opinion.record`<br>`report.export`, `system.users.read` | - Khởi tạo kỳ đánh giá, thiết lập cấu hình diện quyết định, thời hạn, danh sách cán bộ tham gia, kiểm tra điều kiện sẵn sàng và mở/đóng kỳ đánh giá.<br>- Quản lý, ban hành bộ tiêu chí đánh giá và khung tỷ trọng.<br>- Gửi yêu cầu đối soát dữ liệu chuyên ngành (Mẫu 03).<br>- Thực hiện thẩm định hồ sơ, tài liệu minh chứng, điểm số và đề xuất thẩm định tại Bước 3b.<br>- Lập Biên bản họp thống nhất/hiệu chuẩn (Mẫu 20) và Phiếu kiểm tra, giám sát quy trình (Mẫu 19). |
| **9** | **Văn phòng Đảng ủy**<br>(`VAN_PHONG_DANG_UY`) | Toàn công ty | `evaluation.read`, `evaluation.decide`<br>`evaluation.external.record`<br>`evaluation.publish`, `evaluation.reopen`<br>`evaluation.appeal.resolve`<br>`evaluation.calibration.conclude`<br>`evaluation.control.confirm`<br>`evaluation.crosscheck.confirm`<br>`meeting.read`, `meeting.manage`, `report.export` | - Chốt Biên bản họp thống nhất/hiệu chuẩn (Mẫu 20) và xác nhận Phiếu kiểm tra, giám sát (Mẫu 19).<br>- Ghi nhận Quyết định xếp loại của Đảng ủy Công ty đối với cán bộ thuộc thẩm quyền cơ sở (Bước 4).<br>- Ghi nhận kết quả phê duyệt, thẩm định, nhận xét và quyết định của cấp trên (HĐTV, Ban TCĐU, BTV Đảng ủy Tổng công ty) đối với cán bộ thuộc diện cấp trên quản lý.<br>- Công bố kết quả đánh giá (Bước 5); mở lại hồ sơ khi có yêu cầu đính chính hợp lệ; tiếp nhận và xử lý kiến nghị sau công bố. |
| **10** | **Cấp ủy viên**<br>(`CAP_UY_VIEN`) | Toàn công ty | `evaluation.read`<br>`meeting.read`<br>`report.export` | - Tra cứu hồ sơ đánh giá, biên bản hội nghị và kết xuất các biểu mẫu báo cáo tổng hợp phục vụ công tác lãnh đạo, chỉ đạo của Ban Thường vụ và Ban Chấp hành Đảng bộ Công ty. |
| **11** | **Quản trị hệ thống**<br>(`QUAN_TRI_HE_THONG`) | Toàn công ty | `system.users.*`, `system.roles.manage`<br>`system.assignments.manage`<br>`system.audit.read`, `system.settings.manage`<br>`system.templates.manage`, `system.import`<br>`catalog.manage` | - Quản trị kỹ thuật hệ thống: quản lý danh mục tổ chức Đảng, đơn vị chính quyền, chức vụ; quản lý tài khoản người dùng; thực hiện phân công vai trò theo quyết định của cấp có thẩm quyền; quản lý biểu mẫu in và nhật ký vận hành.<br>- Không tham gia xem nội dung chấm điểm hoặc can thiệp nghiệp vụ đánh giá (theo quy định tại Mẫu 18). |

---

## PHẦN III. QUY TRÌNH THỰC HIỆN THEO 04 DIỆN QUYẾT ĐỊNH

| Bước quy trình | 1. Diện Đảng ủy cơ sở (`co-so`)<br>*(Trưởng/Phó các Phòng, Ban, Trung tâm, Xí nghiệp, Chi nhánh)* | 2. Bí thư/Phó BT Chi bộ là nhân viên (`bi-thu-nhan-vien`)<br>*(Đài trưởng, Đội trưởng, Trạm trưởng giữ chức BT/PBT Chi bộ trực thuộc ĐBBP — M24, M25)* | 3. Phó Giám đốc (`pho-giam-doc`)<br>*(Phó Giám đốc Công ty)* | 4. Diện BTV Đảng ủy Tổng công ty (`cap-tren`)<br>*(Chủ tịch Công ty, Giám đốc, Kiểm soát viên)* |
| :--- | :--- | :--- | :--- | :--- |
| **Bước 1a. Đăng ký sản phẩm, nhiệm vụ** (`B1_REGISTER`) | Cán bộ tự đăng ký<br>*(Thời hạn: Ngày làm việc cuối quý trước -5)* | Cán bộ tự đăng ký<br>*(Thời hạn: Ngày làm việc cuối quý trước -5)* | Cán bộ tự đăng ký<br>*(Thời hạn: Ngày làm việc thứ 1 đầu quý)* | Cán bộ tự đăng ký<br>*(Thời hạn: Ngày làm việc thứ 1 đầu quý)* |
| **Bước 1b. Phê duyệt danh mục** (`B1_APPROVE`) | - **Cấp phó:** Cấp trưởng đơn vị phê duyệt.<br>- **Cấp trưởng:** Giám đốc Công ty phê duyệt (trên cơ sở ý kiến của PGĐ phụ trách).<br>*(Thời hạn: Ngày làm việc cuối quý trước -1)* | Cấp trưởng đơn vị trực tiếp quản lý phê duyệt<br>*(Thời hạn: Ngày làm việc cuối quý trước -1)* | Chủ tịch Công ty phê duyệt trên hệ thống<br>*(Thời hạn: Ngày làm việc thứ 5 đầu quý)* | HĐTV Tổng công ty phê duyệt (Văn phòng Đảng ủy ghi nhận kết quả)<br>*(Thời hạn: Ngày làm việc thứ 5 đầu quý)* |
| **Bước 2a. Tự chấm điểm** (`B2_SELF_SCORE`) | Cán bộ tự đánh giá<br>*(Thời hạn: Ngày 09 tháng cuối quý)* | Cán bộ tự đánh giá<br>*(Thời hạn: Ngày 09 tháng cuối quý)* | Cán bộ tự đánh giá<br>*(Thời hạn: Ngày 09 tháng cuối quý)* | Cán bộ tự đánh giá<br>*(Thời hạn: Ngày 09 tháng cuối quý)* |
| **Bước 2b. Chi bộ xác nhận** (`B2_CELL_CONFIRM`) | Chi ủy / Bí thư Chi bộ nơi sinh hoạt xác nhận<br>*(Thời hạn: Ngày 09 tháng cuối quý)* | Chi ủy Chi bộ hoặc Đảng ủy bộ phận xác nhận<br>*(Thời hạn: Ngày 09 tháng cuối quý)* | Chi ủy / Bí thư Chi bộ nơi sinh hoạt xác nhận<br>*(Thời hạn: Ngày 09 tháng cuối quý)* | Chi ủy / Bí thư Chi bộ nơi sinh hoạt xác nhận<br>*(Thời hạn: Ngày 09 tháng cuối quý)* |
| **Bước 3a. Tập thể lãnh đạo đề xuất** (`B3A_COLLECTIVE`) | Tập thể lãnh đạo Phòng/Đơn vị họp, bỏ phiếu; Thư ký hội nghị ghi nhận<br>*(Thời hạn: Ngày 10 tháng cuối quý)* | *Không áp dụng (`Off`)* | Tập thể lãnh đạo Công ty họp, bỏ phiếu; Thư ký hội nghị ghi nhận<br>*(Thời hạn: Ngày 14 tháng cuối quý)* | Tập thể lãnh đạo Công ty họp, bỏ phiếu; Thư ký hội nghị ghi nhận<br>*(Thời hạn: Ngày 14 tháng cuối quý)* |
| **Bước 3b. Cơ quan thẩm định** (`B3B_APPRAISAL`) | Phòng TCCB-LĐ thẩm định trên hệ thống<br>*(Thời hạn: Ngày 11 tháng cuối quý)* | Phòng TCCB-LĐ thẩm định trên hệ thống<br>*(Thời hạn: Ngày 11 tháng cuối quý)* | *Không tách bước riêng (`Off`) — Kết quả thẩm định của Ban TCĐU TCT được ghi nhận cùng Bước 4* | Ban Tổ chức Đảng ủy TCT thẩm định (Văn phòng Đảng ủy ghi nhận)<br>*(Thời hạn: Ngày 15 tháng cuối quý)* |
| **Bước 3c. Cấp trực tiếp sử dụng nhận xét** (`B3C_DIRECTOR`) | Giám đốc Công ty nhận xét, đề xuất mức xếp loại<br>*(Thời hạn: Ngày 14 tháng cuối quý)* | Cấp trưởng đơn vị nhận xét, đề xuất mức xếp loại trên ý kiến cấp phó<br>*(Thời hạn: Ngày 14 tháng cuối quý)* | Chủ tịch Công ty nhận xét, đề xuất mức xếp loại trên hệ thống<br>*(Thời hạn: Ngày 14 tháng cuối quý)* | HĐTV Tổng công ty nhận xét, đề xuất (Văn phòng Đảng ủy ghi nhận)<br>*(Thời hạn: Ngày 17 tháng cuối quý)* |
| **Bước 4. Quyết định xếp loại** (`B4_DECISION`) | Đảng ủy Công ty quyết định (Văn phòng Đảng ủy ghi nhận sau khi qua cổng Đối soát & Hiệu chuẩn)<br>*(Thời hạn: Ngày 14 tháng cuối quý)* | Đảng ủy Công ty quyết định (Văn phòng Đảng ủy ghi nhận sau khi qua cổng Đối soát & Hiệu chuẩn)<br>*(Thời hạn: Ngày 14 tháng cuối quý)* | BTV Đảng ủy Tổng công ty quyết định (Văn phòng Đảng ủy ghi nhận kết quả)<br>*(Thời hạn: Ngày 18 tháng cuối quý)* | BTV Đảng ủy Tổng công ty quyết định (Văn phòng Đảng ủy ghi nhận kết quả)<br>*(Thời hạn: Ngày 18 tháng cuối quý)* |
| **Bước 5. Công bố và lưu trữ** (`B5_PUBLISH`) | Văn phòng Đảng ủy thực hiện công bố trên hệ thống | Văn phòng Đảng ủy thực hiện công bố trên hệ thống | Văn phòng Đảng ủy thực hiện công bố trên hệ thống | Văn phòng Đảng ủy thực hiện công bố trên hệ thống |

---

## PHẦN IV. DANH MỤC TÀI KHOẢN, CHỨC VỤ VÀ PHÂN QUYỀN HIỆN HÀNH TẠI ĐẢNG BỘ CÔNG TY

### 1. Ban Lãnh đạo Công ty (`CTY`) và Quản trị hệ thống

| Tài khoản | Họ và tên | Chức vụ bổ nhiệm hiện hành (`MemberPosition`) | Diện QĐ mặc định | Vai trò và Phạm vi phân công (`UserRoleAssignment`) |
| :--- | :--- | :--- | :---: | :--- |
| `quantri` | Quản trị hệ thống | — | — | - Quản trị hệ thống *(Toàn công ty)* |
| `thinh.lt` | Lê Tiến Thịnh | - Chủ tịch Công ty (`M14`, tại `CTY`)<br>- Bí thư Đảng ủy (`M8`, tại `DU`)<br>- UV BCH Đảng bộ TCT (`M6`, tại `DU`) | `cap-tren` | - Cấp trực tiếp sử dụng *(Đơn vị `CTY`)*<br>- Cấp ủy viên *(Toàn công ty)*<br>- Người được đánh giá *(Toàn công ty)* |
| `giang.nh` | Nguyễn Hoàng Giang | - Giám đốc (`M14`, tại `CTY`)<br>- Phó Bí thư Đảng ủy (`M9`, tại `DU`) | `cap-tren` | - Lãnh đạo đơn vị *(Đơn vị `CTY`)*<br>- Cấp trực tiếp sử dụng *(12 đơn vị trực thuộc `CTY`)*<br>- Chi bộ *(Đảng ủy `DU`)*<br>- Cấp ủy viên *(Toàn công ty)*<br>- Người được đánh giá *(Toàn công ty)* |
| `da.dc` | Đỗ Công Đà | - Kiểm soát viên (`M15`, tại `CTY`) | `cap-tren` | - Người được đánh giá *(Toàn công ty)* |
| `hung.pq` | Phan Quốc Hưng | - Phó Giám đốc (`M14`, tại `CTY`)<br>- UV BTV Đảng ủy (`M17`), Chủ nhiệm UBKT (`M19`) | `pho-giam-doc` | - Phó Giám đốc phụ trách *(Đơn vị `CTY`)*<br>- Cấp ủy viên *(Toàn công ty)*<br>- Người được đánh giá *(Toàn công ty)* |
| `minh.dn` | Đinh Nhật Minh | - Phó Giám đốc (`M14`, tại `CTY`)<br>- Đảng ủy viên (`M18`, tại `DU`)<br>- Trưởng trung tâm (`M26`, tại `TTHL`) | `pho-giam-doc` | - Phó Giám đốc phụ trách *(Đơn vị `CTY`)*<br>- Cấp ủy viên *(Toàn công ty)*<br>- Người được đánh giá *(Toàn công ty)* |
| `thanh.nn` | Nguyễn Như Thành | - Phó Giám đốc (`M14`, tại `CTY`)<br>- Đảng ủy viên (`M18`, tại `DU`) | `pho-giam-doc` | - Phó Giám đốc phụ trách *(Đơn vị `CTY`)*<br>- Cấp ủy viên *(Toàn công ty)*<br>- Người được đánh giá *(Toàn công ty)* |

### 2. Khối Phòng chức năng và Văn phòng Công ty

| Đơn vị | Tài khoản | Họ và tên | Chức vụ chính quyền & Đảng | Diện QĐ | Vai trò và Phạm vi phân công |
| :--- | :--- | :--- | :--- | :---: | :--- |
| **Phòng TCCB-LĐ**<br>(`TCCB` / `CB-TCCB`) | `loan.ttp` | Trần Thị Phương Loan | Trưởng phòng (`M26`), Đảng ủy viên (`M18`), Bí thư CB (`M22`) | `co-so` | - Cơ quan thẩm định *(Toàn công ty)*<br>- Lãnh đạo đơn vị *(Đơn vị `TCCB`)*<br>- Chi bộ *(Chi bộ `CB-TCCB`)*<br>- Thư ký hội nghị *(Toàn công ty)*<br>- Cơ quan chuyên môn *(Đơn vị `TCCB`)*<br>- Cấp ủy viên, Người được đánh giá |
| **Phòng TCCB-LĐ**<br>(`TCCB` / `CB-TCCB`) | `loan.ctp` | Chu Thị Phương Loan | Phó Trưởng phòng (`M26`), Phó CN UBKT (`M19`), Phó BT CB (`M23`) | `co-so` | - Cơ quan thẩm định *(Toàn công ty)*<br>- Chi bộ *(Chi bộ `CB-TCCB`)*<br>- Cơ quan chuyên môn *(Đơn vị `TCCB`)*<br>- Người được đánh giá |
| **Văn phòng Công ty**<br>(`VP` / `CB-VP`) | `hung.dt` | Đinh Thế Hùng | Chánh Văn phòng (`M26`), Đảng ủy viên (`M18`), Bí thư CB (`M22`) | `co-so` | - Văn phòng Đảng ủy *(Toàn công ty)*<br>- Lãnh đạo đơn vị *(Đơn vị `VP`)*<br>- Chi bộ *(Chi bộ `CB-VP`)*<br>- Thư ký hội nghị *(Toàn công ty)*<br>- Cơ quan chuyên môn *(Đơn vị `VP`)*<br>- Cấp ủy viên, Người được đánh giá |
| **Văn phòng Công ty**<br>(`VP` / `CB-VP`) | `long.nc` | Nguyễn Chí Long | Phó Chánh Văn phòng (`M26`), UV UBKT (`M19`) | `co-so` | - Văn phòng Đảng ủy *(Toàn công ty)*<br>- Thư ký hội nghị *(Toàn công ty)*<br>- Cơ quan chuyên môn *(Đơn vị `VP`)*<br>- Người được đánh giá |
| **Văn phòng Công ty**<br>(`VP` / `CB-VP`) | `thao.nt` | Nguyễn Thanh Thảo | Phó Chánh Văn phòng (`M26`), Phó BT CB (`M23`) | `co-so` | - Văn phòng Đảng ủy *(Toàn công ty)*<br>- Chi bộ *(Chi bộ `CB-VP`)*<br>- Thư ký hội nghị *(Toàn công ty)*<br>- Cơ quan chuyên môn *(Đơn vị `VP`)*<br>- Người được đánh giá |
| **Phòng KT-AT-CL**<br>(`KTATCL` / `CB-KTATCL`) | `tuan.la` | Lê Anh Tuấn | Trưởng phòng (`M26`), Đảng ủy viên (`M18`), Bí thư CB (`M22`) | `co-so` | - Lãnh đạo đơn vị *(Đơn vị `KTATCL`)*<br>- Chi bộ *(Chi bộ `CB-KTATCL`)*<br>- Cơ quan chuyên môn *(Đơn vị `KTATCL`)*<br>- Cấp ủy viên, Người được đánh giá |
| **Phòng KT-AT-CL**<br>(`KTATCL` / `CB-KTATCL`) | `hung.nt` | Nguyễn Tiến Hùng | Phó Trưởng phòng (`M26`), Phó BT CB (`M23`) | `co-so` | - Chi bộ *(Chi bộ `CB-KTATCL`)*<br>- Cơ quan chuyên môn *(Đơn vị `KTATCL`)*<br>- Người được đánh giá |
| **Phòng KT-AT-CL**<br>(`KTATCL` / `CB-KTATCL`) | `nam.dt`<br>`nghia.tv` | Dương Thành Nam<br>Trịnh Văn Nghĩa | Phó Trưởng phòng (`M26`) | `co-so` | - Cơ quan chuyên môn *(Đơn vị `KTATCL`)*<br>- Người được đánh giá |
| **Phòng KH-KD**<br>(`KHKD` / `CB-KHKD`) | `duy.lm` | Lê Mai Duy | Trưởng phòng (`M26`), Đảng ủy viên (`M18`), Bí thư CB (`M22`) | `co-so` | - Lãnh đạo đơn vị *(Đơn vị `KHKD`)*<br>- Chi bộ *(Chi bộ `CB-KHKD`)*<br>- Cơ quan chuyên môn *(Đơn vị `KHKD`)*<br>- Cấp ủy viên, Người được đánh giá |
| **Phòng KH-KD**<br>(`KHKD` / `CB-KHKD`) | `dao.nta` | Nguyễn Thị Anh Đào | Phó Trưởng phòng (`M26`), Phó BT CB (`M23`) | `co-so` | - Chi bộ *(Chi bộ `CB-KHKD`)*<br>- Thư ký hội nghị *(Đơn vị `KHKD`)*<br>- Cơ quan chuyên môn *(Đơn vị `KHKD`)*<br>- Người được đánh giá |
| **Phòng KH-KD**<br>(`KHKD` / `CB-KHKD`) | `trung.nb` | Nguyễn Bình Trung | Phó Trưởng phòng (`M26`) | `co-so` | - Cơ quan chuyên môn *(Đơn vị `KHKD`)*<br>- Người được đánh giá |
| **Phòng TC-KT**<br>(`TCKT` / `CB-TCKT`) | `trang.npq` | Nguyễn Phạm Quỳnh Trang | Kế toán trưởng / Trưởng phòng (`M26`), Đảng ủy viên (`M18`), Bí thư CB (`M22`) | `co-so` | - Lãnh đạo đơn vị *(Đơn vị `TCKT`)*<br>- Chi bộ *(Chi bộ `CB-TCKT`)*<br>- Cơ quan chuyên môn *(Đơn vị `TCKT`)*<br>- Cấp ủy viên, Người được đánh giá |
| **Phòng TC-KT**<br>(`TCKT` / `CB-TCKT`) | `huyen.utt` | Uông Thị Thu Huyền | Phó Trưởng phòng (`M26`), Phó BT CB (`M23`), UV UBKT (`M19`) | `co-so` | - Chi bộ *(Chi bộ `CB-TCKT`)*<br>- Cơ quan chuyên môn *(Đơn vị `TCKT`)*<br>- Người được đánh giá |
| **Phòng NC-PT**<br>(`NCPT` / `CB-NCPT`) | `giang.nv` | Nguyễn Văn Giang | Trưởng phòng (`M26`), Đảng ủy viên (`M18`), Bí thư CB (`M22`) | `co-so` | - Lãnh đạo đơn vị *(Đơn vị `NCPT`)*<br>- Chi bộ *(Chi bộ `CB-NCPT`)*<br>- Cấp ủy viên, Người được đánh giá |
| **Phòng NC-PT**<br>(`NCPT` / `CB-NCPT`) | `tuan.na` | Nguyễn Anh Tuấn | Phó Trưởng phòng (`M26`), Phó BT CB (`M23`) | `co-so` | - Chi bộ *(Chi bộ `CB-NCPT`)*<br>- Người được đánh giá |

### 3. Khối Ban, Trung tâm, Xí nghiệp và Chi nhánh trực thuộc

| Đơn vị | Tài khoản | Họ và tên | Chức vụ chính quyền & Đảng | Diện QĐ | Vai trò và Phạm vi phân công |
| :--- | :--- | :--- | :--- | :---: | :--- |
| **Ban QLDA ĐT&XDCN**<br>(`BQLDA` / `CB-BQLDA`) | `quang.lh` | Lê Hồng Quang | Giám đốc ĐVTT (`M26`), Đảng ủy viên (`M18`), Bí thư CB (`M22`) | `co-so` | - Lãnh đạo đơn vị *(Đơn vị `BQLDA`)*<br>- Chi bộ *(Chi bộ `CB-BQLDA`)*<br>- Cấp ủy viên, Người được đánh giá |
| **Ban QLDA ĐT&XDCN** | `hoi.lx` | Lê Xuân Hội | Phó Giám đốc ĐVTT (`M26`), Phó BT CB (`M23`) | `co-so` | - Chi bộ *(Chi bộ `CB-BQLDA`)*, Người được đánh giá |
| **Ban QLDA ĐT&XDCN** | `nam.nt` | Nguyễn Thành Nam | Phó Giám đốc ĐVTT (`M26`) | `co-so` | - Người được đánh giá |
| **Xí nghiệp Chế tạo**<br>(`XNCT` / `CB-XNCT`) | `tuan.tq` | Thái Quốc Tuấn | Giám đốc ĐVTT (`M26`), Bí thư CB (`M22`) | `co-so` | - Lãnh đạo đơn vị *(Đơn vị `XNCT`)*<br>- Chi bộ *(Chi bộ `CB-XNCT`)*, Người được đánh giá |
| **Xí nghiệp Chế tạo** | `giang.lt` | Lê Trường Giang | Phó Giám đốc ĐVTT (`M26`), Phó BT CB (`M23`) | `co-so` | - Chi bộ *(Chi bộ `CB-XNCT`)*, Người được đánh giá |
| **Xí nghiệp Chế tạo** | `quang.hv` | Hoàng Văn Quang | Phó Giám đốc ĐVTT (`M26`) | `co-so` | - Người được đánh giá |
| **Chi nhánh TP.HCM**<br>(`CNHCM` / `CB-CNHCM`) | `tuan.ta` | Trần Anh Tuấn | Giám đốc ĐVTT (`M26`), Bí thư CB (`M22`) | `co-so` | - Lãnh đạo đơn vị *(Đơn vị `CNHCM`)*<br>- Chi bộ *(Chi bộ `CB-CNHCM`)*, Người được đánh giá |
| **Chi nhánh TP.HCM** | `hien.dt` | Đinh Thanh Hiền | Phó Giám đốc ĐVTT (`M26`), Phó BT CB (`M23`) | `co-so` | - Chi bộ *(Chi bộ `CB-CNHCM`)*, Người được đánh giá |
| **TT Bay KTHC**<br>(`TTBKT` / `CB-TTBKT`) | `thang.nm` | Nguyễn Minh Thắng | Trưởng trung tâm (`M26`), Đảng ủy viên (`M18`), Bí thư CB (`M22`) | `co-so` | - Lãnh đạo đơn vị *(Đơn vị `TTBKT`)*<br>- Chi bộ *(Chi bộ `CB-TTBKT`)*<br>- Cấp ủy viên, Người được đánh giá |
| **TT Bay KTHC** | `kien.nt` | Nguyễn Trung Kiên | Phó Trưởng trung tâm (`M26`), Phó BT CB (`M23`) | `co-so` | - Chi bộ *(Chi bộ `CB-TTBKT`)*, Người được đánh giá |
| **TT Huấn luyện CNS**<br>(`TTHL` / `CB-KTATCL`) | `loan.nt` | Nguyễn Tố Loan | Phó Trưởng trung tâm (`M26`) | `co-so` | - Người được đánh giá |
| **TT Bảo đảm kỹ thuật**<br>(`TTBDKT` / `DBBP`) | `tuan.nm` | Nguyễn Minh Tuấn | Quyền Trưởng trung tâm (`M26`, tại `TTBDKT`) | `co-so` | - Lãnh đạo đơn vị *(Đơn vị `TTBDKT`)*<br>- Người được đánh giá |
| **TT Bảo đảm kỹ thuật** | `hien.lv` | Lâm Văn Hiển | Phó Trưởng trung tâm (`M26`), Bí thư ĐBBP (`M20`), UV UBKT (`M19`) | `co-so` | - Chi bộ *(Đảng bộ bộ phận `DBBP`)*<br>- Người được đánh giá |
| **TT Bảo đảm kỹ thuật** | `vuong.dh` | Đỗ Hùng Vương | Phó Trưởng trung tâm (`M26`) | `co-so` | - Người được đánh giá |

### 4. Khối Đài trưởng, Đội trưởng, Trạm trưởng giữ chức vụ Bí thư, Phó Bí thư Chi bộ trực thuộc Đảng bộ bộ phận (`bi-thu-nhan-vien`)

| Tài khoản | Họ và tên | Chức vụ chính quyền & Đơn vị | Chức vụ Đảng & Chi bộ | Vai trò và Phạm vi phân công |
| :--- | :--- | :--- | :--- | :--- |
| `cuong.nt` | Nguyễn Tuấn Cường | Đội trưởng (`DOI-CNTT`) | Bí thư CB (`M24`, `CB-KTTH`) | Chi bộ (`CB-KTTH`), Người được đánh giá |
| `hung.nx` | Nguyễn Xuân Hưng | Đội trưởng (`DOI-HC`) | Phó Bí thư CB (`M25`, `CB-KTTH`) | Chi bộ (`CB-KTTH`), Người được đánh giá |
| `thuy.nt` | Nguyễn Thanh Thủy | Đội trưởng (`DOI-TSS`) | Bí thư CB (`M24`, `CB-KTCNS`) | Chi bộ (`CB-KTCNS`), Người được đánh giá |
| `kien.nd` | Nguyễn Đăng Kiên | Trạm trưởng (`TRAM-TS`) | Phó Bí thư CB (`M25`, `CB-KTCNS`) | Chi bộ (`CB-KTCNS`), Người được đánh giá |
| `trung.ha` | Hoàng Anh Trung | Đài trưởng (`DAI-DB`) | Bí thư CB (`M24`, `CB-DB`) | Chi bộ (`CB-DB`), Người được đánh giá |
| `ha.vm` | Vũ Mạnh Hà | Đài trưởng (`DAI-CB`) | Bí thư CB (`M24`, `CB-CB`) | Chi bộ (`CB-CB`), Người được đánh giá |
| `thang.nn` | Nguyễn Ngọc Thắng | Đài trưởng (`DAI-NH`) | Bí thư CB (`M24`, `CB-ND`) | Chi bộ (`CB-ND`), Người được đánh giá |
| `duong.nd` | Nguyễn Đình Dương | Đài trưởng (`DAI-TX`) | Phó Bí thư CB (`M25`, `CB-ND`) | Chi bộ (`CB-ND`), Người được đánh giá |
| `duong.nq` | Nguyễn Quang Dương | Đài trưởng (`DAI-VINH`) | Bí thư CB (`M24`, `CB-VINH`) | Chi bộ (`CB-VINH`), Người được đánh giá |
| `cuong.bd` | Bùi Đức Cường | Đài trưởng (`DAI-DH`) | Phó Bí thư CB (`M25`, `CB-VINH`) | Chi bộ (`CB-VINH`), Người được đánh giá |
| `thiet.la` | Lê Anh Thiết | Đài trưởng (`DAI-DN`) | Bí thư CB (`M24`, `CB-DN`) | Chi bộ (`CB-DN`), Người được đánh giá |
| `son.nv` | Nguyễn Văn Sơn | Đài trưởng (`DAI-CL`) | Phó Bí thư CB (`M25`, `CB-DN`) | Chi bộ (`CB-DN`), Người được đánh giá |
| `dung.pt` | Phạm Tiến Dũng | Đài trưởng (`DAI-PC`) | Bí thư CB (`M24`, `CB-PC`) | Chi bộ (`CB-PC`), Người được đánh giá |
| `thach.nnh` | Nguyễn Ngọc Hoàng Thạch | Đài trưởng (`DAI-TSN`) | Bí thư CB (`M24`, `CB-TSN`) | Chi bộ (`CB-TSN`), Người được đánh giá |
| `son.db` | Đặng Bá Sơn | Đài trưởng (`DAI-CR`) | Phó Bí thư CB (`M25`, `CB-TSN`) | Chi bộ (`CB-TSN`), Người được đánh giá |
| `son.vt` | Võ Thanh Sơn | Đài trưởng (`DAI-CT`) | Bí thư CB (`M24`, `CB-CT`) | Chi bộ (`CB-CT`), Người được đánh giá |
| `hung.tp` | Trần Phi Hùng | Đài trưởng (`DAI-BMT`) | Bí thư CB (`M24`, `CB-BMT`) | Chi bộ (`CB-BMT`), Người được đánh giá |
| `hau.bd` | Bùi Duy Hậu | Đài trưởng (`DAI-LK`) | Phó Bí thư CB (`M25`, `CB-BMT`) | Chi bộ (`CB-BMT`), Người được đánh giá |

---

## PHẦN V. QUY TRÌNH XỬ LÝ NGHIỆP VỤ KHI CÓ BIẾN ĐỘNG CÔNG TÁC CÁN BỘ

Khi có văn bản, quyết định về công tác cán bộ của cấp có thẩm quyền, Quản trị hệ thống (`quantri`) và Cơ quan thẩm định (`CO_QUAN_THAM_DINH`) thực hiện cập nhật trên hệ thống theo đúng các quy trình dưới đây:

### 1. Trường hợp điều động, bổ nhiệm cán bộ giữ chức vụ mới tại đơn vị mới
*(Ví dụ minh họa: Điều động, bổ nhiệm đồng chí Trần Thị Phương Loan — Trưởng phòng Tổ chức cán bộ - Lao động (`TCCB`) giữ chức vụ Trưởng Trung tâm Huấn luyện CNS (`TTHL`) kể từ ngày `01/12/2026`)*

1. **Cập nhật thông tin đơn vị và chức vụ bổ nhiệm** *(Thực hiện tại chức năng: **Hệ thống → Tài khoản**)*:
   - Mở hồ sơ tài khoản `loan.ttp`, cập nhật trường **Đơn vị chính quyền** sang `Trung tâm Huấn luyện CNS (TTHL)` và cập nhật **Tổ chức Đảng** theo quyết định chuyển sinh hoạt Đảng (nếu có).
   - Tại danh mục **Chức vụ**:
     - Cập nhật ngày kết thúc hiệu lực (**Đến hết ngày** = `01/12/2026`) đối với chức vụ *Trưởng phòng* tại `Phòng Tổ chức cán bộ - Lao động (TCCB)`.
     - Thêm mới bản ghi chức vụ *Trưởng trung tâm* tại đơn vị `Trung tâm Huấn luyện CNS (TTHL)`, đánh dấu là **Chức vụ chính**, thiết lập ngày bắt đầu hiệu lực (**Từ ngày** = `01/12/2026`) và ghi rõ số, ngày Quyết định bổ nhiệm tại trường **Ghi chú**.
2. **Cập nhật phân công vai trò nghiệp vụ** *(Thực hiện tại chức năng: **Hệ thống → Phân công vai trò**)*:
   - Kết thúc hiệu lực (**Đến hết ngày** = `01/12/2026`) đối với các vai trò gắn với chức trách tại đơn vị cũ (`TCCB`), bao gồm: *Lãnh đạo đơn vị* (phạm vi `TCCB`), *Cơ quan thẩm định* (phạm vi `Toàn công ty`), *Cơ quan chuyên môn* (phạm vi `TCCB`) và *Chi bộ* (phạm vi `CB-TCCB`, nếu thôi tham gia Chi ủy cũ).
   - Thêm mới phân công vai trò **Lãnh đạo đơn vị (`LANH_DAO_PHONG`)** tại phạm vi **`Trung tâm Huấn luyện CNS (TTHL)`** với ngày bắt đầu hiệu lực **Từ ngày** = `01/12/2026`.

---

### 2. Trường hợp phân công Lãnh đạo cấp trên phụ trách, chỉ đạo đơn vị khi khuyết cấp trưởng (không có quyết định bổ nhiệm kiêm nhiệm)
*(Ví dụ minh họa: Phân công đồng chí Nguyễn Như Thành — Phó Giám đốc Công ty trực tiếp phụ trách, điều hành Phòng Tổ chức cán bộ - Lao động (`TCCB`) kể từ ngày `01/12/2026`, không ban hành quyết định bổ nhiệm kiêm chức vụ Trưởng phòng)*

1. **Về thông tin chức vụ và đơn vị công tác** *(Chức năng: **Hệ thống → Tài khoản**)*:
   - **Giữ nguyên, không thay đổi:** Đơn vị công tác của đồng chí Nguyễn Như Thành (`thanh.nn`) vẫn thuộc Ban Giám đốc Công ty (`CTY`); chức vụ bổ nhiệm chính vẫn là *Phó Giám đốc* (`M14`).
   - Hệ quả nghiệp vụ: Hồ sơ đánh giá cá nhân của đồng chí Nguyễn Như Thành tiếp tục thực hiện theo đúng quy trình **Diện Phó Giám đốc (`pho-giam-doc`)**; chức danh in trên văn bản giữ nguyên là *Phó Giám đốc*; dữ liệu báo cáo thống kê cấp trưởng phòng (`M26`) và trần tỷ lệ xếp loại xuất sắc không bị biến động sai lệch.
2. **Về phân công vai trò thực hiện quy trình** *(Chức năng: **Hệ thống → Phân công vai trò**)*:
   - Bổ sung phân công vai trò cho tài khoản `thanh.nn` kể từ ngày `01/12/2026` (ghi rõ số văn bản phân công tại trường **Ghi chú**):
     - Vai trò **Lãnh đạo đơn vị (`LANH_DAO_PHONG`)** tại phạm vi **`Phòng Tổ chức cán bộ - Lao động (TCCB)`**: Thực hiện thẩm quyền phê duyệt danh mục sản phẩm, nhiệm vụ (Bước 1) và quản lý hồ sơ của cán bộ thuộc Phòng TCCB-LĐ.
     - Vai trò **Cơ quan thẩm định (`CO_QUAN_THAM_DINH`)** tại phạm vi **`Toàn công ty`**: Thực hiện thẩm quyền quản lý kỳ đánh giá, yêu cầu đối soát chuyên ngành, lập biên bản họp thống nhất và ký duyệt thẩm định hồ sơ (Bước 3b) của toàn Đảng bộ Công ty.
   - Khi cấp có thẩm quyền kiện toàn chức danh Trưởng phòng TCCB-LĐ mới, Quản trị hệ thống cập nhật ngày kết thúc hiệu lực (**Đến hết ngày**) vào hai bản ghi phân công vai trò nêu trên của tài khoản `thanh.nn`.

---

### 3. Trường hợp giao Quyền cấp trưởng đơn vị (`Quyền Trưởng phòng`, `Quyền Trưởng trung tâm`)
1. Tại mục **Chức vụ** của tài khoản cán bộ được giao Quyền (*Hệ thống → Tài khoản*):
   - Thêm mới chức vụ cấp trưởng tương ứng (*Trưởng phòng* / *Trưởng trung tâm*) tại đơn vị, đánh dấu chọn thuộc tính **"Quyền" (`IsActing`)** và **"Chức vụ chính" (`IsPrimary`)**, nhập ngày hiệu lực và căn cứ quyết định.
   - Hệ thống tự động hiển thị chức danh *"Quyền Trưởng phòng"* / *"Quyền Trưởng trung tâm"* trên các biểu mẫu và áp dụng quy tắc thống kê đối với người đứng đầu đơn vị (`IsHead = true`).
2. Tại mục **Phân công vai trò** (*Hệ thống → Phân công vai trò*):
   - Phân công vai trò **Lãnh đạo đơn vị (`LANH_DAO_PHONG`)** tại phạm vi đơn vị tương ứng theo thời hạn của quyết định giao Quyền.

---

### 4. Trường hợp ủy quyền giải quyết công việc có thời hạn
*(Áp dụng khi cấp trưởng đơn vị đi công tác, học tập hoặc nghỉ phép theo chế độ và có văn bản ủy quyền cho cấp phó thực hiện nhiệm vụ)*

1. Tại chức năng **Hệ thống → Phân công vai trò**, thêm mới phân công vai trò **Lãnh đạo đơn vị (`LANH_DAO_PHONG`)** cho tài khoản của cấp phó nhận ủy quyền tại phạm vi đơn vị đó.
2. Khai báo đầy đủ cả ngày bắt đầu (**Từ ngày**) và ngày kết thúc (**Đến hết ngày**) theo đúng thời hạn ghi trên văn bản ủy quyền.
3. Hệ thống tự động kích hoạt quyền khi đến thời điểm bắt đầu và tự động chấm dứt hiệu lực phân quyền ngay khi hết thời hạn ủy quyền.

---

### 5. Trường hợp biến động nhân sự phát sinh trong khi kỳ đánh giá đang mở (`Open`)
Khi cán bộ có quyết định điều chuyển đơn vị công tác trong thời gian kỳ đánh giá quý đang diễn ra, Cơ quan thẩm định (`CO_QUAN_THAM_DINH`) căn cứ phương án đánh giá đã được duyệt để xử lý trên kỳ đang mở như sau:

- **Phương án 1 — Đánh giá tại đơn vị cũ đối với kỳ hiện tại, chuyển sang đơn vị mới từ kỳ kế tiếp:**
  - Quản trị hệ thống cập nhật đơn vị công tác mới trên hồ sơ gốc của cán bộ theo Mục 1 Phần V.
  - Không thay đổi thông tin bản chụp (`Snapshot`) của cán bộ trong danh sách người tham gia của kỳ hiện tại; hồ sơ kỳ hiện tại tiếp tục được thực hiện và tổng hợp tại đơn vị cũ cho đến khi kết thúc kỳ.
- **Phương án 2 — Đánh giá tại đơn vị mới ngay trong kỳ đang mở:**
  - Cơ quan thẩm định truy cập vào chi tiết kỳ đánh giá (**Thiết lập → Kỳ đánh giá → thẻ Người tham gia**), chọn chức năng **Sửa thông tin hồ sơ** đối với cán bộ được điều chuyển:
    - Cập nhật lại **Phòng/Đơn vị**, **Chi bộ**, **Khung tỷ trọng** (nếu chưa qua bước tự chấm) và nhập căn cứ điều chỉnh.
    - Trường hợp chức vụ mới làm thay đổi quy trình đánh giá (ví dụ chuyển từ diện nhân viên sang diện cán bộ quản lý hoặc ngược lại), sử dụng chức năng **Đổi diện quyết định** trên dòng hồ sơ đó.
  - Đối với các nhiệm vụ công tác thay đổi do nhận nhiệm vụ tại đơn vị mới, cán bộ lập **Phiếu đề xuất điều chỉnh, bổ sung sản phẩm, nhiệm vụ (Mẫu 02)** trên hồ sơ để trình lãnh đạo đơn vị mới phê duyệt theo quy định.

---

### 6. Trường hợp cán bộ nghỉ chế độ (nghỉ hưu) hoặc chuyển công tác ra ngoài Công ty
1. **Trường hợp phát sinh sau khi kỳ đánh giá đã công bố kết quả:** Quản trị hệ thống thực hiện khóa trạng thái hoạt động của tài khoản (`IsActive = false`) và cập nhật ngày kết thúc hiệu lực chức vụ, vai trò. Toàn bộ hồ sơ đánh giá lịch sử của các kỳ trước được lưu trữ nguyên trạng.
2. **Trường hợp phát sinh ở đầu kỳ khi hồ sơ chưa phát sinh dữ liệu đánh giá:** Cơ quan thẩm định thực hiện loại bỏ cán bộ khỏi danh sách tham gia kỳ hiện tại (**Người tham gia → Bỏ khỏi danh sách**), sau đó Quản trị hệ thống khóa tài khoản người dùng.
