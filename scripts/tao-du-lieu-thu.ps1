<#
  Tạo nhanh dữ liệu thử trên máy phát triển: một kỳ Quý IV/2026 với 7 hồ sơ (đủ các diện quyết định và một người Chưa xếp loại), đẩy mọi hồ sơ tới giai đoạn chọn
  rồi dừng để tự thao tác tiếp trên giao diện. Chỉ gọi API (đi qua kiểm tra quyền và luật luồng), không ghi thẳng CSDL.
  Ví dụ:
    powershell -File scripts/tao-du-lieu-thu.ps1 -GiaiDoan tham-dinh
    powershell -File scripts/tao-du-lieu-thu.ps1 -GiaiDoan cong-bo     # đã có kỳ thử thì đẩy tiếp các hồ sơ đang có
    powershell -File scripts/tao-du-lieu-thu.ps1 -LamSach
  Giai đoạn (hồ sơ dừng ở bước này, chờ người làm tiếp):
    dang-ky, gui-danh-muc, ra-soat, duyet, tu-cham, chi-bo, tap-the, tham-dinh, nhan-xet, quyet-dinh, cong-bo
    xong (công bố luôn mọi hồ sơ)
  Thử Mẫu 17: -MucQuyetDinh @{ 'trung.nb' = 'HoanThanh'; 'cuong.nt' = 'KhongHoanThanh' }
  Mỗi bước dùng tài khoản đầu tiên trong -TaiKhoan có nút thao tác trên hồ sơ (theo API actions).
  Kỳ đã có sẵn trên máy chủ (không tạo kỳ, không thêm người, không đánh dấu Chưa xếp loại; hỏi mật khẩu khi chạy, mọi tài khoản dùng chung):
    powershell -File scripts/tao-du-lieu-thu.ps1 -KyCoSan -ApiUrl http://192.168.22.149:3001 -Nam 2027 -Quy 1 -GiaiDoan tu-cham -GiuLai trung.nb,dao.nta
  -TaoKy: chưa có kỳ của -Nam/-Quy thì tạo, thêm mọi cán bộ giữ chức vụ thuộc diện đánh giá, mở kỳ rồi đẩy như -KyCoSan; đã có kỳ thì dùng kỳ đó.
    powershell -File scripts/tao-du-lieu-thu.ps1 -TaoKy -ApiUrl http://192.168.22.149:3001 -Nam 2027 -Quy 1 -GiaiDoan dang-ky
  -GiuLai: hồ sơ để nguyên, tự thao tác trên giao diện.
  -LamSach làm rỗng các bảng vận hành (kỳ, hồ sơ, biên bản, phiếu, nhật ký) của CSDL thử qua docker; tài khoản, danh mục giữ nguyên.
#>
param(
    [ValidateSet('dang-ky', 'gui-danh-muc', 'ra-soat', 'duyet', 'tu-cham', 'chi-bo', 'tap-the', 'tham-dinh', 'nhan-xet', 'quyet-dinh', 'cong-bo', 'xong')]
    [string]$GiaiDoan,
    # Dùng kỳ đang có của -Nam/-Quy với toàn bộ người trong kỳ
    [switch]$KyCoSan,
    # Tạo kỳ mới cho -Nam/-Quy (từ chối nếu đã có) rồi làm như -KyCoSan
    [switch]$TaoKy,
    [int]$Nam = 2026,
    [int]$Quy = 4,
    [string[]]$GiuLai = @(),
    # Thêm trả lại, thu hồi, điều chỉnh việc, ý kiến, Chưa xếp loại, mở lại trên vài hồ sơ diện cơ sở
    [switch]$TinhHuong,
    [switch]$LamSach,
    [string]$ApiUrl = 'http://localhost:5000',
    # Thử lần lượt, cấp gần nhất trước để đúng thực tế (thư ký Văn phòng ghi đề xuất); giang.nh: Giám đốc duyệt danh mục; tuan.nm: Trưởng trung tâm của cuong.nt; hung.nx: Chi bộ của cuong.nt; huyen.utt: Chi bộ của thanh.nn, da.dc
    [string[]]$TaiKhoan = @('duy.lm', 'dao.nta', 'trung.nb', 'tuan.nm', 'hung.nx', 'huyen.utt', 'long.nc', 'loan.ttp', 'loan.ctp', 'hung.dt', 'giang.nh', 'thinh.lt', 'thanh.nn'),
    # Bỏ trống: máy phát triển dùng mật khẩu thử, nơi khác hỏi khi chạy
    [string]$MatKhau,
    # CSDL mà backend dev đang dùng; -LamSach chỉ chạy trên CSDL thử
    [string]$CsdlDev = 'congtacdang_dev',
    [string]$PgContainer = 'attech-dangbo-postgres',
    # Mức quyết định riêng theo tài khoản (mặc định HoanThanhTot), để thử Mẫu 17 với người xếp loại thấp
    [hashtable]$MucQuyetDinh = @{}
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

if (-not $LamSach -and -not $GiaiDoan) {
    Write-Host 'Hãy chọn -GiaiDoan <dang-ky|tu-cham|tap-the|tham-dinh|nhan-xet|quyet-dinh|cong-bo|xong> hoặc -LamSach.' -ForegroundColor Yellow
    exit 1
}

$IsLocal = $ApiUrl -match '^https?://(localhost|127\.0\.0\.1)(:|/|$)'
# Tạo kỳ, thêm người, đánh dấu Chưa xếp loại chỉ làm trên máy phát triển
if ($TaoKy -and $LamSach) {
    Write-Host 'Không dùng -TaoKy cùng -LamSach.' -ForegroundColor Red
    exit 1
}
if (-not $IsLocal -and -not $KyCoSan -and -not $TaoKy -and -not $LamSach) {
    Write-Host 'Ngoài máy phát triển chỉ chạy được với -KyCoSan (dùng kỳ đã mở) hoặc -TaoKy (tạo kỳ mới).' -ForegroundColor Red
    exit 1
}
if (-not $IsLocal -and $LamSach) {
    Write-Host '-LamSach chỉ dùng trên máy phát triển.' -ForegroundColor Red
    exit 1
}
$ShowPassword = $false
# Cửa sổ chạy script truyền mật khẩu qua biến môi trường, không qua dòng lệnh
if (-not $MatKhau -and $env:CTD_MATKHAU) { $MatKhau = $env:CTD_MATKHAU }
if (-not $MatKhau -and -not $LamSach) {
    if ($IsLocal) { $MatKhau = 'Thu12345a'; $ShowPassword = $true }
    else {
        $secure = Read-Host 'Mật khẩu chung của các tài khoản' -AsSecureString
        $MatKhau = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
    }
}

$Year = $Nam
$Quarter = $Quy
$PeriodName = "Đánh giá, xếp loại cán bộ Quý $(@('I', 'II', 'III', 'IV')[$Quy - 1])/$Nam"
$ManagerUser = 'loan.ttp'
# Mỗi diện quyết định một trường hợp: cơ sở (Trưởng, Phó phòng), Phó Giám đốc, Bí thư chi bộ là nhân viên, cấp trên; nghia.tv: Chưa xếp loại
$Evaluated = [ordered]@{
    'duy.lm' = 'co-so'; 'dao.nta' = 'co-so'; 'trung.nb' = 'co-so'; 'thanh.nn' = 'pho-giam-doc'
    'cuong.nt' = 'bi-thu-nhan-vien'; 'da.dc' = 'cap-tren'; 'nghia.tv' = 'co-so'
}
$NotRated = @{ 'nghia.tv' = 'Đi đào tạo dài hạn trong quý' }
if ($KyCoSan -or $TaoKy) { $Evaluated = [ordered]@{}; $NotRated = @{} }
# Chủ hồ sơ tự đăng ký, tự chấm nên phải có trong danh sách tài khoản (trừ người Chưa xếp loại)
$Accounts = @(@($TaiKhoan) + @($Evaluated.Keys | Where-Object { -not $NotRated.ContainsKey($_) }) | Select-Object -Unique)
# Không lập, không chốt biên bản có hồ sơ của chính mình
$CalibrationPreparers = @('loan.ctp', 'loan.ttp')
$CalibrationConcluders = @('long.nc', 'hung.dt', 'thao.nt')
# Lập biên bản họp tập thể bước 3a (vai trò Thư ký hội nghị phạm vi toàn Công ty)
$MeetingSecretaries = @('hung.dt', 'long.nc', 'thao.nt', 'loan.ttp')
$script:Meetings = @{}
$Steps = @('B1_REGISTER', 'B1_UNIT_SUBMIT', 'B1_HR_REVIEW', 'B1_APPROVE', 'B2_SELF_SCORE', 'B2_CELL_CONFIRM', 'B3A_COLLECTIVE', 'B3B_APPRAISAL', 'B3C_DIRECTOR', 'B4_DECISION', 'B5_PUBLISH')
$StageStep = @{
    'dang-ky' = 'B1_REGISTER'; 'gui-danh-muc' = 'B1_UNIT_SUBMIT'; 'ra-soat' = 'B1_HR_REVIEW'; 'duyet' = 'B1_APPROVE'
    'tu-cham' = 'B2_SELF_SCORE'; 'chi-bo' = 'B2_CELL_CONFIRM'; 'tap-the' = 'B3A_COLLECTIVE'; 'tham-dinh' = 'B3B_APPRAISAL'
    'nhan-xet' = 'B3C_DIRECTOR'; 'quyet-dinh' = 'B4_DECISION'; 'cong-bo' = 'B5_PUBLISH'
}
# Hành động trong hệ thống ứng với bước, và đường dẫn API của nó
$StepAction = @{
    'B1_REGISTER' = 'SubmitTasks'; 'B1_UNIT_SUBMIT' = 'ForwardTasks'; 'B1_HR_REVIEW' = 'ReviewTasks'; 'B1_APPROVE' = 'ApproveTasks'; 'B2_SELF_SCORE' = 'SubmitSelfScore'; 'B2_CELL_CONFIRM' = 'ConfirmByCell'
    'B3A_COLLECTIVE' = 'RecordCollectiveProposal'; 'B3B_APPRAISAL' = 'Appraise'; 'B3C_DIRECTOR' = 'DirectorReview'
    'B4_DECISION' = 'RecordDecision'; 'B5_PUBLISH' = 'Publish'
}
$ActionPath = @{
    'SubmitTasks' = 'tasks/submit'; 'ForwardTasks' = 'tasks/forward'; 'ReviewTasks' = 'tasks/review'; 'ApproveTasks' = 'tasks/approve'; 'SubmitSelfScore' = 'self-score/submit'; 'ConfirmByCell' = 'cell/confirm'
    'RecordCollectiveProposal' = 'collective'; 'Appraise' = 'appraisal'; 'DirectorReview' = 'director-review'
    'RecordDecision' = 'decision'; 'Publish' = 'publish'
}
# Ngày làm việc kể từ hôm nay làm hạn tối thiểu của từng bước, để không bước nào quá hạn
$DeadlineFloor = @{
    'B1_REGISTER' = 2; 'B1_UNIT_SUBMIT' = 3; 'B1_HR_REVIEW' = 3; 'B1_APPROVE' = 4; 'B2_SELF_SCORE' = 5; 'B2_CELL_CONFIRM' = 6; 'B3A_COLLECTIVE' = 7
    'B3B_APPRAISAL' = 8; 'B3C_DIRECTOR' = 9; 'B4_DECISION' = 10
}
# Điểm tự chấm hơi khác nhau giữa các hồ sơ, đều trong khoảng Hoàn thành tốt
$Profiles = @{
    'duy.lm'   = @{ Ratio = 0.84; NotAssured = @() }
    'dao.nta'  = @{ Ratio = 0.80; NotAssured = @('1.4') }
    'trung.nb' = @{ Ratio = 0.78; NotAssured = @('2.1') }
    'thanh.nn' = @{ Ratio = 0.82; NotAssured = @('3.2') }
}
$Products = @(
    @{ Name = 'Kế hoạch công tác trọng tâm của đơn vị'; Kind = 'TrongTam'; Role = 'ChuTri'; Share = 3 },
    @{ Name = 'Báo cáo đánh giá kết quả thực hiện chỉ tiêu'; Kind = 'TrongTam'; Role = 'ChuTri'; Share = 2 },
    @{ Name = 'Theo dõi, đôn đốc công việc thường xuyên'; Kind = 'ThuongXuyen'; Role = 'PhoiHop'; Share = 2 }
)

$script:Sessions = @{}
$script:Criteria = $null
$Today = (Get-Date).Date
$TodayIso = $Today.ToString('yyyy-MM-dd')

function Fail([string]$message) {
    Write-Host $message -ForegroundColor Red
    exit 1
}

function Write-Step([string]$message) { Write-Host "==> $message" -ForegroundColor Cyan }

function Read-Body($response) {
    if ($null -eq $response) { return '' }
    $reader = New-Object System.IO.StreamReader($response.GetResponseStream(), [System.Text.Encoding]::UTF8)
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

# Gọi API; trả về @{ Status; Ok; Data; Message; Body }. Không ném lỗi theo mã HTTP để người gọi tự quyết.
function Invoke-Api($session, [string]$method, [string]$path, $body = $null) {
    $params = @{ Uri = "$ApiUrl$path"; Method = $method; UseBasicParsing = $true; TimeoutSec = 120; WebSession = $session }
    if ($null -ne $body) {
        $json = $body | ConvertTo-Json -Depth 30 -Compress
        $params.Body = [System.Text.Encoding]::UTF8.GetBytes($json)
        $params.ContentType = 'application/json; charset=utf-8'
    }
    $status = 0; $text = ''; $retryAfter = $null
    try {
        $r = Invoke-WebRequest @params
        $status = [int]$r.StatusCode
        $text = [System.Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
    } catch [System.Net.WebException] {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { Fail "Gọi $method $path không thành công: $($_.Exception.Message). Backend có đang chạy ở $ApiUrl không?" }
        $status = [int]$resp.StatusCode
        $retryAfter = $resp.Headers['Retry-After']
        $text = Read-Body $resp
    }
    $parsed = $null
    if ($text) { try { $parsed = $text | ConvertFrom-Json } catch { $parsed = $null } }
    $message = if ($parsed -and $parsed.message) { $parsed.message } else { $text }
    if ($parsed -and $parsed.errors) { $message = "$message " + (@($parsed.errors) -join ' ') }
    return @{
        Status = $status; Ok = ($status -ge 200 -and $status -lt 300); Data = $(if ($parsed) { $parsed.data } else { $null })
        Message = $message; RetryAfter = $retryAfter
    }
}

# Như Invoke-Api nhưng dừng script khi lỗi, in mã HTTP và thông báo của API
function Invoke-ApiOk($session, [string]$method, [string]$path, $body, [string]$what) {
    $r = Invoke-Api $session $method $path $body
    if (-not $r.Ok) { Fail "$what không thành công: HTTP $($r.Status) - $($r.Message)" }
    return $r.Data
}

# Đăng nhập một lần cho mỗi tài khoản; giới hạn đăng nhập theo phút nên gặp 429 thì chờ rồi thử lại
function Get-Session([string]$username) {
    if ($script:Sessions.ContainsKey($username)) { return $script:Sessions[$username] }
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        $r = Invoke-Api $session 'POST' '/api/auth/login' @{ username = $username; password = $MatKhau }
        if ($r.Status -ne 429) { break }
        $wait = if ($r.RetryAfter) { [int]$r.RetryAfter } else { 60 }
        Write-Host "   Đăng nhập $username bị giới hạn tần suất, chờ $wait giây..." -ForegroundColor DarkYellow
        Start-Sleep -Seconds $wait
    }
    if (-not $r.Ok) { Fail "Đăng nhập $username không thành công: HTTP $($r.Status) - $($r.Message)" }
    $script:Sessions[$username] = $session
    return $session
}

function Get-WorkingDay([int]$count) {
    $d = $Today
    $added = 0
    while ($added -lt $count) {
        $d = $d.AddDays(1)
        if ($d.DayOfWeek -ne 'Saturday' -and $d.DayOfWeek -ne 'Sunday') { $added++ }
    }
    return $d
}

function Find-Period($manager) {
    $periods = Invoke-ApiOk $manager 'GET' '/api/evaluations/periods' $null 'Lấy danh sách kỳ đánh giá'
    return @($periods | Where-Object { $_.year -eq $Year -and $_.quarter -eq $Quarter }) | Select-Object -First 1
}

function Get-Participants($manager, $periodId) {
    return @(Invoke-ApiOk $manager 'GET' "/api/evaluations/periods/$periodId/participants" $null 'Lấy danh sách người được đánh giá')
}

function Get-Record($session, $recordId) {
    return Invoke-ApiOk $session 'GET' "/api/evaluations/records/$recordId" $null 'Đọc hồ sơ'
}

function Get-Actions($session, $recordId) {
    $r = Invoke-Api $session 'GET' "/api/evaluations/records/$recordId/actions"
    if ($r.Status -eq 403 -or $r.Status -eq 404) { return @() }
    if (-not $r.Ok) { Fail "Lấy thao tác của hồ sơ không thành công: HTTP $($r.Status) - $($r.Message)" }
    return @($r.Data.actions)
}

# Tài khoản đã đăng nhập xét trước để bớt đăng nhập (máy chủ giới hạn số lần đăng nhập mỗi phút)
function Get-AccountOrder {
    return @(@($Accounts | Where-Object { $script:Sessions.ContainsKey($_) }) + @($Accounts | Where-Object { -not $script:Sessions.ContainsKey($_) }))
}

# Tài khoản đầu tiên (theo thứ tự danh sách) có nút cho bước; hệ thống tự loại chủ hồ sơ và xung đột lợi ích
function Find-Performer($recordId, [string]$step) {
    $blocked = $null
    foreach ($user in (Get-AccountOrder)) {
        $actions = Get-Actions (Get-Session $user) $recordId
        foreach ($a in $actions) {
            if ($a.step -ne $step) { continue }
            if ($a.action -ne $StepAction[$step] -and $a.action -ne 'RecordExternal') { continue }
            if ($a.blockedBy) { $blocked = $a.blockedReason; continue }
            return @{ User = $user; Action = $a.action }
        }
    }
    return @{ User = $null; Blocked = $blocked }
}

function Load-Criteria($manager, $period) {
    if ($script:Criteria) { return $script:Criteria }
    $set = Invoke-ApiOk $manager 'GET' "/api/criteria-sets/$($period.criteriaSetId)" $null 'Đọc bộ tiêu chí của kỳ'
    $script:Criteria = $set.content
    return $script:Criteria
}

function New-TaskList($criteria, [string]$fullName) {
    $total = [double]$criteria.parameters.totalTaskWeight
    $axis = @($criteria.axes)[0].code
    $shares = 0; foreach ($p in $Products) { $shares += $p.Share }
    $used = 0.0
    $tasks = @(for ($i = 0; $i -lt $Products.Count; $i++) {
        $p = $Products[$i]
        # Sản phẩm cuối nhận phần còn lại để tổng đúng bằng tổng tỷ trọng của bộ tiêu chí
        $weight = if ($i -eq $Products.Count - 1) { $total - $used } else { [Math]::Round($total * $p.Share / $shares, 1) }
        $used += $weight
        [ordered]@{
            taskName = $p.Name; targetOutput = "Hoàn thành đúng hạn, đạt yêu cầu ($fullName)"; weight = $weight; axisCode = $axis
            kind = $p.Kind; role = $p.Role; exceedCriteria = 'Hoàn thành trước hạn, chất lượng vượt yêu cầu'
            evidenceSource = 'Báo cáo, văn bản đã ban hành'; confirmer = 'Lãnh đạo đơn vị'
        }
    })
    return @{ tasks = $tasks }
}

function New-SelfScore($criteria, $record, [string]$username) {
    $score = $Profiles[$username]
    if (-not $score) { $score = @{ Ratio = 0.8; NotAssured = @() } }
    $general = [ordered]@{}
    foreach ($group in @($criteria.generalGroups)) {
        foreach ($item in @($group.items)) {
            if ($score.NotAssured -contains $item.code) {
                $general[$item.code] = @{ score = 0; reason = 'Chưa hoàn thành đầy đủ nội dung trong quý' }
            } else {
                $general[$item.code] = @{ score = $item.maxScore }
            }
        }
    }
    $taskScores = @(foreach ($t in @($record.tasks)) {
        [ordered]@{ taskId = $t.id; criteriaA_Ratio = $score.Ratio; criteriaB_Ratio = $score.Ratio
            criteriaC_Ratio = $score.Ratio; criteriaD_Ratio = $score.Ratio; isExceedStandard = $false }
    })
    $assessment = [ordered]@{}
    foreach ($section in @($criteria.selfAssessmentSections)) {
        if ($section.code) { $assessment[$section.code] = "Hoàn thành các nhiệm vụ được giao trong quý, bảo đảm tiến độ và chất lượng ($($record.fullName))." }
    }
    $axisReports = [ordered]@{}
    foreach ($axisCode in @($record.tasks | ForEach-Object { $_.axisCode } | Select-Object -Unique)) {
        if (-not $axisCode) { continue }
        $axisReports[$axisCode] = [ordered]@{
            tasks = @([ordered]@{ content = 'Triển khai kế hoạch công tác trọng tâm của đơn vị'; level = 1 })
            deadline = $Today.AddDays(30).ToString('dd/MM/yyyy'); status = '- Đã hoàn thành'
            product = 'Kế hoạch, báo cáo đã ban hành'; progress = 'Đúng hạn'
        }
    }
    $groupNotes = [ordered]@{}
    foreach ($group in @($criteria.generalGroups)) { $groupNotes[$group.code] = 'Chấp hành tốt' }
    return [ordered]@{
        generalScores = $general; taskScores = $taskScores
        selfProposedGrade = 'HoanThanhTot'; selfGradeExplanation = 'Tự nhận mức theo kết quả thực hiện nhiệm vụ trong quý'
        selfAssessment = $assessment; axisReports = $axisReports; generalGroupNotes = $groupNotes
    }
}

$ExternalAuthority = @{
    'B1_APPROVE' = 'Hội đồng thành viên Tổng công ty'; 'B2_CELL_CONFIRM' = 'Chi bộ'; 'B3A_COLLECTIVE' = 'Tập thể lãnh đạo Tổng công ty'
    'B3B_APPRAISAL' = 'Ban Tổ chức Đảng ủy Tổng công ty'; 'B3C_DIRECTOR' = 'Hội đồng thành viên Tổng công ty'
    'B4_DECISION' = 'Ban Thường vụ Đảng ủy Tổng công ty'
}

function New-StepBody([string]$step, [string]$action, $record, [string]$username, $criteria) {
    $decisionGrade = if ($MucQuyetDinh.ContainsKey($username)) { $MucQuyetDinh[$username] } else { 'HoanThanhTot' }
    if ($action -eq 'RecordExternal') {
        $grade = if ($step -eq 'B4_DECISION') { $decisionGrade } else { 'HoanThanhTot' }
        return [ordered]@{
            authorityName = $ExternalAuthority[$step]; documentNumber = '01/TB-THU'; documentDate = $TodayIso
            comment = 'Ghi nhận kết quả của cấp trên'; grade = $grade
        }
    }
    switch ($step) {
        'B1_REGISTER' { return New-TaskList $criteria $record.fullName }
        'B1_UNIT_SUBMIT' { return @{ comment = 'Gửi danh mục sản phẩm của đơn vị' } }
        'B1_HR_REVIEW' { return @{ comment = 'Đã rà soát, trình duyệt' } }
        'B1_APPROVE' { return @{ comment = 'Đồng ý danh mục sản phẩm đăng ký' } }
        'B2_SELF_SCORE' { return New-SelfScore $criteria $record $username }
        'B2_CELL_CONFIRM' { return @{ comment = 'Chi bộ xác nhận phiếu tự chấm' } }
        'B3A_COLLECTIVE' {
            $text = if ($record.unitLeaderProposal) { 'Đề xuất trên cơ sở kết quả họp lãnh đạo đơn vị' } else { 'Tập thể lãnh đạo thống nhất đề xuất' }
            $body = @{ proposedGrade = 'HoanThanhTot'; comment = $text }
            if ($record.collectiveMinutesMode -eq 'Required') {
                $meeting = Get-CollectiveMeeting $record $username
                $body['meetingId'] = $meeting.id
                $body['votes'] = [ordered]@{
                    votesExcellent = 0; votesGood = [int]$meeting.presentCount; votesSatisfactory = 0
                    votesUnsatisfactory = 0; votesNotRated = 0; invalidVotes = 0
                }
            }
            return $body
        }
        'B3B_APPRAISAL' {
            $levels = @(foreach ($t in @($record.tasks)) { [ordered]@{ taskId = $t.id; level = 1 } })
            return [ordered]@{
                generalScore = $record.generalCriteriaScore; tasksScore = $record.tasksScore; taskLevels = $levels
                comment = 'Đủ minh chứng, thống nhất với phiếu tự chấm'; proposedGrade = 'HoanThanhTot'
                explanation = 'Căn cứ minh chứng nộp kèm phiếu tự chấm'
            }
        }
        'B3C_DIRECTOR' { return @{ comment = 'Hoàn thành tốt nhiệm vụ được giao'; proposedGrade = 'HoanThanhTot' } }
        'B4_DECISION' {
            return [ordered]@{
                finalGrade = $decisionGrade; documentNumber = '01-QĐ/ĐU'; documentDate = $TodayIso
                explanation = 'Theo kết quả thẩm định và nhận xét'
            }
        }
        'B5_PUBLISH' { return [ordered]@{ comment = 'Công bố kết quả' } }
        default { Fail "Bước $step chưa được hỗ trợ trong script." }
    }
}

# Biên bản họp tập thể (Mẫu 12) bước 3a, mỗi đơn vị một biên bản; dùng lại biên bản đã có của lần chạy trước.
# Thư ký chỉ có phạm vi chi bộ bị 403 khi lập theo Phòng -> lập theo chi bộ của cán bộ.
function Get-CollectiveMeeting($record, [string]$owner) {
    $key = if ($record.departmentId) { "d:$($record.departmentId)" } else { "c:$($record.partyCellId)" }
    $cellKey = "c:$($record.partyCellId)"
    if ($script:Meetings.ContainsKey($key)) { return $script:Meetings[$key] }
    if ($record.partyCellId -and $script:Meetings.ContainsKey($cellKey)) { return $script:Meetings[$cellKey] }
    $byCell = [ordered]@{
        periodId = $record.periodId; departmentId = $null; partyCellId = $record.partyCellId
        stage = 'B3A_COLLECTIVE'; formCode = 'M12'; meetingType = 'Họp tập thể lãnh đạo đơn vị'
        invitedCount = 5; presentCount = 5
    }
    foreach ($user in $MeetingSecretaries) {
        if ($user -eq $owner) { continue }
        $session = Get-Session $user
        $list = Invoke-Api $session 'GET' "/api/evaluations/meetings?periodId=$($record.periodId)"
        if ($list.Ok) {
            $meetings = @($list.Data | Where-Object { $_.stage -eq 'B3A_COLLECTIVE' })
            $found = $meetings | Where-Object {
                $(if ($record.departmentId) { $_.departmentId -eq $record.departmentId } else { -not $_.departmentId -and $_.partyCellId -eq $record.partyCellId })
            } | Select-Object -First 1
            if ($found) { $script:Meetings[$key] = $found; return $found }
            if ($record.partyCellId) {
                $found = $meetings | Where-Object { -not $_.departmentId -and $_.partyCellId -eq $record.partyCellId } | Select-Object -First 1
                if ($found) { $script:Meetings[$cellKey] = $found; return $found }
            }
        }
        $body = [ordered]@{
            periodId = $record.periodId; departmentId = $record.departmentId; partyCellId = $(if ($record.departmentId) { $null } else { $record.partyCellId })
            stage = 'B3A_COLLECTIVE'; formCode = 'M12'; meetingType = 'Họp tập thể lãnh đạo đơn vị'
            invitedCount = 5; presentCount = 5
        }
        $r = Invoke-Api $session 'POST' '/api/evaluations/meetings' $body
        if ($r.Ok) {
            Write-Host "   Lập biên bản họp tập thể của $($record.departmentName) ($user)" -ForegroundColor DarkCyan
            $script:Meetings[$key] = $r.Data
            return $r.Data
        }
        if ($r.Status -ne 403) { Fail "Lập biên bản họp tập thể của $($record.departmentName) không thành công: HTTP $($r.Status) - $($r.Message)" }
        if ($record.departmentId -and $record.partyCellId) {
            $r = Invoke-Api $session 'POST' '/api/evaluations/meetings' $byCell
            if ($r.Ok) {
                Write-Host "   Lập biên bản họp tập thể theo chi bộ của $($record.fullName) ($user)" -ForegroundColor DarkCyan
                $script:Meetings[$cellKey] = $r.Data
                return $r.Data
            }
            if ($r.Status -ne 403) { Fail "Lập biên bản họp tập thể theo chi bộ của $($record.fullName) không thành công: HTTP $($r.Status) - $($r.Message)" }
        }
    }
    Fail "Lập biên bản họp tập thể cho $($record.departmentName) không thành công: không tài khoản nào trong $($MeetingSecretaries -join ', ') có quyền. Hãy kiểm tra vai trò Thư ký hội nghị."
}

# Thực hiện bước hiện tại của một hồ sơ bằng tài khoản đầu tiên có nút
function Invoke-RecordStep($manager, $participant, [string]$step) {
    $performer = Find-Performer $participant.recordId $step
    if (-not $performer.User) {
        $why = if ($performer.Blocked) { " (bị khóa: $($performer.Blocked))" } else { '' }
        Fail "Không tài khoản nào trong danh sách thực hiện được bước $step của hồ sơ $($participant.fullName)$why. Hãy kiểm tra vai trò, phạm vi gán."
    }
    $session = Get-Session $performer.User
    $record = Get-Record $session $participant.recordId
    $body = New-StepBody $step $performer.Action $record $participant.username (Load-Criteria $manager $script:Period)
    $body['version'] = $record.version
    $path = if ($performer.Action -eq 'RecordExternal') { "external/$step" } else { $ActionPath[$performer.Action] }
    $result = Invoke-ApiOk $session 'POST' "/api/evaluations/records/$($participant.recordId)/$path" $body `
        "Bước $step của hồ sơ $($participant.fullName) ($($performer.User))"
    $kind = if ($performer.Action -eq 'RecordExternal') { 'ghi nhận kết quả cấp trên' } else { $performer.Action }
    Write-Host (("   {0,-22} {1,-16} {2,-10} -> {3}" -f $participant.fullName, $step, $performer.User, $result.status) + " ($kind)")
}

# Biên bản họp thống nhất (Mẫu 20) cho các hồ sơ có cổng Bắt buộc: một người lập, người khác chốt
function Complete-Calibration($manager, $period, $records) {
    if ($records.Count -eq 0) { return }
    # Đơn vị mà người lập hoặc mọi người chốt đều có hồ sơ: tách mỗi hồ sơ một biên bản
    $batches = @()
    foreach ($group in ($records | Group-Object departmentId)) {
        $owners = @($group.Group | ForEach-Object { $_.username })
        $canPrepare = @($CalibrationPreparers | Where-Object { $owners -notcontains $_ }).Count -gt 0
        $canConclude = @($CalibrationConcluders | Where-Object { $owners -notcontains $_ }).Count -gt 0
        if ($canPrepare -and $canConclude) { $batches += , @($group.Group) }
        else { foreach ($one in $group.Group) { $batches += , @($one) } }
    }
    foreach ($batch in $batches) {
        $ids = @($batch | ForEach-Object { $_.recordId })
        $owners = @($batch | ForEach-Object { $_.username })
        $names = ($batch | ForEach-Object { $_.fullName }) -join ', '
        $preparerUser = @($CalibrationPreparers | Where-Object { $owners -notcontains $_ }) | Select-Object -First 1
        if (-not $preparerUser) { Fail "Lập biên bản họp thống nhất cho $names không thành công: người lập trong danh sách đều có hồ sơ trong biên bản." }
        $preparer = Get-Session $preparerUser
        Write-Step "Lập biên bản họp thống nhất (Mẫu 20) cho: $names"
        $created = Invoke-ApiOk $preparer 'POST' '/api/calibrations' ([ordered]@{
            periodId = $period.id; departmentId = $batch[0].departmentId; groupName = "Cán bộ quản lý $($batch[0].departmentName)"; recordIds = $ids
        }) "Lập biên bản họp thống nhất ($preparerUser)"
        $save = [ordered]@{
            version = $created.version; groupName = $created.groupName; heldAt = (Get-Date).ToUniversalTime().ToString('o'); location = 'Phòng họp'
            chairName = 'Chủ trì cuộc họp'; secretaryName = 'Thư ký cuộc họp'; recordIds = $ids
            attendees = @([ordered]@{ fullName = 'Thành viên dự họp'; capacity = 'Ủy viên'; dissentNote = $null })
            summary = @(); cases = @()
        }
        if ($ids.Count -eq 1) { $save['extraConclusion'] = 'Không có hồ sơ cùng nhóm chức danh để so sánh.' }
        $saved = Invoke-ApiOk $preparer 'PUT' "/api/calibrations/$($created.id)" $save "Lưu biên bản họp thống nhất ($preparerUser)"
        $done = $false
        foreach ($user in $CalibrationConcluders) {
            if ($owners -contains $user) { continue }
            $r = Invoke-Api (Get-Session $user) 'POST' "/api/calibrations/$($created.id)/conclude" @{ version = $saved.version }
            if ($r.Ok) { Write-Host "   Đã chốt biên bản ($user)"; $done = $true; break }
            if ($r.Status -ne 403) { Fail "Chốt biên bản họp thống nhất ($user) không thành công: HTTP $($r.Status) - $($r.Message)" }
        }
        if (-not $done) { Fail "Chốt biên bản họp thống nhất không thành công: $($CalibrationConcluders -join ', ') không có quyền chốt cho các hồ sơ này." }
    }
}

# Phiếu kiểm tra (Mẫu 19) cho các hồ sơ có cổng Bắt buộc: lập, kết luận Đủ điều kiện, gửi, người khác xác nhận
function Complete-ControlCheck($manager, $period, $records) {
    if ($records.Count -eq 0) { return }
    $ids = @($records | ForEach-Object { $_.recordId })
    $owners = @($records | ForEach-Object { $_.username })
    Write-Step "Lập phiếu kiểm tra (Mẫu 19) cho: $(($records | ForEach-Object { $_.fullName }) -join ', ')"
    $creator = $null; $check = $null
    foreach ($user in (Get-AccountOrder)) {
        if ($owners -contains $user) { continue }
        $r = Invoke-Api (Get-Session $user) 'POST' '/api/control-checks' ([ordered]@{ periodId = $period.id; groupName = 'Cán bộ quản lý'; recordIds = $ids })
        if ($r.Ok) { $creator = $user; $check = $r.Data; break }
        if ($r.Status -ne 403) { Fail "Lập phiếu kiểm tra ($user) không thành công: HTTP $($r.Status) - $($r.Message)" }
    }
    if (-not $creator) { Fail 'Lập phiếu kiểm tra không thành công: không tài khoản nào trong danh sách có quyền lập.' }
    $session = Get-Session $creator
    $items = @(foreach ($i in @($check.items)) { [ordered]@{ itemCode = $i.itemCode; result = 'Pass'; evidence = $null } })
    $filled = Invoke-ApiOk $session 'PUT' "/api/control-checks/$($check.id)" ([ordered]@{ version = $check.version; items = $items; conclusion = 'Eligible' }) "Điền phiếu kiểm tra ($creator)"
    $submitted = Invoke-ApiOk $session 'POST' "/api/control-checks/$($check.id)/submit" @{ version = $filled.version } "Gửi phiếu kiểm tra ($creator)"
    foreach ($user in (Get-AccountOrder)) {
        if ($user -eq $creator -or $owners -contains $user) { continue }
        $r = Invoke-Api (Get-Session $user) 'POST' "/api/control-checks/$($check.id)/confirm" @{ version = $submitted.version }
        if ($r.Ok) { Write-Host "   Lập: $creator, xác nhận: $user"; return }
        if ($r.Status -ne 403) { Fail "Xác nhận phiếu kiểm tra ($user) không thành công: HTTP $($r.Status) - $($r.Message)" }
    }
    Fail 'Xác nhận phiếu kiểm tra không thành công: không tài khoản nào khác người lập có quyền xác nhận.'
}

function Get-ProfileSettings($period, [string]$code) {
    return @($period.settings.profiles) | Where-Object { $_.code -eq $code } | Select-Object -First 1
}

# Hồ sơ đã nằm trong biên bản họp thống nhất còn hiệu lực (lập tay hoặc lần chạy trước) -> trạng thái biên bản.
# Không dựa vào calibrationSessionId của hồ sơ: trường đó chỉ có khi biên bản đã chốt điều chỉnh kết quả của hồ sơ.
# Người xem không thấy biên bản, phiếu có hồ sơ của chính mình: đọc bằng mọi người lập rồi gộp.
function Get-CalibratedRecords($manager, $period) {
    $map = @{}; $seen = @{}
    foreach ($viewer in @($CalibrationPreparers + $CalibrationConcluders | Select-Object -Unique)) {
        $session = Get-Session $viewer
        $r = Invoke-Api $session 'GET' "/api/calibrations?periodId=$($period.id)"
        if (-not $r.Ok) { continue }
        foreach ($s in @($r.Data)) {
            if ($s.status -eq 'Void' -or $seen.ContainsKey([string]$s.id)) { continue }
            $detail = Invoke-Api $session 'GET' "/api/calibrations/$($s.id)"
            if (-not $detail.Ok) { continue }
            $seen[[string]$s.id] = $true
            foreach ($rec in @($detail.Data.records)) { $map[[string]$rec.recordId] = $s.status }
        }
    }
    return $map
}

# Hồ sơ đã thuộc phiếu kiểm tra còn hiệu lực (chưa hủy) -> trạng thái phiếu.
function Get-CheckedRecords($manager, $period) {
    $map = @{}; $seen = @{}
    foreach ($viewer in @(@($ManagerUser) + $CalibrationPreparers | Select-Object -Unique)) {
        $session = Get-Session $viewer
        $r = Invoke-Api $session 'GET' "/api/control-checks?periodId=$($period.id)"
        if (-not $r.Ok) { continue }
        foreach ($c in @($r.Data)) {
            if ($c.status -eq 'Cancelled' -or $seen.ContainsKey([string]$c.id)) { continue }
            $detail = Invoke-Api $session 'GET' "/api/control-checks/$($c.id)"
            if (-not $detail.Ok) { continue }
            $seen[[string]$c.id] = $true
            foreach ($rec in @($detail.Data.records | Where-Object { $_.isCurrent })) { $map[[string]$rec.recordId] = $c.status }
        }
    }
    return $map
}

# Trước bước quyết định: làm các cổng Bắt buộc cho hồ sơ đang chờ quyết định trong hệ thống, bỏ hồ sơ đã có biên bản, phiếu
function Complete-Gates($manager, $period) {
    $waiting = @(Get-Participants $manager $period.id | Where-Object { $_.currentStep -eq 'B4_DECISION' -and $GiuLai -notcontains $_.username })
    $calibrated = Get-CalibratedRecords $manager $period
    $checked = Get-CheckedRecords $manager $period
    $calibration = @(); $control = @()
    foreach ($p in $waiting) {
        $flow = Get-ProfileSettings $period $p.workflowProfileCode
        if ($flow.steps.B4_DECISION.mode -ne 'Internal') { continue }
        $id = [string]$p.recordId
        if ($flow.gates.CALIBRATION.mode -eq 'Required') {
            if (-not $calibrated.ContainsKey($id)) { $calibration += $p }
            elseif ($calibrated[$id] -ne 'Concluded') { Write-Host "   $($p.fullName): biên bản họp thống nhất chưa chốt, hãy chốt trên giao diện." -ForegroundColor DarkYellow }
        }
        if ($flow.gates.CONTROL.mode -eq 'Required') {
            if (-not $checked.ContainsKey($id)) { $control += $p }
            elseif ($checked[$id] -ne 'Confirmed') { Write-Host "   $($p.fullName): phiếu kiểm tra chưa xác nhận, hãy hoàn tất trên giao diện." -ForegroundColor DarkYellow }
        }
    }
    Complete-Calibration $manager $period $calibration
    Complete-ControlCheck $manager $period $control
}

# Hạn các bước còn trước hôm nay (hoặc quá sát) thì dời tới hôm nay + vài ngày làm việc
function Update-Deadlines($manager, $period) {
    foreach ($flow in @($period.settings.profiles)) {
        foreach ($step in $DeadlineFloor.Keys) {
            $setting = $flow.steps.$step
            if (-not $setting -or $setting.mode -eq 'Off' -or -not $setting.deadline) { continue }
            $floor = Get-WorkingDay $DeadlineFloor[$step]
            if ([datetime]::ParseExact($setting.deadline, 'yyyy-MM-dd', $null) -lt $floor) { $setting.deadline = $floor.ToString('yyyy-MM-dd') }
        }
    }
    return Invoke-ApiOk $manager 'PUT' "/api/evaluations/periods/$($period.id)" ([ordered]@{ version = $period.version; settings = $period.settings }) 'Đặt lại hạn các bước'
}

function Show-Readiness($readiness) {
    Write-Host 'Kỳ chưa sẵn sàng: còn bước không có người thực hiện.' -ForegroundColor Red
    foreach ($i in @($readiness.issues)) { Write-Host "   - $($i.fullName) | $($i.stepName): $($i.message)" -ForegroundColor Red }
}

function Add-Evaluated($manager, $period) {
    $candidates = @(Invoke-ApiOk $manager 'GET' "/api/evaluations/periods/$($period.id)/candidates" $null 'Lấy danh sách cán bộ')
    $ids = @()
    foreach ($u in $Evaluated.Keys) {
        $c = $candidates | Where-Object { $_.username -eq $u } | Select-Object -First 1
        if (-not $c) { Fail "Thêm người được đánh giá không thành công: không tìm thấy tài khoản $u trong danh sách cán bộ." }
        if (-not $c.alreadyAdded) { $ids += $c.memberId }
    }
    if ($ids.Count -gt 0) {
        $added = Invoke-ApiOk $manager 'POST' "/api/evaluations/periods/$($period.id)/participants" @{ memberIds = $ids } 'Thêm người được đánh giá'
        Write-Host "   Đã thêm $($added.added) người."
    }
    # Diện gán tự động theo chức vụ; lệch so với trường hợp muốn thử thì báo để kiểm tra danh mục chức vụ
    foreach ($p in (Get-Participants $manager $period.id)) {
        $want = $Evaluated[$p.username]
        if ($want -and $p.workflowProfileCode -ne $want) {
            Write-Host "   Lưu ý: $($p.username) nhận diện $($p.workflowProfileCode) theo chức vụ, không phải $want." -ForegroundColor DarkYellow
        }
    }
}

# Tạo kỳ dự thảo như giao diện: bộ tiêu chí xuất bản mới nhất, ngày theo quý, hạn các bước theo quy tắc
function New-Period($manager) {
    Write-Step "Tạo kỳ $PeriodName"
    $sets = @(Invoke-ApiOk $manager 'GET' '/api/criteria-sets' $null 'Lấy danh sách bộ tiêu chí')
    $set = $sets | Where-Object { $_.status -eq 'Published' } | Sort-Object publishedAt -Descending | Select-Object -First 1
    if (-not $set) { Fail 'Tạo kỳ không thành công: chưa có bộ tiêu chí đã xuất bản.' }
    $start = New-Object DateTime($Year, (3 * $Quarter - 2), 1)
    $period = Invoke-ApiOk $manager 'POST' '/api/evaluations/periods' ([ordered]@{
        year = $Year; quarter = $Quarter; name = $PeriodName; criteriaSetId = $set.id
        startDate = $start.ToString('yyyy-MM-ddT00:00:00Z'); endDate = $start.AddMonths(3).AddDays(-1).ToString('yyyy-MM-ddT00:00:00Z')
    }) 'Tạo kỳ đánh giá'
    Write-Host "   Bộ tiêu chí: $($set.name) ($($set.code))"
    return $period
}

# Mọi cán bộ giữ chức vụ thuộc diện đánh giá; diện quyết định gán tự động theo chức vụ
function Add-InScope($manager, $period) {
    $candidates = @(Invoke-ApiOk $manager 'GET' "/api/evaluations/periods/$($period.id)/candidates" $null 'Lấy danh sách cán bộ')
    $ids = @($candidates | Where-Object { $_.inScope -and -not $_.alreadyAdded } | ForEach-Object { $_.memberId })
    if ($ids.Count -eq 0) { Fail 'Thêm người được đánh giá không thành công: không có cán bộ nào giữ chức vụ thuộc diện đánh giá.' }
    $added = Invoke-ApiOk $manager 'POST' "/api/evaluations/periods/$($period.id)/participants" @{ memberIds = $ids } 'Thêm người được đánh giá'
    Write-Host "   Đã thêm $($added.added) người."
    foreach ($s in @($added.skipped)) { Write-Host "   Bỏ qua: $s" -ForegroundColor DarkYellow }
}

# Kiểm tra kẹt luồng rồi mở kỳ dự thảo
function Open-Period($manager, $period) {
    $readiness = Invoke-ApiOk $manager 'GET' "/api/evaluations/periods/$($period.id)/readiness" $null 'Kiểm tra kẹt luồng'
    if (-not $readiness.ready) { Show-Readiness $readiness; Write-Host "Kỳ `"$($period.name)`" vẫn ở dự thảo: bổ sung người thực hiện rồi mở kỳ trên giao diện." -ForegroundColor Red; exit 1 }
    $period = Invoke-ApiOk $manager 'GET' "/api/evaluations/periods/$($period.id)" $null 'Đọc kỳ'
    if ($period.status -ne 'Draft') { return }
    Write-Step 'Mở kỳ'
    $r = Invoke-Api $manager 'POST' "/api/evaluations/periods/$($period.id)/open" @{ version = $period.version }
    if ($r.Status -eq 409 -and $r.Data -and $null -ne $r.Data.issues) { Show-Readiness $r.Data; exit 1 }
    if (-not $r.Ok) { Fail "Mở kỳ không thành công: HTTP $($r.Status) - $($r.Message)" }
}

# Người không đánh giá trong quý: đánh dấu Chưa xếp loại ngay sau khi mở kỳ
function Set-NotRated($manager, $period) {
    foreach ($p in (Get-Participants $manager $period.id)) {
        $reason = $NotRated[$p.username]
        if (-not $reason -or $p.status -eq 'AwaitingPublish' -or $p.status -eq 'Published') { continue }
        $record = Get-Record $manager $p.recordId
        $result = Invoke-ApiOk $manager 'POST' "/api/evaluations/records/$($p.recordId)/not-rated" @{ version = $record.version; reason = $reason } "Đánh dấu Chưa xếp loại $($p.fullName)"
        Write-Host (("   {0,-22} Chưa xếp loại    {1,-10} -> {2}" -f $p.fullName, $ManagerUser, $result.status))
    }
}

function Show-Summary($manager, $period) {
    Write-Step 'Hiện trạng hồ sơ'
    $rows = foreach ($p in (Get-Participants $manager $period.id)) {
        $next = @()
        foreach ($user in (Get-AccountOrder)) {
            foreach ($a in (Get-Actions (Get-Session $user) $p.recordId)) {
                if ($a.step -eq $p.currentStep -and ($a.action -eq $StepAction[$p.currentStep] -or $a.action -eq 'RecordExternal')) {
                    $label = if ($a.blockedBy) { "$user (đang khóa)" } else { $user }
                    $next += $label
                    break
                }
            }
        }
        $who = if ($next.Count -gt 0) { $next -join ', ' } elseif ($p.status -eq 'Published') { '-' } else { '(không có trong danh sách tài khoản)' }
        [pscustomobject]@{ 'Hồ sơ' = "$($p.fullName) ($($p.username))"; 'Diện' = $p.workflowProfileCode; 'Trạng thái' = $p.statusDisplayName; 'Người làm tiếp' = $who }
    }
    $rows | Format-Table -AutoSize -Wrap | Out-String -Width 220 | Write-Host
}

# ---------------------------------------------------------------- Tình huống (-TinhHuong)
# Mỗi tình huống làm một lần trên một hồ sơ diện cơ sở khác nhau, chọn khi hồ sơ tới bước; lưu lựa chọn để chạy lại không làm lặp
$ReturnAt = [ordered]@{
    'B1_UNIT_SUBMIT'  = @{ Action = 'ReturnByUnit'; Path = 'tasks/forward/return' }
    'B1_HR_REVIEW'    = @{ Action = 'ReturnByReviewer'; Path = 'tasks/review/return' }
    'B1_APPROVE'      = @{ Action = 'ReturnTasks'; Path = 'tasks/return' }
    'B2_CELL_CONFIRM' = @{ Action = 'ReturnByCell'; Path = 'cell/return' }
    'B3B_APPRAISAL'   = @{ Action = 'ReturnByAppraiser'; Path = 'appraisal/return' }
}
$OpinionAt = @{ 'B1_HR_REVIEW' = 'ListCheck'; 'B1_APPROVE' = 'DeputyOnList'; 'B3C_DIRECTOR' = 'DeputyOnReview' }
$ScenarioName = [ordered]@{
    'chua-xep-loai' = 'Đánh dấu Chưa xếp loại'
    'tra-lai:B1_UNIT_SUBMIT' = 'Trưởng đơn vị trả lại danh mục'; 'tra-lai:B1_HR_REVIEW' = 'TCCB-LĐ trả lại danh mục'
    'tra-lai:B1_APPROVE' = 'Người duyệt trả lại danh mục'; 'thu-hoi' = 'Thu hồi duyệt danh mục'
    'dieu-chinh' = 'Đề nghị điều chỉnh việc (Mẫu 04) và duyệt'; 'tra-lai:B2_CELL_CONFIRM' = 'Chi bộ trả lại phiếu tự chấm'
    'tra-lai:B3B_APPRAISAL' = 'Thẩm định trả lại'; 'y-kien-ra-soat' = 'Ý kiến rà soát danh mục (TCCB-LĐ)'
    'y-kien-pgd' = 'Ý kiến Phó Giám đốc (danh mục, đề xuất mức)'
    'mo-lai' = 'Mở lại hồ sơ đã công bố rồi công bố lại'
}
$script:State = @{}
$script:StateFile = $null

function Load-State($period) {
    $script:StateFile = Join-Path $env:TEMP "ctd-tinh-huong-$($period.id).json"
    if (Test-Path $script:StateFile) {
        $obj = Get-Content $script:StateFile -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($prop in $obj.PSObject.Properties) { $script:State[$prop.Name] = [string]$prop.Value }
    }
}

function Save-State { $script:State | ConvertTo-Json | Set-Content -Path $script:StateFile -Encoding UTF8 }

function Test-Done([string]$key) { return $script:State.ContainsKey("${key}:xong") }

function Set-Done([string]$key) { $script:State["${key}:xong"] = '1'; Save-State }

# Hồ sơ đã được chọn cho tình huống này, hoặc chọn hồ sơ khi chưa dùng cho tình huống khác
function Select-ForScenario([string]$key, $p) {
    $id = [string]$p.recordId
    if ($script:State.ContainsKey($key)) { return $script:State[$key] -eq $id }
    if (@($script:State.Values) -contains $id) { return $false }
    $script:State[$key] = $id; Save-State
    return $true
}

# Tài khoản đầu tiên có thao tác (theo API actions) trên hồ sơ
function Find-Action([string]$recordId, [string]$action) {
    foreach ($user in (Get-AccountOrder)) {
        foreach ($a in (Get-Actions (Get-Session $user) $recordId)) {
            if ($a.action -eq $action -and -not $a.blockedBy) { return $user }
        }
    }
    return $null
}

function Get-Participant($manager, [string]$recordId) {
    return Get-Participants $manager $script:Period.id | Where-Object { [string]$_.recordId -eq $recordId } | Select-Object -First 1
}

# Đi tiếp từng bước của một hồ sơ cho tới khi hồ sơ chờ ở bước thứ $stopIndex
function Push-Record($manager, [string]$recordId, [int]$stopIndex) {
    for ($i = 0; $i -lt 25; $i++) {
        $p = Get-Participant $manager $recordId
        if (-not $p -or -not $p.currentStep) { return }
        if ([array]::IndexOf($Steps, $p.currentStep) -ge $stopIndex) { return }
        if ($p.currentStep -eq 'B4_DECISION') { Complete-Gates $manager $script:Period }
        Invoke-RecordStep $manager $p $p.currentStep
    }
    Fail "Hồ sơ $recordId không đi tiếp được sau nhiều lần thử."
}

# Người làm thao tác chính và quản trị: không chọn hồ sơ của họ cho tình huống
function Test-KeyAccount([string]$username) {
    return ($TaiKhoan -contains $username) -or ($CalibrationPreparers -contains $username) -or ($CalibrationConcluders -contains $username) -or $username -eq 'quantri' -or $MucQuyetDinh.ContainsKey($username)
}

function Write-Scenario([string]$text) { Write-Host "   [Tình huống] $text" -ForegroundColor Magenta }

function Invoke-Return($manager, $p, [string]$step) {
    $key = "tra-lai:$step"
    if ((Test-Done $key) -or -not (Select-ForScenario $key $p)) { return }
    $def = $ReturnAt[$step]
    $user = Find-Action ([string]$p.recordId) $def.Action
    if (-not $user) { Fail "Tình huống trả lại ở bước $step của $($p.fullName): không tài khoản nào có thao tác trả lại." }
    $session = Get-Session $user
    $record = Get-Record $session $p.recordId
    $result = Invoke-ApiOk $session 'POST' "/api/evaluations/records/$($p.recordId)/$($def.Path)" ([ordered]@{ version = $record.version; reason = 'Thử trả lại: đề nghị bổ sung, làm rõ nội dung' }) `
        "Trả lại ở bước $step của hồ sơ $($p.fullName) ($user)"
    Write-Scenario "$($p.fullName): $($ScenarioName[$key]) ($user) -> $($result.status)"
    Set-Done $key
    Push-Record $manager ([string]$p.recordId) ([array]::IndexOf($Steps, $step))
}

# Ý kiến rà soát ở B1_HR_REVIEW; ý kiến Phó Giám đốc (danh mục ở B1_APPROVE, đề xuất mức ở B3C) trên một hồ sơ có PGĐ ghi được
function Invoke-Opinion($p, [string]$step) {
    $kind = $OpinionAt[$step]
    $key = if ($kind -eq 'ListCheck') { 'y-kien-ra-soat' } else { 'y-kien-pgd' }
    if (Test-Done "${key}:$kind") { return }
    $id = [string]$p.recordId
    if ($script:State.ContainsKey($key)) { if ($script:State[$key] -ne $id) { return } }
    elseif (@($script:State.Values) -contains $id) { return }
    elseif ($key -eq 'y-kien-pgd') {
        # Thử tối đa vài hồ sơ để tìm hồ sơ có PGĐ phụ trách
        $tries = [int]$script:State['y-kien-pgd:thu'] + 1
        if ($tries -gt 8) { return }
        $script:State['y-kien-pgd:thu'] = [string]$tries
    }
    foreach ($user in (Get-AccountOrder)) {
        $session = Get-Session $user
        $r = Invoke-Api $session 'GET' "/api/evaluations/records/$id/opinions"
        if (-not $r.Ok -or -not $r.Data.canRecord -or @($r.Data.kinds | ForEach-Object { $_.value }) -notcontains $kind) { continue }
        Invoke-ApiOk $session 'POST' "/api/evaluations/records/$id/opinions" ([ordered]@{ kind = $kind; content = 'Thống nhất với đề xuất, đề nghị bám sát chỉ tiêu được giao' }) `
            "Ghi ý kiến $kind cho hồ sơ $($p.fullName) ($user)" | Out-Null
        Write-Scenario "$($p.fullName): ghi ý kiến $kind ($user)"
        $script:State[$key] = $id
        Set-Done "${key}:$kind"
        return
    }
    if ($script:State.ContainsKey($key)) {
        Write-Host "   [Tình huống] $($p.fullName): không tài khoản nào ghi được ý kiến $kind." -ForegroundColor DarkYellow
        Set-Done "${key}:$kind"
    } else { Save-State }
}

# Chủ hồ sơ đề nghị điều chỉnh chỉ tiêu một việc (Mẫu 04), người duyệt danh mục duyệt
function Invoke-TaskChange($manager, $p) {
    $key = 'dieu-chinh'
    if ((Test-Done $key) -or -not (Select-ForScenario $key $p)) { return }
    $owner = Get-Session $p.username
    $record = Get-Record $owner $p.recordId
    $task = @($record.tasks)[0]
    $proposal = [ordered]@{
        taskName = $task.taskName; targetOutput = "$($task.targetOutput) - điều chỉnh chỉ tiêu"; deadline = $task.deadline; axisCode = $task.axisCode
        productCode = $task.productCode; role = $task.role; exceedCriteria = $task.exceedCriteria; evidenceSource = $task.evidenceSource; confirmer = $task.confirmer
    }
    $r = Invoke-Api $owner 'POST' "/api/evaluations/records/$($p.recordId)/task-changes" ([ordered]@{
        version = $record.version; type = 'Adjust'; taskId = $task.id; proposal = $proposal; reason = 'Thay đổi yêu cầu công việc trong quý'
    })
    if (-not $r.Ok) {
        Write-Host "   [Tình huống] $($p.fullName): đề nghị điều chỉnh việc không thành công: HTTP $($r.Status) - $($r.Message)" -ForegroundColor DarkYellow
        Set-Done $key
        return
    }
    foreach ($user in (Get-AccountOrder)) {
        $session = Get-Session $user
        $r = Invoke-Api $session 'GET' "/api/evaluations/records/$($p.recordId)/task-changes"
        if (-not $r.Ok) { continue }
        $pending = @($r.Data.requests | Where-Object { $_.status -eq 'Pending' -and $_.canDecide }) | Select-Object -First 1
        if (-not $pending) { continue }
        Invoke-ApiOk $session 'POST' "/api/evaluations/records/$($p.recordId)/task-changes/$($pending.id)/approve" ([ordered]@{ version = $pending.version; comment = 'Đồng ý điều chỉnh' }) `
            "Duyệt điều chỉnh việc của hồ sơ $($p.fullName) ($user)" | Out-Null
        Write-Scenario "$($p.fullName): $($ScenarioName[$key]) ($($p.username) đề nghị, $user duyệt)"
        Set-Done $key
        return
    }
    Fail "Duyệt điều chỉnh việc của hồ sơ $($p.fullName) không thành công: không tài khoản nào duyệt được."
}

function Invoke-Revoke($manager, $p) {
    $key = 'thu-hoi'
    if ((Test-Done $key) -or -not (Select-ForScenario $key $p)) { return }
    $user = Find-Action ([string]$p.recordId) 'RevokeTaskApproval'
    if (-not $user) { Fail "Thu hồi duyệt danh mục của $($p.fullName): không tài khoản nào có thao tác thu hồi." }
    $session = Get-Session $user
    $record = Get-Record $session $p.recordId
    $result = Invoke-ApiOk $session 'POST' "/api/evaluations/records/$($p.recordId)/tasks/approve/revoke" @{ version = $record.version } "Thu hồi duyệt danh mục của $($p.fullName) ($user)"
    Write-Scenario "$($p.fullName): $($ScenarioName[$key]) ($user) -> $($result.status)"
    Set-Done $key
    Push-Record $manager ([string]$p.recordId) ([array]::IndexOf($Steps, 'B2_SELF_SCORE'))
}

# Gọi trước khi làm bước $step của hồ sơ; chỉ áp dụng diện cơ sở (đủ các bước trong hệ thống)
function Invoke-Scenarios($manager, $p, [string]$step) {
    if ($p.workflowProfileCode -ne 'co-so' -or (Test-KeyAccount $p.username)) { return }
    if ($OpinionAt.ContainsKey($step)) { Invoke-Opinion $p $step }
    if ($ReturnAt.Contains($step)) { Invoke-Return $manager $p $step }
    if ($step -eq 'B2_SELF_SCORE') {
        Invoke-Revoke $manager $p
        Invoke-TaskChange $manager $p
    }
}

function Invoke-NotRatedScenario($manager) {
    $key = 'chua-xep-loai'
    if (Test-Done $key) { return }
    $p = @(Get-Participants $manager $script:Period.id | Where-Object {
            $_.workflowProfileCode -eq 'co-so' -and $_.currentStep -eq 'B1_REGISTER' -and $GiuLai -notcontains $_.username -and -not (Test-KeyAccount $_.username)
        }) | Select-Object -Last 1
    if (-not $p -or -not (Select-ForScenario $key $p)) { return }
    $record = Get-Record $manager $p.recordId
    $result = Invoke-ApiOk $manager 'POST' "/api/evaluations/records/$($p.recordId)/not-rated" @{ version = $record.version; reason = 'Đi đào tạo dài hạn trong quý' } "Đánh dấu Chưa xếp loại $($p.fullName)"
    Write-Scenario "$($p.fullName): $($ScenarioName[$key]) ($ManagerUser) -> $($result.status)"
    Set-Done $key
}

# Sau công bố: mở lại một hồ sơ diện Phó Giám đốc về bước nhận xét rồi đi lại tới công bố (không vướng biên bản bắt buộc)
function Invoke-ReopenScenario($manager) {
    $key = 'mo-lai'
    if (Test-Done $key) { return }
    $p = @(Get-Participants $manager $script:Period.id | Where-Object {
            $_.workflowProfileCode -eq 'pho-giam-doc' -and $_.status -eq 'Published' -and $GiuLai -notcontains $_.username
        }) | Select-Object -First 1
    if (-not $p -or -not (Select-ForScenario $key $p)) { return }
    $user = Find-Action ([string]$p.recordId) 'Reopen'
    if (-not $user) { Fail "Mở lại hồ sơ $($p.fullName): không tài khoản nào có thao tác mở lại." }
    $session = Get-Session $user
    $record = Get-Record $session $p.recordId
    $result = Invoke-ApiOk $session 'POST' "/api/evaluations/records/$($p.recordId)/reopen" ([ordered]@{
        version = $record.version; reason = 'Thử mở lại để bổ sung nhận xét'; targetStep = 'B3C_DIRECTOR'
    }) "Mở lại hồ sơ $($p.fullName) ($user)"
    Write-Scenario "$($p.fullName): mở lại ($user) -> $($result.status)"
    Push-Record $manager ([string]$p.recordId) $Steps.Count
    Write-Scenario "$($p.fullName): đã công bố lại"
    Set-Done $key
}

function Show-Scenarios($manager) {
    if ($script:State.Count -eq 0) { return }
    $all = @(Get-Participants $manager $script:Period.id)
    Write-Step 'Tình huống đã làm'
    foreach ($key in $ScenarioName.Keys) {
        if (-not $script:State.ContainsKey($key)) { continue }
        $p = $all | Where-Object { [string]$_.recordId -eq $script:State[$key] } | Select-Object -First 1
        Write-Host ("   {0,-62} {1}" -f $ScenarioName[$key], "$($p.fullName) ($($p.username))")
    }
}

# ---------------------------------------------------------------- Làm sạch
if ($LamSach) {
    # Không CASCADE để không lỡ xóa bảng gốc; API không xóa được kỳ nên làm rỗng thẳng các bảng vận hành (docker/du-lieu-goc.txt), giữ nguyên dữ liệu gốc.
    if ($CsdlDev -ne 'congtacdang_dev' -and $CsdlDev -notlike 'ctd_*') { Fail "Chỉ làm sạch CSDL thử (congtacdang_dev hoặc ctd_*), không làm sạch '$CsdlDev'." }
    $repoRoot = Split-Path $PSScriptRoot -Parent
    $envFile = Join-Path $repoRoot 'docker/.env'
    if (-not (Test-Path $envFile)) { Fail "Thiếu $envFile để lấy tài khoản CSDL." }
    $pgUser = ((Get-Content $envFile | Where-Object { $_ -match '^POSTGRES_USER=' }) -replace '^POSTGRES_USER=', '').Trim()
    $tables = Get-Content (Join-Path $repoRoot 'docker/du-lieu-goc.txt') -Encoding UTF8 |
        Where-Object { $_ -like 'VAN_HANH|*' } | ForEach-Object { '"' + ($_.Split('|')[1]) + '"' }
    if ($tables.Count -eq 0) { Fail 'Không đọc được danh sách bảng vận hành trong docker/du-lieu-goc.txt.' }
    Write-Step "Làm sạch dữ liệu vận hành của CSDL $CsdlDev ($($tables.Count) bảng: kỳ, hồ sơ, biên bản, phiếu, nhật ký...)"
    $sql = "TRUNCATE TABLE $($tables -join ', ') RESTART IDENTITY;"
    & docker exec $PgContainer psql -U $pgUser -d $CsdlDev -v ON_ERROR_STOP=1 -c $sql
    if ($LASTEXITCODE -ne 0) { Fail 'Làm sạch không thành công: lệnh psql báo lỗi.' }
    Write-Host 'Đã làm sạch. Tài khoản, danh mục, vai trò, bộ tiêu chí giữ nguyên. Tải lại trang trên trình duyệt.' -ForegroundColor Green
    exit 0
}
$manager = Get-Session $ManagerUser

# ---------------------------------------------------------------- Tạo / dùng lại kỳ
$GiuLai = @($GiuLai | ForEach-Object { $_ -split '[,;\s]+' } | Where-Object { $_ })
$targetIndex = if ($GiaiDoan -eq 'xong') { $Steps.Count } else { [array]::IndexOf($Steps, $StageStep[$GiaiDoan]) }
$period = Find-Period $manager
if ($TaoKy -and $period) {
    Write-Host "Đã có kỳ `"$($period.name)`", dùng kỳ này." -ForegroundColor DarkYellow
    $TaoKy = $false; $KyCoSan = $true
}
if ($TaoKy) {
    $period = New-Period $manager
    Write-Host "   Mã kỳ: $($period.id)"
    Write-Step 'Đặt lại hạn các bước (không bước nào quá hạn so với hôm nay)'
    $period = Update-Deadlines $manager $period
    Write-Step 'Thêm cán bộ giữ chức vụ thuộc diện đánh giá'
    Add-InScope $manager $period
    Open-Period $manager $period
    $KyCoSan = $true
    $period = Find-Period $manager
}
if ($KyCoSan) {
    if (-not $period) { Fail "Không tìm thấy kỳ Quý $Quy/$Nam. Muốn tạo kỳ mới thì tích ô Tạo kỳ mới nếu chưa có (hoặc dùng -TaoKy)." }
    if ($period.status -ne 'Open') { Fail "Kỳ `"$($period.name)`" đang ở trạng thái $($period.statusDisplayName), cần đang mở." }
    $all = @(Get-Participants $manager $period.id)
    if ($all.Count -eq 0) { Fail "Kỳ `"$($period.name)`" chưa có người được đánh giá." }
    $Accounts = @(@($Accounts) + @($all | ForEach-Object { $_.username }) | Select-Object -Unique)
    $missing = @($GiuLai | Where-Object { $all.username -notcontains $_ })
    if ($missing.Count -gt 0) { Fail "Không có trong kỳ: $($missing -join ', ')." }
    Write-Step "Đẩy tiếp kỳ `"$($period.name)`" ($($all.Count) hồ sơ, giữ lại $($GiuLai.Count))"
    $count = $all.Count
} elseif ($period) {
    $count = @(Get-Participants $manager $period.id).Count
    if ($period.status -notin @('Draft', 'Open')) { Fail "Kỳ `"$($period.name)`" đang ở trạng thái $($period.statusDisplayName), không dùng được để thử." }
    if ($count -eq 0) { Write-Step "Dùng lại kỳ `"$($period.name)`" (chưa có hồ sơ)" }
    else { Write-Step "Đẩy tiếp kỳ `"$($period.name)`" ($count hồ sơ)" }
} else {
    $period = New-Period $manager
    $count = 0
}
$script:Period = $period

if ($count -eq 0 -and -not $KyCoSan) {
    Write-Step 'Đặt lại hạn các bước (không bước nào quá hạn so với hôm nay)'
    $period = Update-Deadlines $manager $period
    Write-Step 'Thêm người được đánh giá'
    Add-Evaluated $manager $period
    Open-Period $manager $period
}
$period = Invoke-ApiOk $manager 'GET' "/api/evaluations/periods/$($period.id)" $null 'Đọc kỳ'
$script:Period = $period
# Cửa sổ chạy script đọc dòng này để gắn tiến độ với kỳ
Write-Host "   Mã kỳ: $($period.id)"
Set-NotRated $manager $period
if ($TinhHuong) { Load-State $period; Invoke-NotRatedScenario $manager }

# ---------------------------------------------------------------- Đẩy hồ sơ tới giai đoạn chọn
Write-Step "Đẩy hồ sơ tới giai đoạn $GiaiDoan"
foreach ($step in $Steps) {
    $index = [array]::IndexOf($Steps, $step)
    if ($step -eq 'B4_DECISION' -and $targetIndex -ge $index) { Complete-Gates $manager $period }
    if ($index -ge $targetIndex) { break }
    foreach ($p in @(Get-Participants $manager $period.id | Where-Object { $_.currentStep -eq $step -and $GiuLai -notcontains $_.username })) {
        if ($TinhHuong) {
            Invoke-Scenarios $manager $p $step
            $p = Get-Participant $manager ([string]$p.recordId)
            if ($p.currentStep -ne $step) { continue }
        }
        Invoke-RecordStep $manager $p $step
    }
}
if ($TinhHuong -and $GiaiDoan -eq 'xong') { Invoke-ReopenScenario $manager }
if ($TinhHuong) { Show-Scenarios $manager }

if ($KyCoSan) {
    # Kỳ đông người: chỉ tóm tắt theo trạng thái, người làm tiếp xem ở Việc cần xử lý
    Write-Step 'Hiện trạng hồ sơ'
    Get-Participants $manager $period.id | Group-Object statusDisplayName | Sort-Object Name |
        ForEach-Object { [pscustomobject]@{ 'Trạng thái' = $_.Name; 'Số hồ sơ' = $_.Count } } | Format-Table -AutoSize | Out-String | Write-Host
    if ($GiuLai.Count -gt 0) { Write-Host "Giữ lại để thao tác tay: $($GiuLai -join ', ')" }
    Write-Host 'Xong. Người làm tiếp xem ở Việc cần xử lý trên giao diện.' -ForegroundColor Green
} else {
    Show-Summary $manager $period
    $hint = if ($ShowPassword) { " (mật khẩu $MatKhau)" } else { '' }
    Write-Host "Xong. Mở giao diện, đăng nhập tài khoản ở cột `"Người làm tiếp`"$hint để thao tác tiếp." -ForegroundColor Green
}
