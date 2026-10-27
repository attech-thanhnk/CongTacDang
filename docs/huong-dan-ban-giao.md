# Hướng dẫn bàn giao — Đưa hệ thống vào sử dụng

Tài liệu dành cho **quản trị hệ thống** và **người quản lý kỳ** khi đưa hệ thống vào sử dụng và vận hành hằng kỳ. Mục tiêu: sau bàn giao, đơn vị tự khai báo, giao việc và xử lý vướng mắc mà không cần hỏi thêm.

Tài liệu liên quan:

- [Hướng dẫn sử dụng](huong-dan-su-dung.md): cách làm từng màn hình cho mọi nhóm người dùng.
- [Triển khai](deployment.md): cài đặt máy chủ, cấu hình, sao lưu (dành cho bộ phận CNTT).
- [Nhập dữ liệu từ Excel](nhap-du-lieu.md): nhập đơn vị, tổ chức Đảng, cán bộ, phân công vai trò hàng loạt.
- [Thuật ngữ thống nhất](thiet-ke/thuat-ngu.md): tên gọi chuẩn của biểu mẫu, bước, vai trò, quyền.

Tên nút và tên menu được đặt trong dấu ngoặc kép, đúng như trên màn hình.

**Mục lục**

1. [Ai làm gì ở bước nào](#1-ai-làm-gì-ở-bước-nào)
2. [Vai trò mặc định và cần bao nhiêu người](#2-vai-trò-mặc-định-và-cần-bao-nhiêu-người)
3. [Danh sách việc khi đưa vào sử dụng](#3-danh-sách-việc-khi-đưa-vào-sử-dụng)
4. [Khi hồ sơ bị kẹt](#4-khi-hồ-sơ-bị-kẹt)
5. [Lỗi thường gặp](#5-lỗi-thường-gặp)

---

## 1. Ai làm gì ở bước nào

Bảng dưới đây dùng vai trò mặc định (khi mới cài). **Quyền** là tên hiển thị trong "Ma trận quyền" của vai trò; việc nào cần quyền nào được quyết định bởi quyền chứ không bởi tên vai trò, nên đơn vị có thể tạo vai trò khác và chọn quyền tương ứng. Cột "Làm ở đâu" là đường đi trên màn hình của người thực hiện.

Một người **không** xét duyệt, lập hoặc xác nhận được trên hồ sơ của chính mình, dù có quyền.

### 1.1 Các bước của hồ sơ

| Bước | Người thực hiện (vai trò mặc định) | Quyền cần có | Làm ở đâu | Kết quả |
|---|---|---|---|---|
| Đăng ký sản phẩm, nhiệm vụ | Chính cán bộ (Người được đánh giá) | Tham gia đánh giá (bản thân) | "Việc cần xử lý" hoặc "Tổng quan", mở hồ sơ, nút "Nộp danh mục" | Chờ duyệt danh mục |
| Duyệt danh mục sản phẩm | Lãnh đạo đơn vị | Duyệt danh mục sản phẩm | Mở hồ sơ, nút "Duyệt" (hoặc "Trả lại danh mục") | Chờ tự chấm |
| Tự chấm điểm, đề xuất mức | Chính cán bộ | Tham gia đánh giá (bản thân) | Mở hồ sơ, "Lưu nháp", "Nộp phiếu" | Chờ Chi bộ xác nhận |
| Chi bộ xác nhận phiếu tự chấm | Chi bộ | Chi bộ xác nhận phiếu tự chấm | Mở hồ sơ, nút "Xác nhận" (hoặc "Trả lại phiếu tự chấm") | Chờ đề xuất tập thể |
| Đề xuất của tập thể lãnh đạo | Thư ký hội nghị | Ghi nhận đề xuất của tập thể lãnh đạo | Mở hồ sơ, nút "Ghi nhận đề xuất" (nên lập biên bản ở menu "Đánh giá tập thể" trước) | Chờ thẩm định |
| Thẩm định | Cơ quan thẩm định | Thẩm định | Mở hồ sơ, nút "Ghi nhận thẩm định" (hoặc "Trả lại để chủ hồ sơ sửa") | Chờ nhận xét của cấp trực tiếp sử dụng |
| Nhận xét của cấp trực tiếp sử dụng | Cấp trực tiếp sử dụng (Giám đốc, Chủ tịch). Diện nhân viên: Lãnh đạo đơn vị | Nhận xét của cấp trực tiếp sử dụng (diện nhân viên: Lãnh đạo đơn vị đề xuất) | Mở hồ sơ, nút "Ghi nhận nhận xét" | Chờ quyết định |
| Quyết định mức xếp loại | Văn phòng Đảng ủy | Ghi nhận quyết định của Đảng ủy cơ sở. Bước do cấp trên làm: Ghi nhận kết quả của cấp trên | Mở hồ sơ, nút "Ghi nhận quyết định" (hoặc "Ghi nhận kết quả") | Chờ công bố |
| Công bố và lưu trữ kết quả | Văn phòng Đảng ủy | Công bố và lưu trữ kết quả | Mở hồ sơ, nút "Công bố" | Đã công bố (khóa hồ sơ) |

Với bước do **cấp trên thực hiện** (ví dụ Diện BTV Đảng ủy Tổng công ty), người có quyền "Ghi nhận kết quả của cấp trên" nhập kết quả của cấp trên thay cho người làm bước đó.

### 1.2 Ba cổng trước khi ghi nhận quyết định

| Việc | Người thực hiện (vai trò mặc định) | Quyền cần có | Làm ở đâu | Kết quả |
|---|---|---|---|---|
| Yêu cầu xác nhận số liệu (Mẫu 03) | Cơ quan thẩm định | Yêu cầu đối soát chuyên ngành (Mẫu 03) | Mở hồ sơ, khối "Xác nhận số liệu", nút "Yêu cầu xác nhận" | Phiếu "Chờ lập phiếu"; nút "Thẩm định" bị khóa tới khi phiếu xong hoặc bị hủy |
| Lập phiếu xác nhận số liệu (Mẫu 03) | Cơ quan chuyên môn | Lập phiếu đối soát chuyên ngành | Menu "Xác nhận số liệu", mở phiếu, "Lưu kết quả", "Gửi xác nhận" | Phiếu "Chờ xác nhận" |
| Xác nhận phiếu số liệu (Mẫu 03) | Cơ quan chuyên môn (người khác người lập) | Xác nhận phiếu đối soát chuyên ngành | Mở phiếu, nút "Xác nhận" (hoặc "Trả lại" ở menu ⋯) | Phiếu "Đã xác nhận" |
| Lập biên bản hiệu chuẩn (Mẫu 20) | Cơ quan thẩm định | Lập biên bản hiệu chuẩn (Mẫu 20) | Menu "Rà soát", thẻ "Họp thống nhất", nút "Lập biên bản", "Lưu" | Biên bản "Đang soạn" |
| Chốt biên bản hiệu chuẩn (Mẫu 20) | Văn phòng Đảng ủy (người khác người lập) | Chốt biên bản hiệu chuẩn | Mở biên bản, nút "Chốt biên bản" | Biên bản "Đã chốt"; kết quả hiệu chuẩn áp vào hồ sơ |
| Lập phiếu kiểm soát (Mẫu 19) | Cơ quan thẩm định | Lập phiếu kiểm soát (Mẫu 19) | Menu "Rà soát", thẻ "Kiểm tra", nút "Lập phiếu", "Lưu", "Gửi xác nhận" | Phiếu "Chờ xác nhận" |
| Xác nhận phiếu kiểm soát (Mẫu 19) | Văn phòng Đảng ủy (người khác người lập) | Xác nhận phiếu kiểm soát | Mở phiếu, nút "Xác nhận" (hoặc "Trả lại", "Mở lại" ở menu ⋯) | Phiếu "Đã xác nhận"; cổng thông qua nếu kết luận "Đủ điều kiện trình phê duyệt" |

Mức độ bắt buộc của từng cổng đặt theo **diện quyết định** của kỳ (Bắt buộc / Chỉ ghi nhận / Tắt; xác nhận số liệu Tùy chọn / Tắt). Khi chưa chỉnh, họp thống nhất và kiểm tra là **Bắt buộc** với diện có bước Quyết định làm trong hệ thống, và **Chỉ ghi nhận** với diện do cấp trên quyết định. Cách chỉnh: [mục 3, bước 10](#3-danh-sách-việc-khi-đưa-vào-sử-dụng).

### 1.3 Quản lý kỳ, sau công bố, báo cáo

| Việc | Người thực hiện (vai trò mặc định) | Quyền cần có | Làm ở đâu |
|---|---|---|---|
| Tạo kỳ, cấu hình, mở kỳ, khóa dữ liệu, đóng kỳ | Cơ quan thẩm định | Quản lý kỳ đánh giá | Menu "Quản lý", "Kỳ đánh giá" |
| Quản lý bộ tiêu chí | Cơ quan thẩm định | Quản lý bộ tiêu chí | Menu "Quản lý", "Bộ tiêu chí" |
| Mở lại hồ sơ đã công bố | Văn phòng Đảng ủy | Mở lại hồ sơ đã khóa | Mở hồ sơ, nút "Mở lại hồ sơ" (nhập lý do) |
| Gửi kiến nghị về kết quả | Chính cán bộ | Gửi kiến nghị về kết quả | Hồ sơ đã công bố, khối "Kiến nghị" |
| Xử lý kiến nghị | Văn phòng Đảng ủy | Xử lý kiến nghị về kết quả | "Việc cần xử lý", nhóm "Kiến nghị chờ xử lý" |
| Lập, duyệt kế hoạch 30-60-90 ngày | Lãnh đạo đơn vị, Cấp trực tiếp sử dụng | Lập kế hoạch khắc phục 30-60-90 ngày | Hồ sơ đã công bố, khối "Kế hoạch 30-60-90 ngày (Mẫu 17)" |
| Xuất báo cáo | Cơ quan thẩm định, Cấp trực tiếp sử dụng, Cấp ủy viên, Văn phòng Đảng ủy | Xuất báo cáo tổng hợp | Menu "Báo cáo" |
| Quản trị tài khoản, vai trò, danh mục, thông tin đơn vị, biểu mẫu Word | Quản trị hệ thống | các quyền "Quản trị hệ thống" và "Quản lý danh mục" | Menu "Hệ thống" và "Quản lý" |

---

## 2. Vai trò mặc định và cần bao nhiêu người

Hệ thống tự tạo các vai trò dưới đây khi khởi động lần đầu (vai trò "Cơ quan chuyên môn" cũng được tự tạo nếu chưa có). Tên trong cột 1 là tên khi mới cài; quản trị có thể đổi tên ở "Vai trò", **Sửa** (đổi tên không ảnh hưởng việc phân quyền). Tên gọn khuyến nghị xem [thuật ngữ](thiet-ke/thuat-ngu.md), mục 4.

Một **phân công** gồm: người, vai trò, phạm vi (Toàn công ty / Đơn vị chính quyền / Tổ chức Đảng) và thời hạn. Phạm vi đơn vị gồm cả các đơn vị cấp dưới.

| Vai trò (tên khi mới cài) | Quyền chính | Việc điển hình | Gán cho ai, phạm vi | Tối thiểu |
|---|---|---|---|---|
| Người được đánh giá | Tham gia đánh giá (bản thân); xem kết quả đã công bố; gửi kiến nghị | Đăng ký sản phẩm, tự chấm, xem kết quả, kiến nghị | Mọi cán bộ có hồ sơ trong kỳ; Toàn công ty | Mọi người được đánh giá |
| Lãnh đạo đơn vị | Xem hồ sơ; duyệt danh mục sản phẩm; lãnh đạo đơn vị đề xuất; lập kế hoạch 30-60-90 ngày | Duyệt danh mục, đề xuất mức, lập kế hoạch khắc phục | Trưởng phòng, Phó Trưởng phòng; phạm vi Đơn vị chính quyền là Phòng đó | 2 người mỗi Phòng |
| Thư ký hội nghị | Xem hồ sơ; ghi nhận đề xuất tập thể; lập biên bản hội nghị | Ghi nhận kết quả hội nghị, lập Mẫu 12, 13 | Chuyên viên giữ vai trò thư ký; phạm vi Phòng (hoặc đơn vị tổ chức hội nghị) | 1 người mỗi nơi tổ chức hội nghị, nên thêm 1 dự phòng |
| Chi bộ | Xem hồ sơ; Chi bộ xác nhận phiếu tự chấm; lập hồ sơ tập thể | Xác nhận phiếu tự chấm, lập Mẫu 07 | Bí thư, Phó Bí thư Chi bộ; phạm vi Tổ chức Đảng là Chi bộ | 2 người mỗi Chi bộ |
| Cơ quan thẩm định | Xem hồ sơ; thẩm định; quản lý kỳ; quản lý bộ tiêu chí; xuất báo cáo; xem tài khoản; yêu cầu đối soát; lập biên bản hiệu chuẩn; lập phiếu kiểm soát | Mở kỳ, thẩm định, yêu cầu Mẫu 03, lập Mẫu 20 và Mẫu 19 | Cán bộ Phòng Tổ chức cán bộ - Lao động; chỉ gán được **Toàn công ty** (vì có quyền quản lý kỳ) | 2 người |
| Cấp trực tiếp sử dụng | Xem hồ sơ; nhận xét, đề xuất mức; xuất báo cáo; lập kế hoạch 30-60-90 ngày | Nhận xét ở bước 7, lập kế hoạch khắc phục | Giám đốc, Chủ tịch (và người thay thế); Toàn công ty | 1 người; thêm người dự phòng nếu có |
| Cấp ủy viên | Xem hồ sơ, biên bản; xuất báo cáo | Theo dõi, không có bước bắt buộc | Các Đảng ủy viên cần xem; Toàn công ty | Không bắt buộc |
| Văn phòng Đảng ủy | Xem hồ sơ; ghi nhận quyết định; ghi nhận kết quả của cấp trên; công bố; mở lại hồ sơ; xử lý kiến nghị; chốt biên bản hiệu chuẩn; xác nhận phiếu kiểm soát; xác nhận phiếu đối soát do cơ quan ngoài hệ thống; xuất báo cáo; lập biên bản hội nghị | Ghi nhận quyết định, công bố, chốt Mẫu 20, xác nhận Mẫu 19, xác nhận Mẫu 03 của cơ quan ngoài hệ thống | Cán bộ Văn phòng Đảng ủy; Toàn công ty | 2 người |
| Cơ quan chuyên môn | Lập phiếu đối soát chuyên ngành; xác nhận phiếu đối soát chuyên ngành | Lập và xác nhận phiếu Mẫu 03 gửi tới đơn vị mình | Cán bộ các phòng, đơn vị chuyên môn của ATTECH được yêu cầu đối soát (ví dụ Phòng Kỹ thuật - An toàn - Chất lượng, Phòng Tài chính - Kế toán); phạm vi Đơn vị chính quyền là **đơn vị đối soát** của từng phòng, đơn vị | 2 người mỗi đơn vị đối soát (người lập khác người xác nhận) |
| Quản trị hệ thống | Quản trị tài khoản, vai trò, phân công, nhật ký, thông tin đơn vị, biểu mẫu Word, nhập dữ liệu; quản lý danh mục | Khai báo và vận hành kỹ thuật; **không** xem nội dung đánh giá | Cán bộ CNTT; Toàn công ty | 2 người |

Vai trò "Cơ quan chuyên môn" không có quyền xem hồ sơ đánh giá: người giữ vai trò chỉ thấy các phiếu xác nhận số liệu gửi tới đơn vị mình (menu "Xác nhận số liệu").

### 2.1 Để luồng không bị kẹt

Các quy tắc sau là nguyên nhân kẹt hồ sơ thường gặp nhất. Hệ thống kiểm tra trước khi mở kỳ ([mục 3, bước 12](#3-danh-sách-việc-khi-đưa-vào-sử-dụng)), nhưng nên chuẩn bị đúng ngay từ khi phân công.

1. **Người duyệt khác chủ hồ sơ.** Trưởng phòng cũng là người được đánh giá, nên không duyệt danh mục của chính mình. Mỗi hồ sơ cần ít nhất một người *khác* có quyền ở bước đó trong phạm vi hồ sơ. Vì vậy mỗi Phòng nên có Trưởng phòng và Phó Trưởng phòng cùng giữ vai trò Lãnh đạo đơn vị; mỗi Chi bộ nên có cả Bí thư và Phó Bí thư giữ vai trò Chi bộ.
2. **Cơ quan thẩm định cần từ 2 người.** Nếu người đứng đầu cơ quan thẩm định cũng là người được đánh giá, hồ sơ của họ phải do người khác thẩm định.
3. **Người lập khác người xác nhận hoặc chốt.** Với Mẫu 19 và Mẫu 20 ở chế độ Bắt buộc, cần ít nhất **hai tài khoản khác nhau**: một người có quyền lập (mặc định cơ quan thẩm định), một người có quyền xác nhận hoặc chốt (mặc định Văn phòng Đảng ủy). Cả hai không phải chủ hồ sơ. Nếu cùng một người giữ cả hai vai trò mà không còn ai khác thì không qua được cổng.
4. **Mẫu 03 cũng cần hai người ở cơ quan chuyên môn:** người lập khác người xác nhận. Nếu xác nhận với cơ quan ngoài hệ thống, người yêu cầu nhập kết quả thay và **Văn phòng Đảng ủy xác nhận** (vai trò này có sẵn quyền xác nhận phiếu đối soát ở phạm vi Toàn công ty). Người xác nhận khác người nhập thay và không phải chủ hồ sơ.
5. **Người dự phòng.** Bước chỉ có một người làm được sẽ dừng khi người đó vắng. Hệ thống nhắc ở mục "Rà soát" khi kiểm tra sẵn sàng. Nên gán thêm người dự phòng, hoặc giao tạm có thời hạn (ô "Đến hết ngày").
6. **Quyền đúng phạm vi.** Quyền chỉ có tác dụng trong phạm vi được phân công. Người giữ vai trò Lãnh đạo đơn vị ở Phòng A không duyệt được hồ sơ của Phòng B.
7. **Vai trò có quyền quản lý kỳ, quản trị chỉ gán ở phạm vi Toàn công ty.**

---

## 3. Danh sách việc khi đưa vào sử dụng

Làm theo đúng thứ tự: bước sau dùng dữ liệu của bước trước. Mọi thao tác được ghi vào "Nhật ký hệ thống".

**0. Chuẩn bị**

- [ ] Máy chủ đã chạy (xem [Triển khai](deployment.md)). Đăng nhập bằng tài khoản quản trị ban đầu; hệ thống bắt đổi mật khẩu ngay.
- [ ] Chuẩn bị danh sách (giấy hoặc bảng tính): cây đơn vị chính quyền và tổ chức Đảng (mã, tên, đơn vị cấp trên, loại), cán bộ (tên đăng nhập, họ tên, đơn vị, tổ chức Đảng, chức vụ kể cả kiêm nhiệm), vai trò và phạm vi của từng người, danh sách người được đánh giá.
- [ ] Nên tạo ngay một quản trị thứ hai (hệ thống không cho tự gán hoặc gỡ vai trò của chính mình và luôn giữ ít nhất một quản trị).

**1. Thông tin đơn vị** (menu "Hệ thống", "Thông tin đơn vị")

- [ ] Kiểm tra và sửa: "Tên tổ chức Đảng cấp trên", "Tên Đảng bộ", "Tên đầy đủ của công ty", "Tên đơn vị chủ quản cấp trên", "Tên viết tắt", "Địa danh", "Cơ quan thẩm định", "Tên hiển thị của hệ thống". Bấm "Lưu". Thông tin này in trên mọi biểu mẫu và hiện trên giao diện, nên làm trước khi xuất bất kỳ biểu mẫu nào.

**2. Cây đơn vị và tổ chức Đảng** (menu "Quản lý", "Danh mục tổ chức")

- [ ] Thẻ "Loại đơn vị": rà các loại có sẵn (Đảng ủy, Đảng bộ bộ phận, Chi bộ; Công ty, Đơn vị, Phòng, Trung tâm, Xưởng, Đội, Đài, Trạm), thêm hoặc sửa cho đúng cơ cấu.
- [ ] Thẻ "Đơn vị chính quyền" rồi thẻ "Tổ chức Đảng": bấm "Thêm … gốc" trước, sau đó thêm đơn vị cấp dưới (chọn cấp trên và loại). Kiểm tra cây ở từng thẻ. Có nhiều đơn vị thì dùng "Nhập dữ liệu" (Excel): [Nhập dữ liệu từ Excel](nhap-du-lieu.md).
- [ ] Thẻ "Ngày nghỉ": khai báo ngày lễ, Tết, nghỉ bù của năm (dùng khi tính thời hạn các bước).

**3. Danh mục chức vụ** (cùng trang, thẻ "Chức vụ")

- [ ] Rà danh mục có sẵn (theo mã thống kê M1 đến M26). Thêm chức vụ còn thiếu: tên, bên (Đảng / chính quyền / đoàn thể), mã chức danh, thẩm quyền quyết định mặc định. Thẩm quyền của chức vụ quyết định cán bộ thuộc diện Đảng ủy cơ sở hay cấp trên quyết định.

**4. Tài khoản** (menu "Hệ thống", "Tài khoản")

- [ ] Bấm "Tạo tài khoản": tên đăng nhập, họ và tên, ngày sinh, "Đơn vị công tác chính", "Nơi sinh hoạt Đảng", khung tỷ trọng (để trống là dùng khung mặc định của bộ tiêu chí). Hệ thống hiện **mật khẩu tạm một lần**: ghi lại ngay để giao cho người đó.
- [ ] Mở chi tiết tài khoản, thẻ "Chức vụ", bấm "Thêm chức vụ" (nơi giữ chức vụ, từ ngày, đến hết ngày). Giữ nhiều chức vụ là kiêm nhiệm. Kiểm tra "Cấp quyết định xếp loại" suy ra đúng.
- [ ] Giao tên đăng nhập và mật khẩu tạm riêng cho từng người. Người dùng đăng nhập lần đầu phải đổi mật khẩu.

**5. Vai trò và phân công**

- [ ] Menu "Hệ thống", "Vai trò": xem các vai trò mặc định; mở "Ma trận quyền" để kiểm tra từng vai trò có đúng quyền ([mục 2](#2-vai-trò-mặc-định-và-cần-bao-nhiêu-người)). Nếu hệ thống nâng cấp từ bản cũ và đã sửa quyền của vai trò, kiểm tra vai trò đã có các quyền Mẫu 03, 19, 20 chưa.
- [ ] Vai trò **"Cơ quan chuyên môn"** (cho Mẫu 03) tự có khi backend khởi động, không cần tạo. Phân công theo từng đơn vị xác nhận (từng Ban).
- [ ] Phân công vai trò cho từng người bằng một trong ba cách: (a) trang chi tiết "Tài khoản", thẻ "Vai trò & phạm vi", nút "Phân công vai trò"; (b) trang chi tiết "Vai trò", thẻ "Người được gán", nút "Gán cho người"; (c) hàng loạt qua "Nhập dữ liệu", loại "Gán vai trò". Chọn "Loại phạm vi" và đơn vị đúng ([mục 2](#2-vai-trò-mặc-định-và-cần-bao-nhiêu-người)).
- [ ] Menu "Hệ thống", "Người thực hiện": chọn đơn vị để thấy từng việc của luồng đã có người chưa (nhãn "Chưa có người"), và dùng "Thêm người" để giao nhanh. Trang này **không** có việc Mẫu 03, 19, 20: phân công các vai trò đó ở "Tài khoản" hoặc "Vai trò".
- [ ] Kiểm tra vài người tiêu biểu (Bí thư Chi bộ, Lãnh đạo đơn vị, cơ quan thẩm định, Văn phòng Đảng ủy, Cơ quan chuyên môn): mở chi tiết "Tài khoản", thẻ "Người này làm được gì".

**6. Bộ tiêu chí** (menu "Quản lý", "Bộ tiêu chí")

- [ ] Hệ thống có sẵn một bộ mặc định đã xuất bản. Rà nội dung (tiêu chí chung, trục kết quả, khung tỷ trọng, mức xếp loại, tham số). Cần điều chỉnh: "Nhân bản", sửa bản nháp, "Lưu", "Xuất bản". Bộ đã xuất bản không sửa được. Nghiệp vụ nên xác nhận nội dung trước khi mở kỳ.

**7. Biểu mẫu Word** (menu "Hệ thống", "Biểu mẫu Word")

- [ ] Kiểm tra từng biểu mẫu đang dùng file gốc hay phiên bản đã tải lên; tải thử một vài mẫu. Cần sửa bố cục: tải file đang dùng về, sửa trong Word giữ nguyên các ô dữ liệu (tag), "Kiểm tra", "Tải lên".

**8. Tạo kỳ** (menu "Quản lý", "Kỳ đánh giá")

- [ ] Bấm "Tạo kỳ": năm, quý, từ ngày, đến ngày, tên kỳ, bộ tiêu chí đã xuất bản. Kỳ ở trạng thái Dự thảo.

**9. Chọn bộ tiêu chí cho kỳ**

- [ ] Thẻ "Bộ tiêu chí": nếu chưa chọn, chọn ở "Chọn bộ đã xuất bản" rồi "Dùng bộ này". Đã mở kỳ thì không đổi được.

**10. Diện quyết định và cổng** (thẻ "Diện quyết định"; **chỉ sửa được khi kỳ còn Dự thảo**)

- [ ] Hai ô "Diện quyết định mặc định": chọn diện cho người thuộc diện Đảng ủy cơ sở quyết định và diện cấp trên quyết định.
- [ ] Kỳ mới có sẵn bốn diện: "Diện Đảng ủy cơ sở", "Diện BTV Đảng ủy Tổng công ty", "Bí thư/Phó bí thư Chi bộ là nhân viên", "Phó Giám đốc". Với mỗi diện: chế độ từng bước ("Nội bộ", "Cấp trên thực hiện", "Không áp dụng"), quyền thực hiện, thời hạn.
- [ ] Khối "Cổng kiểm soát trước quyết định": chọn chế độ cho từng diện.
  - "Xác nhận số liệu": "Tùy chọn" hoặc "Tắt".
  - "Họp thống nhất" và "Kiểm tra trước quyết định": "Bắt buộc", "Chỉ ghi nhận" hoặc "Tắt".
  - Chọn "Bắt buộc" chỉ khi đã có đủ người lập và người xác nhận ([mục 2.1](#21-để-luồng-không-bị-kẹt)). Chưa sẵn sàng thì để "Chỉ ghi nhận": hồ sơ không bị chặn nhưng vẫn hiện nhắc.
- [ ] Bấm "Tính lại thời hạn" để điền ngày theo quy tắc (cần đã khai báo ngày nghỉ), kiểm tra lại từng ngày. Ô "Chặn hoàn thành bước khi đã quá thời hạn": bật nếu muốn khóa bước khi quá hạn. Bấm **"Lưu"**.

**11. Người được đánh giá** (thẻ "Người được đánh giá")

- [ ] Bấm "Thêm người": thêm cả đơn vị ("Thêm cả đơn vị") hoặc từng người ("Tìm cán bộ"). Để trống "Diện quyết định" là hệ thống chọn theo cấp quyết định (và gợi ý theo chức vụ).
- [ ] Kiểm tra cột "Khung" và "Diện quyết định" từng người; nút "Sửa" để chỉnh Phòng, Chi bộ, khung tỷ trọng, cấp quyết định (bắt buộc lý do), "Đổi diện quyết định" để đổi diện.

**12. Kiểm tra sẵn sàng**

- [ ] Xem khối "Chuẩn bị mở kỳ" ở đầu trang kỳ: bộ tiêu chí, người được đánh giá, khung tỷ trọng, thời hạn, người thực hiện. Mục chưa xong có nút đi thẳng tới nơi xử lý.
- [ ] Mở thẻ "Người thực hiện các bước", bấm "Kiểm tra lại", đọc các cảnh báo (bảng dưới) và xử lý hết trước khi mở kỳ.

| Cảnh báo | Nghĩa là | Cách xử lý |
|---|---|---|
| "… sẽ kẹt ở bước "…" vì không có tài khoản đang hoạt động nào (ngoài chủ hồ sơ) có quyền "…" trong phạm vi …" | Bước đó chưa có ai làm được ngoài chủ hồ sơ | Bấm tên nơi phân công, thêm người có vai trò phù hợp; hoặc đổi chế độ bước của diện quyết định |
| "… vì tài khoản của chủ hồ sơ không hoạt động hoặc không có quyền "Tham gia đánh giá (bản thân)"" | Chủ hồ sơ không đăng nhập được hoặc thiếu vai trò Người được đánh giá | Mở "Mở lại tài khoản" nếu bị vô hiệu hóa; phân công vai trò Người được đánh giá |
| "… sẽ kẹt trước bước "Quyết định mức xếp loại" vì cổng "…" bắt buộc nhưng không có hai tài khoản đang hoạt động khác nhau (ngoài chủ hồ sơ) để lập và xác nhận…" | Cổng Bắt buộc mà chỉ có 0 hoặc 1 người làm được (hoặc một người giữ cả hai việc) | Phân công thêm người cho quyền còn thiếu; hoặc đổi cổng sang "Chỉ ghi nhận" |
| "… chưa có khung tỷ trọng A-B-C-D …" hoặc "Khung tỷ trọng "…" không có trong bộ tiêu chí …" | Hồ sơ không có khung hợp lệ nên không tự chấm được | Thẻ "Người được đánh giá", "Sửa", chọn khung. **Lỗi này chặn mở kỳ, kể cả "Mở kỳ bắt buộc"** |
| "N hồ sơ dùng diện quyết định khác diện gợi ý theo chức vụ" (mục "Rà soát") | Chỉ nhắc, không chặn | Xem danh sách; đổi diện quyết định nếu nhầm |
| "N người đang ghi là sinh hoạt thẳng ở Đảng bộ / Đảng bộ bộ phận, không thuộc chi bộ nào" ("Rà soát") | Chỉ nhắc | Sửa "Nơi sinh hoạt Đảng" ở chi tiết "Tài khoản" (hồ sơ tự cập nhật khi kỳ còn Dự thảo) |
| "N hồ sơ thiếu Phòng hoặc Chi bộ" ("Rà soát") | Chỉ nhắc; thiếu đơn vị thì quyền theo đơn vị khó áp đúng | Bổ sung "Đơn vị công tác chính" hoặc "Nơi sinh hoạt Đảng" ở "Tài khoản" |
| "N bước chỉ có một người làm được" ("Rà soát") | Người đó vắng thì hồ sơ phải chờ | Gán thêm người dự phòng (ví dụ Phó Bí thư Chi bộ) |
| "N bước chưa có thời hạn" | Không chặn mở kỳ, nhưng không theo dõi được quá hạn | Thẻ "Diện quyết định", đặt thời hạn và "Lưu" |

**13. Mở kỳ**

- [ ] Bấm "Mở kỳ" ở đầu trang kỳ, xác nhận ("Sau khi mở kỳ chỉ sửa được thời hạn các bước; không đổi được diện quyết định và bộ tiêu chí"). Nút mờ khi chưa chọn bộ tiêu chí hoặc chưa có người được đánh giá.
- [ ] Còn cảnh báo kẹt luồng: hệ thống không mở và chuyển sang thẻ "Người thực hiện các bước". Chỉ dùng "Mở kỳ bắt buộc" (nhập lý do) khi chắc chắn sẽ bổ sung người kịp thời.
- [ ] Sau khi mở: mỗi người thấy việc của mình ở "Việc cần xử lý". Theo dõi ở thẻ "Tiến độ" (đếm hồ sơ theo bước, danh sách quá hạn, sắp đến hạn).

**14. Trong và cuối kỳ**

- [ ] Hoàn tất biên bản hiệu chuẩn (Mẫu 20) và phiếu kiểm soát (Mẫu 19) **khi kỳ còn Đang mở**, trước khi "Khóa dữ liệu" ở menu ⋯ của trang kỳ. Khi kỳ đã khóa, biên bản hiệu chuẩn không lập hoặc chốt được; cần "Mở lại kỳ" (nhập lý do).
- [ ] "Đóng kỳ" chỉ được khi mọi hồ sơ đã công bố.
- [ ] Kiểm tra sau cùng: xuất thử "Mẫu 14" và "Danh sách cán bộ" ở menu "Báo cáo", và một biểu mẫu của hồ sơ; tên Đảng bộ, công ty, địa danh, tên tệp đúng "Thông tin đơn vị".

---

## 4. Khi hồ sơ bị kẹt

Trước hết mở hồ sơ, đọc thanh tiến trình: bước đang chờ và "Chờ: …" ai. Rồi chọn tình huống phù hợp.

| Tình huống | Cách xử lý | Ai làm |
|---|---|---|
| Người phụ trách bước vắng mặt, nghỉ phép | Phân công thêm một người cùng vai trò trong cùng phạm vi: "Hệ thống", "Người thực hiện", chọn đơn vị, "Thêm người" ở đúng việc; hoặc "Tài khoản", thẻ "Vai trò & phạm vi", "Phân công vai trò" (đặt "Đến hết ngày" nếu chỉ giao tạm) | Quản trị hệ thống |
| Người phụ trách đã luân chuyển, thôi chức vụ | Gỡ phân công cũ ("Kết thúc" hoặc "Gỡ khỏi việc này") và phân công người mới. Màn hình "Người thực hiện" có khung "Cần xem lại" gom các trường hợp này | Quản trị hệ thống |
| Không có ai làm được bước (thiếu người) | Thẻ "Người thực hiện các bước" của kỳ cho biết bước nào, vai trò nào, phạm vi nào; bấm tên nơi phân công để thêm người | Quản trị hệ thống |
| Người làm bước là chủ hồ sơ (ví dụ Trưởng phòng duyệt danh mục của chính mình) | Phân công thêm người khác có quyền ở phạm vi hồ sơ (Phó Trưởng phòng, hoặc người ở đơn vị cấp trên); [mục 2.1](#21-để-luồng-không-bị-kẹt) | Quản trị hệ thống |
| Hồ sơ gán nhầm diện quyết định | Thẻ "Người được đánh giá", dòng của người đó, "Đổi diện quyết định", nhập lý do (đổi hàng loạt bằng cách tích nhiều dòng). Chỉ đổi được khi hồ sơ chưa qua bước bị ảnh hưởng | Người quản lý kỳ |
| Hồ sơ gán nhầm khung tỷ trọng, Phòng, Chi bộ, cấp quyết định | Thẻ "Người được đánh giá", "Sửa", nhập lý do. Danh mục đã duyệt thì trả hồ sơ về bước tự chấm trước khi đổi khung | Người quản lý kỳ |
| Người không còn công tác trong kỳ | Thẻ "Người được đánh giá", menu của dòng, "Bỏ khỏi kỳ" (hồ sơ của người đó trong kỳ bị xóa) | Người quản lý kỳ |
| Phiếu tự chấm, danh mục cần sửa lại | Người ở bước sau bấm "Trả lại …" kèm lý do; hồ sơ quay về chủ hồ sơ | Người ở bước sau |
| Cần đính chính hồ sơ đã công bố | Bấm "Mở lại hồ sơ", chọn bước quay về, nhập lý do | Văn phòng Đảng ủy |
| Kỳ đã khóa dữ liệu mà cần sửa bước trước Thẩm định | "Mở lại kỳ" (menu ⋯ trang kỳ) hoặc chỉ mở riêng một hồ sơ: trả lại hoặc mở lại hồ sơ và chọn "Mở riêng hồ sơ đến" (tối đa 60 ngày); hết hạn thì "Gia hạn mở riêng hồ sơ" | Người quản lý kỳ; người có quyền trả lại hoặc mở lại hồ sơ |
| Bước quá hạn bị chặn ("Chặn hoàn thành bước khi đã quá thời hạn" đang bật) | Thẻ "Diện quyết định", đặt lại thời hạn của bước, "Lưu" (sửa được sau khi mở kỳ). Ô chặn chỉ đổi được khi kỳ còn Dự thảo | Người quản lý kỳ |
| Nút "Ghi nhận quyết định" bị khóa vì Mẫu 20 hoặc Mẫu 19 | Xem bảng [mục 12.1 của hướng dẫn sử dụng](huong-dan-su-dung.md#121-nút-có-nhưng-không-bấm-được-dải-vàng-có-biểu-tượng-khóa); lập, chốt, xác nhận hoặc mở lại phiếu theo lý do ghi | Cơ quan thẩm định, Văn phòng Đảng ủy |
| Người lập Mẫu 20 hoặc Mẫu 19 là người duy nhất có quyền xác nhận hoặc chốt | Phân công quyền xác nhận hoặc chốt cho người thứ hai (phạm vi bao trùm các hồ sơ trong biên bản hoặc phiếu). Tạm thời có thể đổi cổng sang "Chỉ ghi nhận" nếu kỳ còn Dự thảo | Quản trị hệ thống; người quản lý kỳ |
| Phiếu hoặc biên bản của người đã nghỉ còn dở | Biên bản "Đang soạn": người khác có quyền lập mở và sửa tiếp (người chốt vẫn phải khác người đã lập ban đầu); không cần thì "Hủy biên bản". Phiếu kiểm soát: "Hủy phiếu" rồi lập phiếu mới | Cơ quan thẩm định |
| Phiếu xác nhận số liệu chờ mãi, cơ quan chuyên môn không phản hồi | Người yêu cầu mở phiếu, menu ⋯, "Hủy yêu cầu", nhập lý do: nút "Thẩm định" mở lại | Cơ quan thẩm định |
| Phiếu kiểm soát hoặc biên bản bị "Hết hiệu lực" sau khi hồ sơ đổi | Mở lại, kiểm lại, xác nhận hoặc chốt lại (hướng dẫn sử dụng, [mục 5.5](huong-dan-su-dung.md#55-hết-hiệu-lực-nghĩa-là-gì-và-làm-gì-tiếp)) | Người lập và người xác nhận |
| Tài khoản người làm bước bị vô hiệu hóa hoặc bị khóa | "Tài khoản", mở chi tiết, "Mở lại tài khoản" hoặc "Mở khóa đăng nhập" | Quản trị hệ thống |
| Cần xem ai làm gì, lúc nào | Trang hồ sơ, khối "Lịch sử"; hoặc "Nhật ký hệ thống" | Người có quyền xem |

---

## 5. Lỗi thường gặp

| Triệu chứng | Nguyên nhân | Cách xử lý |
|---|---|---|
| Đăng nhập báo "Tài khoản đang bị khóa tạm thời do nhập sai mật khẩu nhiều lần…" | Nhập sai mật khẩu 5 lần liên tiếp; tài khoản khóa tạm 15 phút | Chờ hết thời gian khóa, hoặc quản trị mở chi tiết "Tài khoản", bấm "Mở khóa đăng nhập" (xóa số lần nhập sai) |
| Báo "Tài khoản đã bị khóa bởi quản trị hệ thống…" | Tài khoản bị vô hiệu hóa | Quản trị mở chi tiết "Tài khoản", menu ⋯, "Mở lại tài khoản" |
| Người dùng quên mật khẩu | | Quản trị mở chi tiết "Tài khoản", menu ⋯, "Đặt lại mật khẩu": hệ thống hiện mật khẩu tạm **một lần**, ghi lại và giao cho người đó |
| Mật khẩu tạm không dùng được, hoặc bị buộc đổi mật khẩu | Mật khẩu tạm chỉ dùng cho lần đăng nhập đầu; sau đó hệ thống bắt đổi (tối thiểu 8 ký tự, có cả chữ và số) | Đăng nhập bằng mật khẩu tạm, đặt mật khẩu mới ở trang "Đổi mật khẩu". Mất mật khẩu tạm thì "Đặt lại mật khẩu" lần nữa |
| Mất mật khẩu quản trị, không còn quản trị nào đăng nhập được | Mật khẩu đã đổi không còn ai nhớ và không có quản trị thứ hai | Người vận hành chạy trên máy chủ: `cd /opt/congtacdang/docker && ./dat-lai-mat-khau.sh <tên-đăng-nhập>`. Script in mật khẩu tạm **một lần**, bắt đổi mật khẩu ở lần đăng nhập đầu, mở khóa đăng nhập và đăng xuất mọi phiên của đúng tài khoản đó (hiệu lực trong vòng một phút), rồi ghi một dòng vào nhật ký. Thử trước bằng `--kiem-tra` (không đổi gì). Tài khoản bị tắt thì script cảnh báo và không bật lại. Chi tiết: [trien-khai-may-moi.md](trien-khai-may-moi.md#đặt-lại-mật-khẩu-quản-trị-bằng-dòng-lệnh) |
| Báo "Bạn không có quyền … trên đối tượng này (ngoài phạm vi được gán)" hoặc "Bạn không có quyền … để thực hiện thao tác này" (403) | Người dùng thiếu quyền, hoặc có quyền nhưng ở phạm vi khác đơn vị của hồ sơ | Quản trị mở chi tiết "Tài khoản", thẻ "Người này làm được gì", đối chiếu; phân công vai trò đúng phạm vi |
| Báo "Bạn không được thực hiện "…" trên hồ sơ của chính mình (xung đột lợi ích)" | Người dùng đang làm bước xét duyệt trên hồ sơ của mình | Không xử lý được bằng quyền; người khác làm bước đó |
| Báo "Bạn chưa được cấp quyền "…". Hãy liên hệ quản trị hệ thống" (màn hình "Không có quyền truy cập") | Trang yêu cầu quyền người dùng chưa có | Phân công vai trò có quyền đó |
| Không thấy mục menu cần dùng | Menu hiện theo quyền ([hướng dẫn sử dụng, mục 1.1](huong-dan-su-dung.md#11-menu)) | Kiểm tra "Người này làm được gì". Vừa phân công mà menu chưa đổi: tải lại trang hoặc đăng xuất rồi đăng nhập lại |
| Cán bộ không thấy hồ sơ của mình | Chưa được thêm vào kỳ, hoặc đang chọn nhầm kỳ ở ô "Kỳ đánh giá" | Thẻ "Người được đánh giá" của kỳ, "Thêm người"; hoặc chọn đúng kỳ |
| Tải PDF báo "Máy chủ chưa cài LibreOffice nên chưa xuất được PDF…" (lỗi 503) | Máy chủ chưa có LibreOffice | Người dùng tải bản Word hoặc Excel; CNTT cài LibreOffice theo mục "LibreOffice (xuất PDF)" của [Triển khai](deployment.md) |
| Biểu mẫu thiếu hoặc sai họ tên, ngày sinh, chức vụ, tên Đảng bộ | Dữ liệu nguồn thiếu | Ngày sinh, chức vụ: sửa ở chi tiết "Tài khoản". Tên Đảng bộ, công ty, địa danh: sửa ở "Thông tin đơn vị" |
| Biểu mẫu Word báo "Thiếu n tag so với file gốc" | File mẫu đang dùng thiếu ô dữ liệu | "Biểu mẫu Word": tải file gốc, sửa lại rồi tải lên; hoặc "Dùng file gốc" |
| Nút thao tác không hiện, kỳ ở trạng thái "Dự thảo", "Khóa dữ liệu" hoặc "Đã đóng" | Kỳ không cho thao tác | "Mở kỳ" (Dự thảo); "Mở lại kỳ" (Khóa dữ liệu); "Mở lại hồ sơ" (Đã đóng) |
| Báo "Đã quá thời hạn của bước "…". Liên hệ người quản lý kỳ để gia hạn." | Kỳ bật "Chặn hoàn thành bước khi đã quá thời hạn" | Đặt lại thời hạn của bước ở thẻ "Diện quyết định" |
| Báo "Hồ sơ đã được người khác cập nhật sau khi bạn mở…" | Hai người cùng sửa một hồ sơ, hoặc mở hai cửa sổ | Bấm "Tải lại", làm lại thao tác |
| Không mở được kỳ | Chưa chọn bộ tiêu chí, chưa có người được đánh giá, còn bước chưa có người thực hiện, hoặc hồ sơ thiếu khung tỷ trọng | Làm theo khối "Chuẩn bị mở kỳ" và thẻ "Người thực hiện các bước" |
