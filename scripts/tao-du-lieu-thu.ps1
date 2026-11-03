<#
  Tạo nhanh dữ liệu thử trên máy phát triển: một kỳ Quý IV/2026 với 4 hồ sơ, đẩy mọi hồ sơ tới giai đoạn chọn
  rồi dừng để tự thao tác tiếp trên giao diện. Chỉ gọi API (đi qua kiểm tra quyền và luật luồng), không ghi thẳng CSDL.
  Ví dụ:
    powershell -File scripts/tao-du-lieu-thu.ps1 -GiaiDoan tham-dinh
    powershell -File scripts/tao-du-lieu-thu.ps1 -GiaiDoan cong-bo -TiepTuc     # đẩy tiếp kỳ thử đã có
    powershell -File scripts/tao-du-lieu-thu.ps1 -LamSach
  Giai đoạn (hồ sơ dừng ở bước này, chờ người làm tiếp):
    dang-ky, tu-cham, tap-the, tham-dinh, nhan-xet, quyet-dinh, cong-bo
  Mỗi bước dùng tài khoản đầu tiên trong -TaiKhoan có nút thao tác trên hồ sơ (theo API actions).
  API không có chức năng xóa kỳ: -LamSach chỉ bỏ được hồ sơ chưa có dữ liệu; kỳ rỗng được dùng lại ở lần chạy sau.
#>
param(
    [ValidateSet('dang-ky', 'tu-cham', 'tap-the', 'tham-dinh', 'nhan-xet', 'quyet-dinh', 'cong-bo')]
    [string]$GiaiDoan,
    [switch]$LamSach,
    # Kỳ thử đã có hồ sơ: đẩy tiếp các hồ sơ đang có thay vì dừng
    [switch]$TiepTuc,
    [string]$ApiUrl = 'http://localhost:5000',
    # Tài khoản được thử lần lượt cho mỗi bước (huyen.utt: Chi bộ Phòng Tài chính - Kế toán của thanh.nn)
    [string[]]$TaiKhoan = @('giang.nh', 'thinh.lt', 'duy.lm', 'dao.nta', 'trung.nb', 'thanh.nn', 'loan.ttp', 'loan.ctp', 'long.nc', 'hung.dt', 'huyen.utt'),
    # Mật khẩu thử chỉ dùng trên máy phát triển
    [string]$MatKhau = 'Thu12345a'
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

if (-not $LamSach -and -not $GiaiDoan) {
    Write-Host 'Hãy chọn -GiaiDoan <dang-ky|tu-cham|tap-the|tham-dinh|nhan-xet|quyet-dinh|cong-bo> hoặc -LamSach.' -ForegroundColor Yellow
    exit 1
}

$Year = 2026
$Quarter = 4
$PeriodName = 'Đánh giá, xếp loại cán bộ Quý IV/2026'
$ManagerUser = 'loan.ttp'
$Accounts = $TaiKhoan
$Evaluated = @('duy.lm', 'dao.nta', 'trung.nb', 'thanh.nn')
$CalibrationPreparer = 'loan.ctp'
$CalibrationConcluders = @('long.nc', 'hung.dt')
$Steps = @('B1_REGISTER', 'B1_APPROVE', 'B2_SELF_SCORE', 'B2_CELL_CONFIRM', 'B3A_COLLECTIVE', 'B3B_APPRAISAL', 'B3C_DIRECTOR', 'B4_DECISION', 'B5_PUBLISH')
$StageStep = @{
    'dang-ky' = 'B1_REGISTER'; 'tu-cham' = 'B2_SELF_SCORE'; 'tap-the' = 'B3A_COLLECTIVE'; 'tham-dinh' = 'B3B_APPRAISAL'
    'nhan-xet' = 'B3C_DIRECTOR'; 'quyet-dinh' = 'B4_DECISION'; 'cong-bo' = 'B5_PUBLISH'
}
# Hành động trong hệ thống ứng với bước, và đường dẫn API của nó
$StepAction = @{
    'B1_REGISTER' = 'SubmitTasks'; 'B1_APPROVE' = 'ApproveTasks'; 'B2_SELF_SCORE' = 'SubmitSelfScore'; 'B2_CELL_CONFIRM' = 'ConfirmByCell'
    'B3A_COLLECTIVE' = 'RecordCollectiveProposal'; 'B3B_APPRAISAL' = 'Appraise'; 'B3C_DIRECTOR' = 'DirectorReview'
    'B4_DECISION' = 'RecordDecision'; 'B5_PUBLISH' = 'Publish'
}
$ActionPath = @{
    'SubmitTasks' = 'tasks/submit'; 'ApproveTasks' = 'tasks/approve'; 'SubmitSelfScore' = 'self-score/submit'; 'ConfirmByCell' = 'cell/confirm'
    'RecordCollectiveProposal' = 'collective'; 'Appraise' = 'appraisal'; 'DirectorReview' = 'director-review'
    'RecordDecision' = 'decision'; 'Publish' = 'publish'
}
# Ngày làm việc kể từ hôm nay làm hạn tối thiểu của từng bước, để không bước nào quá hạn
$DeadlineFloor = @{
    'B1_REGISTER' = 2; 'B1_APPROVE' = 3; 'B2_SELF_SCORE' = 5; 'B2_CELL_CONFIRM' = 6; 'B3A_COLLECTIVE' = 7
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

# Tài khoản đầu tiên (theo thứ tự danh sách) có nút cho bước; hệ thống tự loại chủ hồ sơ và xung đột lợi ích
function Find-Performer($recordId, [string]$step) {
    $blocked = $null
    foreach ($user in $Accounts) {
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
    if ($action -eq 'RecordExternal') {
        return [ordered]@{
            authorityName = $ExternalAuthority[$step]; documentNumber = '01/TB-THU'; documentDate = $TodayIso
            comment = 'Ghi nhận kết quả của cấp trên'; grade = 'HoanThanhTot'
        }
    }
    switch ($step) {
        'B1_REGISTER' { return New-TaskList $criteria $record.fullName }
        'B1_APPROVE' { return @{ comment = 'Đồng ý danh mục sản phẩm đăng ký' } }
        'B2_SELF_SCORE' { return New-SelfScore $criteria $record $username }
        'B2_CELL_CONFIRM' { return @{ comment = 'Chi bộ xác nhận phiếu tự chấm' } }
        'B3A_COLLECTIVE' { return @{ proposedGrade = 'HoanThanhTot'; comment = 'Tập thể lãnh đạo thống nhất đề xuất' } }
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
                finalGrade = 'HoanThanhTot'; documentNumber = '01-QĐ/ĐU'; documentDate = $TodayIso
                explanation = 'Theo kết quả thẩm định và nhận xét'
            }
        }
        default { Fail "Bước $step chưa được hỗ trợ trong script." }
    }
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
    $preparer = Get-Session $CalibrationPreparer
    foreach ($group in ($records | Group-Object departmentId)) {
        $ids = @($group.Group | ForEach-Object { $_.recordId })
        $names = ($group.Group | ForEach-Object { $_.fullName }) -join ', '
        Write-Step "Lập biên bản họp thống nhất (Mẫu 20) cho: $names"
        $created = Invoke-ApiOk $preparer 'POST' '/api/calibrations' ([ordered]@{
            periodId = $period.id; departmentId = $group.Group[0].departmentId; groupName = "Cán bộ quản lý $($group.Group[0].departmentName)"; recordIds = $ids
        }) "Lập biên bản họp thống nhất ($CalibrationPreparer)"
        $save = [ordered]@{
            version = $created.version; groupName = $created.groupName; heldAt = (Get-Date).ToUniversalTime().ToString('o'); location = 'Phòng họp'
            chairName = 'Chủ trì cuộc họp'; secretaryName = 'Thư ký cuộc họp'; recordIds = $ids
            attendees = @([ordered]@{ fullName = 'Thành viên dự họp'; capacity = 'Ủy viên'; dissentNote = $null })
            summary = @(); cases = @()
        }
        if ($ids.Count -eq 1) { $save['extraConclusion'] = 'Không có hồ sơ cùng nhóm chức danh để so sánh.' }
        $saved = Invoke-ApiOk $preparer 'PUT' "/api/calibrations/$($created.id)" $save "Lưu biên bản họp thống nhất ($CalibrationPreparer)"
        $owners = @($group.Group | ForEach-Object { $_.username })
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
    foreach ($user in $Accounts) {
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
    foreach ($user in $Accounts) {
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

# Trước bước quyết định: làm các cổng Bắt buộc cho hồ sơ đang chờ quyết định trong hệ thống
function Complete-Gates($manager, $period) {
    $waiting = @(Get-Participants $manager $period.id | Where-Object { $_.currentStep -eq 'B4_DECISION' })
    $calibration = @(); $control = @()
    foreach ($p in $waiting) {
        $flow = Get-ProfileSettings $period $p.workflowProfileCode
        if ($flow.steps.B4_DECISION.mode -ne 'Internal') { continue }
        $record = Get-Record $manager $p.recordId
        if ($flow.gates.CALIBRATION.mode -eq 'Required' -and -not $record.calibrationSessionId) { $calibration += $p }
        if ($flow.gates.CONTROL.mode -eq 'Required') { $control += $p }
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
    foreach ($u in $Evaluated) {
        $c = $candidates | Where-Object { $_.username -eq $u } | Select-Object -First 1
        if (-not $c) { Fail "Thêm người được đánh giá không thành công: không tìm thấy tài khoản $u trong danh sách cán bộ." }
        if (-not $c.alreadyAdded) { $ids += $c.memberId }
    }
    if ($ids.Count -gt 0) {
        $added = Invoke-ApiOk $manager 'POST' "/api/evaluations/periods/$($period.id)/participants" @{ memberIds = $ids } 'Thêm người được đánh giá'
        Write-Host "   Đã thêm $($added.added) người."
    }
    $deputy = Get-Participants $manager $period.id | Where-Object { $_.username -eq 'thanh.nn' }
    if ($deputy -and $deputy.workflowProfileCode -ne 'pho-giam-doc') {
        Write-Host "   Lưu ý: thanh.nn nhận diện $($deputy.workflowProfileCode) theo chức vụ, không phải pho-giam-doc." -ForegroundColor DarkYellow
    }
}

function Show-Summary($manager, $period) {
    Write-Step 'Hiện trạng hồ sơ'
    $rows = foreach ($p in (Get-Participants $manager $period.id)) {
        $next = @()
        foreach ($user in $Accounts) {
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

# ---------------------------------------------------------------- Làm sạch
$manager = Get-Session $ManagerUser
if ($LamSach) {
    $period = Find-Period $manager
    if (-not $period) { Write-Host "Không có kỳ quý $Quarter/$Year, không cần làm sạch."; exit 0 }
    Write-Step "Làm sạch kỳ `"$($period.name)`" ($($period.statusDisplayName))"
    $kept = @()
    foreach ($p in (Get-Participants $manager $period.id)) {
        $r = Invoke-Api $manager 'DELETE' "/api/evaluations/periods/$($period.id)/participants/$($p.recordId)?version=$($p.version)"
        if ($r.Ok) { Write-Host "   Đã bỏ $($p.fullName) khỏi kỳ." }
        elseif ($r.Status -eq 409) { $kept += $p }
        else { Fail "Bỏ $($p.fullName) khỏi kỳ không thành công: HTTP $($r.Status) - $($r.Message)" }
    }
    if ($kept.Count -eq 0) {
        Write-Host 'Kỳ không còn hồ sơ. Lần chạy sau sẽ dùng lại kỳ này (API không có chức năng xóa kỳ).' -ForegroundColor Green
        exit 0
    }
    Write-Host "Còn $($kept.Count) hồ sơ đã có dữ liệu, API không cho bỏ khỏi kỳ và không có chức năng xóa kỳ:" -ForegroundColor Yellow
    foreach ($p in $kept) { Write-Host "   - $($p.fullName): $($p.statusDisplayName)" -ForegroundColor Yellow }
    Write-Host 'Cách xử lý: chạy tiếp bằng -TiepTuc để đẩy kỳ thử tới giai đoạn sau, hoặc làm việc trên bản sao CSDL (docs/kiem-thu-thu-cong.md, mục 1) rồi xóa bản sao.' -ForegroundColor Yellow
    exit 2
}

# ---------------------------------------------------------------- Tạo / dùng lại kỳ
$target = $StageStep[$GiaiDoan]
$targetIndex = [array]::IndexOf($Steps, $target)
$period = Find-Period $manager
if ($period) {
    $count = @(Get-Participants $manager $period.id).Count
    if ($count -gt 0 -and -not $TiepTuc) {
        Write-Host "Đã có kỳ `"$($period.name)`" ($($period.statusDisplayName), $count hồ sơ)." -ForegroundColor Yellow
        Write-Host 'Chạy -LamSach để bỏ hồ sơ khỏi kỳ, hoặc thêm -TiepTuc để đẩy tiếp các hồ sơ đang có tới giai đoạn chọn.' -ForegroundColor Yellow
        exit 2
    }
    if ($period.status -notin @('Draft', 'Open')) { Fail "Kỳ `"$($period.name)`" đang ở trạng thái $($period.statusDisplayName), không dùng được để thử." }
    if ($count -eq 0) { Write-Step "Dùng lại kỳ `"$($period.name)`" (chưa có hồ sơ)" }
    else { Write-Step "Đẩy tiếp kỳ `"$($period.name)`" ($count hồ sơ)" }
} else {
    Write-Step "Tạo kỳ $PeriodName"
    $sets = @(Invoke-ApiOk $manager 'GET' '/api/criteria-sets' $null 'Lấy danh sách bộ tiêu chí')
    $set = $sets | Where-Object { $_.status -eq 'Published' } | Sort-Object publishedAt | Select-Object -First 1
    if (-not $set) { Fail 'Tạo kỳ không thành công: chưa có bộ tiêu chí đã xuất bản.' }
    $period = Invoke-ApiOk $manager 'POST' '/api/evaluations/periods' ([ordered]@{
        year = $Year; quarter = $Quarter; name = $PeriodName; criteriaSetId = $set.id
        startDate = "$Year-10-01T00:00:00Z"; endDate = "$Year-12-31T00:00:00Z"
    }) 'Tạo kỳ đánh giá'
    Write-Host "   Bộ tiêu chí: $($set.name) ($($set.code))"
    $count = 0
}
$script:Period = $period

if ($count -eq 0) {
    Write-Step 'Đặt lại hạn các bước (không bước nào quá hạn so với hôm nay)'
    $period = Update-Deadlines $manager $period
    Write-Step 'Thêm người được đánh giá'
    Add-Evaluated $manager $period
    $readiness = Invoke-ApiOk $manager 'GET' "/api/evaluations/periods/$($period.id)/readiness" $null 'Kiểm tra kẹt luồng'
    if (-not $readiness.ready) { Show-Readiness $readiness; exit 1 }
    if ($period.status -eq 'Draft') {
        Write-Step 'Mở kỳ'
        $period = Invoke-ApiOk $manager 'GET' "/api/evaluations/periods/$($period.id)" $null 'Đọc kỳ'
        $r = Invoke-Api $manager 'POST' "/api/evaluations/periods/$($period.id)/open" @{ version = $period.version }
        if ($r.Status -eq 409 -and $r.Data -and $null -ne $r.Data.issues) { Show-Readiness $r.Data; exit 1 }
        if (-not $r.Ok) { Fail "Mở kỳ không thành công: HTTP $($r.Status) - $($r.Message)" }
    }
}
$period = Invoke-ApiOk $manager 'GET' "/api/evaluations/periods/$($period.id)" $null 'Đọc kỳ'
$script:Period = $period

# ---------------------------------------------------------------- Đẩy hồ sơ tới giai đoạn chọn
Write-Step "Đẩy hồ sơ tới giai đoạn $GiaiDoan"
foreach ($step in $Steps) {
    $index = [array]::IndexOf($Steps, $step)
    if ($step -eq 'B4_DECISION' -and $targetIndex -ge $index) { Complete-Gates $manager $period }
    if ($index -ge $targetIndex) { break }
    foreach ($p in @(Get-Participants $manager $period.id | Where-Object { $_.currentStep -eq $step })) {
        Invoke-RecordStep $manager $p $step
    }
}

Show-Summary $manager $period
Write-Host "Xong. Mở giao diện, đăng nhập tài khoản ở cột `"Người làm tiếp`" (mật khẩu $MatKhau) để thao tác tiếp." -ForegroundColor Green
