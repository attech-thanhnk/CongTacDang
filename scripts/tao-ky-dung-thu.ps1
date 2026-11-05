<#
  Tạo kỳ dùng thử với người thật (docs/dung-thu/ke-hoach-dung-thu.md) qua API: tạo kỳ dự thảo với bộ tiêu chí đã xuất bản mới nhất,
  thêm người được đánh giá trong nhóm thử, đặt hạn các bước theo lịch 2 tuần, kiểm tra kẹt luồng rồi mới mở kỳ.
  Chỉ gọi API (đi qua kiểm tra quyền), không ghi thẳng CSDL.
  Ví dụ:
    powershell -File scripts/tao-ky-dung-thu.ps1 -KiemTra                       # chỉ in kế hoạch, kiểm tra kẹt luồng nếu đã có kỳ dự thảo
    powershell -File scripts/tao-ky-dung-thu.ps1 -NgayBatDau 2026-11-09
    powershell -File scripts/tao-ky-dung-thu.ps1 -MoBatBuoc 'Lý do mở dù còn cảnh báo'
  Chạy lại được: kỳ cùng tên đã mở thì báo rồi dừng; kỳ cùng tên còn dự thảo (lần trước kẹt luồng) thì dùng lại, thêm người còn thiếu,
  đặt lại hạn, kiểm tra và mở.
#>
param(
    # Địa chỉ giao diện (API qua /api) hoặc backend trực tiếp
    [string]$ApiUrl = 'http://192.168.22.149:3001',
    # Người lập kỳ, cần quyền quản lý kỳ
    [string]$TaiKhoan = 'loan.ttp',
    # Bỏ trống thì hỏi khi chạy
    [string]$MatKhau,
    [string]$Ten = 'Kỳ dùng thử',
    # Ngày đầu tuần 1 của lịch dùng thử
    [datetime]$NgayBatDau = (Get-Date).Date,
    # Quý của kỳ; bỏ trống thì lấy theo -NgayBatDau (mỗi quý chỉ có một kỳ)
    [int]$Nam = 0,
    [int]$Quy = 0,
    [switch]$KiemTra,
    # Lý do mở kỳ dù còn cảnh báo kẹt luồng
    [string]$MoBatBuoc,
    # Nhóm thử giữ chức vụ lãnh đạo quản lý hoặc Bí thư/Phó Bí thư chi bộ (bỏ quantri); ai không thuộc diện theo chức vụ thì hệ thống báo bỏ qua
    [string[]]$NguoiDuocDanhGia = @(
        'thinh.lt', 'giang.nh', 'thanh.nn',
        'duy.lm', 'dao.nta', 'trung.nb',
        'tuan.nm', 'hien.lv', 'vuong.dh', 'cuong.nt', 'hung.nx',
        'trang.npq', 'huyen.utt',
        'loan.ttp', 'loan.ctp',
        'hung.dt', 'long.nc', 'thao.nt'
    )
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

# Hạn từng bước = ngày làm việc thứ N kể từ -NgayBatDau (ngày 1). Tuần 1: ngày 1-5, tuần 2: ngày 6-10.
# Bước mới chưa có trong bảng nhận hạn của bước đứng sau nó (script báo để bổ sung).
$LichBuoc = [ordered]@{
    'B1_REGISTER'     = @{ Ngay = 2;  Ten = 'Đăng ký sản phẩm' }
    'B1_UNIT_SUBMIT'  = @{ Ngay = 2;  Ten = 'Trưởng đơn vị gửi danh mục' }
    'B1_HR_REVIEW'    = @{ Ngay = 3;  Ten = 'Rà soát, trình duyệt danh mục' }
    'B1_APPROVE'      = @{ Ngay = 3;  Ten = 'Duyệt danh mục' }
    'B2_SELF_SCORE'   = @{ Ngay = 4;  Ten = 'Tự chấm' }
    'B2_CELL_CONFIRM' = @{ Ngay = 5;  Ten = 'Chi bộ xác nhận' }
    'B3A_COLLECTIVE'  = @{ Ngay = 6;  Ten = 'Tập thể đề xuất' }
    'B3B_APPRAISAL'   = @{ Ngay = 7;  Ten = 'Thẩm định' }
    'B3C_DIRECTOR'    = @{ Ngay = 8;  Ten = 'Nhận xét' }
    'B4_DECISION'     = @{ Ngay = 9;  Ten = 'Quyết định (sau họp thống nhất, phiếu kiểm tra)' }
    'B5_PUBLISH'      = @{ Ngay = 10; Ten = 'Công bố' }
}

$ApiUrl = $ApiUrl.TrimEnd('/')
# powershell -File truyền "a,b,c" thành một chuỗi
$NguoiDuocDanhGia = @($NguoiDuocDanhGia | ForEach-Object { $_ -split '[,;\s]+' } | Where-Object { $_ })
$Ten = $Ten.Trim()
$Start = $NgayBatDau.Date
if ($Nam -eq 0) { $Nam = $Start.Year }
if ($Quy -eq 0) { $Quy = [int][Math]::Ceiling($Start.Month / 3) }
if ($Quy -lt 1 -or $Quy -gt 4) { Write-Host 'Quý phải từ 1 đến 4.' -ForegroundColor Red; exit 1 }

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

# Trả về @{ Status; Ok; Data; Message }; không ném lỗi theo mã HTTP để người gọi tự quyết
function Invoke-Api([string]$method, [string]$path, $body = $null) {
    $params = @{ Uri = "$ApiUrl$path"; Method = $method; UseBasicParsing = $true; TimeoutSec = 120; WebSession = $script:Session }
    if ($null -ne $body) {
        $json = $body | ConvertTo-Json -Depth 30 -Compress
        $params.Body = [System.Text.Encoding]::UTF8.GetBytes($json)
        $params.ContentType = 'application/json; charset=utf-8'
    }
    $status = 0; $text = ''
    try {
        $r = Invoke-WebRequest @params
        $status = [int]$r.StatusCode
        $text = [System.Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
    } catch [System.Net.WebException] {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { Fail "Kết nối $ApiUrl không thành công: $($_.Exception.Message)" }
        $status = [int]$resp.StatusCode
        $text = Read-Body $resp
    }
    $parsed = $null
    if ($text) { try { $parsed = $text | ConvertFrom-Json } catch { $parsed = $null } }
    $message = if ($parsed -and $parsed.message) { $parsed.message } else { $text }
    return @{ Status = $status; Ok = ($status -ge 200 -and $status -lt 300); Data = $(if ($parsed) { $parsed.data } else { $null }); Message = $message }
}

function Invoke-ApiOk([string]$method, [string]$path, $body, [string]$what) {
    $r = Invoke-Api $method $path $body
    if (-not $r.Ok) { Fail "$what không thành công: HTTP $($r.Status) - $($r.Message)" }
    return $r.Data
}

function Format-Day([datetime]$d) {
    $thu = @('CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7')[[int]$d.DayOfWeek]
    return "$thu $($d.ToString('dd/MM/yyyy'))"
}

# Ngày làm việc thứ $n tính từ $Start (ngày 1 = $Start nếu là ngày làm việc), bỏ thứ Bảy, Chủ nhật, ngày nghỉ
function Get-WorkingDay([int]$n) {
    $d = $Start
    $count = 0
    while ($true) {
        if ($d.DayOfWeek -ne 'Saturday' -and $d.DayOfWeek -ne 'Sunday' -and -not $script:Holidays.ContainsKey($d.ToString('yyyy-MM-dd'))) {
            $count++
            if ($count -ge $n) { return $d }
        }
        $d = $d.AddDays(1)
    }
}

# Mã bước theo thứ tự trong cấu hình kỳ → ngày hạn; bước lạ lấy hạn của bước có trong bảng đứng sau nó
function Get-StepPlan([string[]]$codes) {
    $plan = [ordered]@{}
    for ($i = 0; $i -lt $codes.Count; $i++) {
        $code = $codes[$i]
        if ($LichBuoc.Contains($code)) {
            $plan[$code] = @{ Ngay = Get-WorkingDay $LichBuoc[$code].Ngay; Ten = $LichBuoc[$code].Ten }
            continue
        }
        $next = $null
        for ($j = $i + 1; $j -lt $codes.Count; $j++) { if ($LichBuoc.Contains($codes[$j])) { $next = $codes[$j]; break } }
        if (-not $next) { for ($j = $i - 1; $j -ge 0; $j--) { if ($LichBuoc.Contains($codes[$j])) { $next = $codes[$j]; break } } }
        $day = if ($next) { $LichBuoc[$next].Ngay } else { 10 }
        Write-Host "   Lưu ý: bước $code chưa có trong bảng `$LichBuoc đầu script, tạm đặt hạn như bước $next." -ForegroundColor DarkYellow
        $plan[$code] = @{ Ngay = Get-WorkingDay $day; Ten = $code }
    }
    return $plan
}

function Get-StepCodes($period) {
    if ($period -and $period.settings -and @($period.settings.profiles).Count -gt 0) {
        return @(@($period.settings.profiles)[0].steps.PSObject.Properties | ForEach-Object { $_.Name })
    }
    return @($LichBuoc.Keys)
}

function Show-Plan($plan, $period) {
    $rows = foreach ($code in $plan.Keys) {
        $row = [ordered]@{ 'Bước' = $code; 'Việc' = $plan[$code].Ten; 'Hạn' = Format-Day $plan[$code].Ngay }
        if ($period) {
            $on = @($period.settings.profiles | Where-Object { $_.steps.$code -and $_.steps.$code.mode -ne 'Off' } | ForEach-Object { $_.name })
            $row['Áp dụng'] = if ($on.Count -eq @($period.settings.profiles).Count) { 'mọi diện' } elseif ($on.Count -eq 0) { '(không diện nào)' } else { $on -join '; ' }
        }
        [pscustomobject]$row
    }
    $rows | Format-Table -AutoSize -Wrap | Out-String -Width 200 | Write-Host
}

# Gom cảnh báo theo bước để thấy ngay bước nào thiếu người, ở đâu
function Show-Readiness($readiness) {
    $issues = @($readiness.issues)
    if ($issues.Count -eq 0) {
        Write-Host "   Không có cảnh báo kẹt luồng ($($readiness.checkedRecords) hồ sơ)." -ForegroundColor Green
    } else {
        Write-Host "   Còn $($issues.Count) cảnh báo: bước chưa có người thực hiện." -ForegroundColor Red
        foreach ($g in ($issues | Group-Object { "$($_.stepName)|$($_.scopeName)|$($_.permissionName)" })) {
            $first = $g.Group[0]
            $names = ($g.Group | ForEach-Object { $_.fullName } | Select-Object -Unique) -join ', '
            $roles = if (@($first.roleNames).Count -gt 0) { " Gán một trong: $(@($first.roleNames) -join ', ')." } else { '' }
            Write-Host "   - $($first.stepName) tại $($first.scopeName) (quyền $($first.permissionName)): $names.$roles" -ForegroundColor Red
        }
    }
    foreach ($item in @($readiness.dataReview)) {
        # Chỉ nhắc, không chặn mở kỳ
        Write-Host "   Lưu ý: $($item.title). $($item.hint)" -ForegroundColor DarkYellow
        foreach ($n in @($item.names)) { Write-Host "     - $n" -ForegroundColor DarkYellow }
    }
}

function Show-Participants($periodId) {
    $rows = foreach ($p in @(Invoke-ApiOk 'GET' "/api/evaluations/periods/$periodId/participants" $null 'Lấy danh sách người được đánh giá')) {
        [pscustomobject]@{ 'Người được đánh giá' = "$($p.fullName) ($($p.username))"; 'Đơn vị' = $p.departmentName; 'Diện quyết định' = $p.workflowProfileName; 'Trạng thái' = $p.statusDisplayName }
    }
    $rows | Format-Table -AutoSize -Wrap | Out-String -Width 200 | Write-Host
}

# ---------------------------------------------------------------- Đăng nhập
if (-not $MatKhau) {
    $secure = Read-Host "Mật khẩu của $TaiKhoan" -AsSecureString
    $MatKhau = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}
$script:Session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$login = Invoke-Api 'POST' '/api/auth/login' @{ username = $TaiKhoan; password = $MatKhau }
if (-not $login.Ok) { Fail "Đăng nhập $TaiKhoan không thành công: HTTP $($login.Status) - $($login.Message)" }
if ($login.Data.mustChangePassword -or $login.Data.user.mustChangePassword) {
    Fail "Đăng nhập $TaiKhoan không thành công: đang dùng mật khẩu tạm. Hãy đăng nhập giao diện, đổi mật khẩu rồi chạy lại."
}

$script:Holidays = @{}
foreach ($y in @($Start.Year, $Start.AddDays(30).Year) | Select-Object -Unique) {
    foreach ($h in @(Invoke-ApiOk 'GET' "/api/holidays?year=$y" $null 'Lấy danh mục ngày nghỉ')) { $script:Holidays[[string]$h.date] = $true }
}

# ---------------------------------------------------------------- Tìm kỳ
$periods = @(Invoke-ApiOk 'GET' '/api/evaluations/periods' $null 'Lấy danh sách kỳ đánh giá')
$period = $periods | Where-Object { $_.name.Trim() -eq $Ten } | Select-Object -First 1
if ($period -and $period.status -ne 'Draft') {
    Write-Host "Đã có kỳ `"$($period.name)`" ($($period.statusDisplayName), $($period.totalRecords) hồ sơ). Không tạo lại." -ForegroundColor Yellow
    exit 0
}
if (-not $period) {
    $sameQuarter = $periods | Where-Object { $_.year -eq $Nam -and $_.quarter -eq $Quy } | Select-Object -First 1
    if ($sameQuarter) {
        Fail "Tạo kỳ không thành công: quý $Quy/$Nam đã có kỳ `"$($sameQuarter.name)`" (mỗi quý một kỳ). Chọn quý khác bằng -Nam, -Quy."
    }
}

$sets = @(Invoke-ApiOk 'GET' '/api/criteria-sets' $null 'Lấy danh sách bộ tiêu chí')
$set = $sets | Where-Object { $_.status -eq 'Published' } | Sort-Object publishedAt -Descending | Select-Object -First 1
if (-not $set -and -not $period) { Fail 'Tạo kỳ không thành công: chưa có bộ tiêu chí đã xuất bản.' }

# ---------------------------------------------------------------- Kế hoạch
Write-Step "Kế hoạch: kỳ `"$Ten`", quý $Quy/$Nam, bắt đầu $(Format-Day $Start), người lập $TaiKhoan"
if ($period) { Write-Host "   Đã có kỳ dự thảo cùng tên ($($period.totalRecords) hồ sơ): dùng lại, không tạo mới." }
if ($set) { Write-Host "   Bộ tiêu chí: $($set.name) ($($set.code), xuất bản $(([datetime]$set.publishedAt).ToLocalTime().ToString('dd/MM/yyyy')))" }
Write-Host "   Người được đánh giá ($($NguoiDuocDanhGia.Count)): $($NguoiDuocDanhGia -join ', ')"
$plan = Get-StepPlan (Get-StepCodes $period)
Show-Plan $plan $period

if ($KiemTra) {
    if ($period) {
        Write-Step 'Kiểm tra kẹt luồng trên kỳ dự thảo hiện có'
        Show-Readiness (Invoke-ApiOk 'GET' "/api/evaluations/periods/$($period.id)/readiness" $null 'Kiểm tra kẹt luồng')
    } else {
        Write-Host 'Chưa có kỳ nên chưa kiểm tra kẹt luồng được. Chạy không có -KiemTra: kỳ được tạo ở dạng dự thảo, chỉ mở khi không còn cảnh báo.' -ForegroundColor Yellow
    }
    Write-Host 'Chế độ -KiemTra: không tạo, không sửa, không mở kỳ.' -ForegroundColor Green
    exit 0
}

# ---------------------------------------------------------------- Tạo kỳ dự thảo
if (-not $period) {
    Write-Step 'Tạo kỳ dự thảo'
    $quarterStart = Get-Date -Year $Nam -Month (($Quy - 1) * 3 + 1) -Day 1
    $quarterStart = $quarterStart.Date
    $end = $quarterStart.AddMonths(3).AddDays(-1)
    $lastDeadline = ($plan.Values | ForEach-Object { $_.Ngay } | Measure-Object -Maximum).Maximum
    if ($lastDeadline -gt $end) { $end = $lastDeadline }
    $period = Invoke-ApiOk 'POST' '/api/evaluations/periods' ([ordered]@{
        year = $Nam; quarter = $Quy; name = $Ten; criteriaSetId = $set.id
        startDate = $quarterStart.ToString('yyyy-MM-ddT00:00:00Z'); endDate = $end.ToString('yyyy-MM-ddT00:00:00Z')
    }) 'Tạo kỳ đánh giá'
    Write-Host "   Đã tạo kỳ `"$($period.name)`"."
}

# ---------------------------------------------------------------- Người được đánh giá
Write-Step 'Thêm người được đánh giá (diện quyết định theo chức vụ)'
$candidates = @(Invoke-ApiOk 'GET' "/api/evaluations/periods/$($period.id)/candidates" $null 'Lấy danh sách cán bộ')
$ids = @()
foreach ($u in $NguoiDuocDanhGia) {
    $c = $candidates | Where-Object { $_.username -eq $u } | Select-Object -First 1
    if (-not $c) { Write-Host "   Bỏ qua $u`: không có trong danh sách cán bộ." -ForegroundColor DarkYellow; continue }
    if (-not $c.inScope) { Write-Host "   Bỏ qua $($c.fullName) ($u): chức vụ không thuộc diện đánh giá." -ForegroundColor DarkYellow; continue }
    if (-not $c.alreadyAdded) { $ids += $c.memberId }
}
if ($ids.Count -gt 0) {
    $added = Invoke-ApiOk 'POST' "/api/evaluations/periods/$($period.id)/participants" @{ memberIds = $ids } 'Thêm người được đánh giá'
    Write-Host "   Đã thêm $($added.added) người."
    foreach ($s in @($added.skipped)) { Write-Host "   Bỏ qua: $s" -ForegroundColor DarkYellow }
} else {
    Write-Host '   Không có người mới cần thêm.'
}

# ---------------------------------------------------------------- Hạn các bước
Write-Step 'Đặt hạn các bước theo lịch dùng thử'
$period = Invoke-ApiOk 'GET' "/api/evaluations/periods/$($period.id)" $null 'Đọc kỳ'
$plan = Get-StepPlan (Get-StepCodes $period)
foreach ($flow in @($period.settings.profiles)) {
    foreach ($prop in $flow.steps.PSObject.Properties) {
        $setting = $prop.Value
        if (-not $setting -or $setting.mode -eq 'Off' -or -not $plan.Contains($prop.Name)) { continue }
        $setting | Add-Member -NotePropertyName deadline -NotePropertyValue ($plan[$prop.Name].Ngay.ToString('yyyy-MM-dd')) -Force
    }
}
$period = Invoke-ApiOk 'PUT' "/api/evaluations/periods/$($period.id)" ([ordered]@{ version = $period.version; settings = $period.settings }) 'Đặt hạn các bước'
Show-Plan $plan $period

# ---------------------------------------------------------------- Kiểm tra kẹt luồng, mở kỳ
Write-Step 'Kiểm tra kẹt luồng'
$readiness = Invoke-ApiOk 'GET' "/api/evaluations/periods/$($period.id)/readiness" $null 'Kiểm tra kẹt luồng'
Show-Readiness $readiness
if (-not $readiness.ready -and -not $MoBatBuoc) {
    Show-Participants $period.id
    Write-Host 'Mở kỳ không thành công: còn bước chưa có người thực hiện. Kỳ vẫn ở dạng dự thảo; gán người thực hiện rồi chạy lại, hoặc thêm -MoBatBuoc kèm lý do.' -ForegroundColor Red
    exit 1
}

Write-Step 'Mở kỳ'
$period = Invoke-ApiOk 'GET' "/api/evaluations/periods/$($period.id)" $null 'Đọc kỳ'
$body = [ordered]@{ version = $period.version }
if (-not $readiness.ready) { $body['force'] = $true; $body['reason'] = $MoBatBuoc }
$r = Invoke-Api 'POST' "/api/evaluations/periods/$($period.id)/open" $body
if ($r.Status -eq 409 -and $r.Data -and $null -ne $r.Data.issues) {
    Show-Readiness $r.Data
    Fail 'Mở kỳ không thành công: còn bước chưa có người thực hiện.'
}
if (-not $r.Ok) { Fail "Mở kỳ không thành công: HTTP $($r.Status) - $($r.Message)" }

Show-Participants $period.id
Write-Host "Đã mở kỳ `"$($r.Data.name)`" với $($r.Data.totalRecords) hồ sơ. Người được đánh giá đăng nhập để đăng ký sản phẩm trước $(Format-Day @($plan.Values)[0].Ngay)." -ForegroundColor Green
