# Cửa sổ đẩy hồ sơ của kỳ đang mở tới từng giai đoạn (gọi tao-du-lieu-thu.ps1 -KyCoSan, hoặc -TaoKy để tạo kỳ mới)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$ScriptPath = Join-Path $PSScriptRoot 'tao-du-lieu-thu.ps1'
$StateFile = Join-Path $env:APPDATA 'ctd-day-giai-doan.json'

$Stages = @(
    @{ Code = 'dang-ky'; Text = '0. Không đẩy (tạo kỳ mới thì dừng ở chờ đăng ký)' }
    @{ Code = 'gui-danh-muc'; Text = '1. Nộp danh mục → chờ gửi danh mục' }
    @{ Code = 'ra-soat'; Text = '2. Gửi danh mục → chờ TCCB-LĐ rà soát' }
    @{ Code = 'duyet'; Text = '3. TCCB-LĐ trình → chờ duyệt danh mục' }
    @{ Code = 'tu-cham'; Text = '4. Duyệt danh mục → chờ tự chấm' }
    @{ Code = 'chi-bo'; Text = '5. Tự chấm → chờ chi bộ xác nhận' }
    @{ Code = 'tap-the'; Text = '6. Chi bộ xác nhận → chờ họp tập thể' }
    @{ Code = 'tham-dinh'; Text = '7. Ghi kết quả họp → chờ thẩm định' }
    @{ Code = 'nhan-xet'; Text = '8. Thẩm định → chờ nhận xét' }
    @{ Code = 'quyet-dinh'; Text = '9. Nhận xét → chờ quyết định' }
    @{ Code = 'cong-bo'; Text = '10. Mẫu 20, quyết định → chờ công bố' }
    @{ Code = 'xong'; Text = '11. Công bố hết' }
)

$saved = @{}
try {
    if (Test-Path $StateFile) {
        $obj = Get-Content $StateFile -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($prop in $obj.PSObject.Properties) { $saved[$prop.Name] = $prop.Value }
    }
} catch { }
function Get-Saved([string]$name, $default) { if ($saved.ContainsKey($name) -and $null -ne $saved[$name]) { return $saved[$name] } return $default }
# Tiến độ (daXong) chỉ đúng với kỳ đã chạy (kyId, kyNam, kyQuy); kỳ khác hoặc tiến độ cũ chưa gắn kỳ thì bắt đầu lại
if (-not $saved['kyId']) { $saved['daXong'] = -1 }

$font = New-Object System.Drawing.Font('Segoe UI', 10)
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Đẩy hồ sơ đánh giá theo giai đoạn'
$form.Size = New-Object System.Drawing.Size(860, 680)
$form.StartPosition = 'CenterScreen'
$form.Font = $font
$form.MinimumSize = New-Object System.Drawing.Size(700, 500)

function New-Label([string]$text, [int]$y) {
    $l = New-Object System.Windows.Forms.Label
    $l.Text = $text; $l.Location = New-Object System.Drawing.Point(16, ($y + 3)); $l.AutoSize = $true
    $form.Controls.Add($l)
}
function New-TextBox([string]$value, [int]$y, [int]$width) {
    $t = New-Object System.Windows.Forms.TextBox
    $t.Text = $value; $t.Location = New-Object System.Drawing.Point(200, $y); $t.Width = $width
    $form.Controls.Add($t)
    return $t
}

New-Label 'Địa chỉ máy chủ' 16
$txtApi = New-TextBox (Get-Saved 'api' 'http://192.168.22.149:3001') 16 300

New-Label 'Kỳ (năm / quý)' 52
$numNam = New-Object System.Windows.Forms.NumericUpDown
$numNam.Minimum = 2020; $numNam.Maximum = 2100; $numNam.Value = [int](Get-Saved 'nam' 2027)
$numNam.Location = New-Object System.Drawing.Point(200, 52); $numNam.Width = 80
$form.Controls.Add($numNam)
$numQuy = New-Object System.Windows.Forms.NumericUpDown
$numQuy.Minimum = 1; $numQuy.Maximum = 4; $numQuy.Value = [int](Get-Saved 'quy' 1)
$numQuy.Location = New-Object System.Drawing.Point(290, 52); $numQuy.Width = 50
$form.Controls.Add($numQuy)

New-Label 'Đẩy tới giai đoạn' 88
$cboStage = New-Object System.Windows.Forms.ComboBox
$cboStage.DropDownStyle = 'DropDownList'; $cboStage.Location = New-Object System.Drawing.Point(200, 88); $cboStage.Width = 420
foreach ($s in $Stages) { [void]$cboStage.Items.Add($s.Text) }
# Mặc định giai đoạn kế tiếp lần chạy thành công trước, nếu cùng năm/quý với kỳ đã chạy
function Select-NextStage {
    $last = -1
    if ([int](Get-Saved 'kyNam' 0) -eq [int]$numNam.Value -and [int](Get-Saved 'kyQuy' 0) -eq [int]$numQuy.Value) { $last = [int](Get-Saved 'daXong' -1) }
    $cboStage.SelectedIndex = [Math]::Min($last + 1, $Stages.Count - 1)
}
Select-NextStage
$numNam.Add_ValueChanged({ Select-NextStage })
$numQuy.Add_ValueChanged({ Select-NextStage })
$form.Controls.Add($cboStage)

$chkScenario = New-Object System.Windows.Forms.CheckBox
$chkScenario.Text = "Thêm tình huống: trả lại, thu hồi, Chưa xếp loại, ý kiến rà soát/PGĐ, Mẫu 04, 05, diện cấp trên,`r`n" +
    "Xuất sắc, khống chế, chênh lệch và giải trình, Mẫu 03, 19, 06-08, 16, 17, kiến nghị, mở lại"
$chkScenario.Location = New-Object System.Drawing.Point(200, 118); $chkScenario.AutoSize = $true
$chkScenario.Checked = [bool](Get-Saved 'tinhHuong' $true)
$form.Controls.Add($chkScenario)

# Không lưu lựa chọn này: chỉ tự tích khi chưa có kỳ đang chạy
$chkCreate = New-Object System.Windows.Forms.CheckBox
$chkCreate.Text = 'Tạo kỳ mới nếu chưa có (đã có kỳ của năm/quý này thì dùng kỳ đó)'
$chkCreate.Location = New-Object System.Drawing.Point(200, 158); $chkCreate.AutoSize = $true
$chkCreate.Add_CheckedChanged({ if (-not $chkCreate.Checked) { Select-NextStage } })
$form.Controls.Add($chkCreate)
if (-not $saved['kyId']) { $chkCreate.Checked = $true }

New-Label 'Mức Hoàn thành (C) cho' 182
$txtGradeC = New-TextBox (Get-Saved 'mucC' 'trung.nb') 182 300

New-Label 'Giữ lại để tự làm' 218
$txtKeep = New-TextBox (Get-Saved 'giuLai' '') 218 420

New-Label 'Mật khẩu chung' 254
$txtPass = New-TextBox '' 254 200
$txtPass.UseSystemPasswordChar = $true

$btnRun = New-Object System.Windows.Forms.Button
$btnRun.Text = 'Chạy'; $btnRun.Location = New-Object System.Drawing.Point(200, 290); $btnRun.Size = New-Object System.Drawing.Size(110, 32)
$form.Controls.Add($btnRun)
$btnStop = New-Object System.Windows.Forms.Button
$btnStop.Text = 'Dừng'; $btnStop.Location = New-Object System.Drawing.Point(320, 290); $btnStop.Size = New-Object System.Drawing.Size(110, 32); $btnStop.Enabled = $false
$form.Controls.Add($btnStop)
$lblStatus = New-Object System.Windows.Forms.Label
$lblStatus.Location = New-Object System.Drawing.Point(445, 297); $lblStatus.AutoSize = $true
$form.Controls.Add($lblStatus)

$txtLog = New-Object System.Windows.Forms.TextBox
$txtLog.Multiline = $true; $txtLog.ScrollBars = 'Both'; $txtLog.ReadOnly = $true; $txtLog.WordWrap = $false
$txtLog.Font = New-Object System.Drawing.Font('Consolas', 9)
$txtLog.Location = New-Object System.Drawing.Point(16, 334)
$txtLog.Size = New-Object System.Drawing.Size(812, 292)
$txtLog.Anchor = 'Top, Bottom, Left, Right'
$form.Controls.Add($txtLog)

$script:Proc = $null
$script:StageIndex = -1
$script:RunPeriodId = $null
$script:RunNoPeriod = $false
$script:RunNam = 0; $script:RunQuy = 0
$queue = New-Object System.Collections.Concurrent.ConcurrentQueue[string]

function Split-Names([string]$text) { return @($text -split '[,;\s]+' | Where-Object { $_ }) }
function Quote([string]$s) { return "'" + $s.Replace("'", "''") + "'" }

function Save-Form {
    $data = [ordered]@{
        api = $txtApi.Text.Trim(); nam = [int]$numNam.Value; quy = [int]$numQuy.Value; tinhHuong = $chkScenario.Checked
        mucC = $txtGradeC.Text; giuLai = $txtKeep.Text; daXong = [int](Get-Saved 'daXong' -1)
        kyId = $saved['kyId']; kyNam = [int](Get-Saved 'kyNam' 0); kyQuy = [int](Get-Saved 'kyQuy' 0)
    }
    try { $data | ConvertTo-Json | Set-Content -Path $StateFile -Encoding UTF8 } catch { }
}

$btnRun.Add_Click({
    if (-not $txtPass.Text) { [System.Windows.Forms.MessageBox]::Show('Nhập mật khẩu chung của các tài khoản.', 'Thiếu mật khẩu') | Out-Null; return }
    $stage = $Stages[$cboStage.SelectedIndex]
    $script:StageIndex = $cboStage.SelectedIndex
    Save-Form

    $script:RunPeriodId = $null; $script:RunNoPeriod = $false
    $script:RunNam = [int]$numNam.Value; $script:RunQuy = [int]$numQuy.Value
    $mode = if ($chkCreate.Checked) { '-TaoKy' } else { '-KyCoSan' }
    $parts = @("& $(Quote $ScriptPath)", $mode, "-ApiUrl $(Quote $txtApi.Text.Trim())", "-Nam $([int]$numNam.Value)", "-Quy $([int]$numQuy.Value)", "-GiaiDoan $($stage.Code)")
    if ($chkScenario.Checked) { $parts += '-TinhHuong' }
    $gradeC = Split-Names $txtGradeC.Text
    if ($gradeC.Count -gt 0) { $parts += '-MucQuyetDinh @{' + (($gradeC | ForEach-Object { "$(Quote $_)='HoanThanh'" }) -join ';') + '}' }
    $keep = Split-Names $txtKeep.Text
    if ($keep.Count -gt 0) { $parts += '-GiuLai ' + (($keep | ForEach-Object { Quote $_ }) -join ',') }
    $command = '[Console]::OutputEncoding = [Text.Encoding]::UTF8; $ProgressPreference = ''SilentlyContinue''; ' + ($parts -join ' ') + '; exit $LASTEXITCODE'

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'powershell.exe'
    $psi.Arguments = '-NoProfile -ExecutionPolicy Bypass -EncodedCommand ' + [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true; $psi.RedirectStandardInput = $true
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8; $psi.StandardErrorEncoding = [Text.Encoding]::UTF8
    $psi.EnvironmentVariables['CTD_MATKHAU'] = $txtPass.Text

    $txtLog.Clear()
    $txtLog.AppendText("Bắt đầu: $($stage.Text)  ($(Get-Date -Format 'HH:mm:ss'))`r`n")
    $p = New-Object System.Diagnostics.Process
    $p.StartInfo = $psi
    $handler = { param($sender, $e) if ($null -ne $e.Data) { $queue.Enqueue($e.Data) } }.GetNewClosure()
    $p.add_OutputDataReceived($handler)
    $p.add_ErrorDataReceived($handler)
    [void]$p.Start()
    $p.StandardInput.Close()
    $p.BeginOutputReadLine(); $p.BeginErrorReadLine()
    $script:Proc = $p
    $btnRun.Enabled = $false; $btnStop.Enabled = $true
    $lblStatus.Text = 'Đang chạy...'; $lblStatus.ForeColor = [System.Drawing.Color]::DarkBlue
    $timer.Start()
})

$btnStop.Add_Click({
    if ($script:Proc -and -not $script:Proc.HasExited) {
        try { Start-Process -FilePath 'taskkill.exe' -ArgumentList "/PID $($script:Proc.Id) /T /F" -WindowStyle Hidden -Wait } catch { }
        $queue.Enqueue('Đã dừng theo yêu cầu. Chạy lại cùng giai đoạn để làm tiếp từ chỗ dừng.')
    }
})

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 300
$timer.Add_Tick({
    $line = $null
    $sb = New-Object System.Text.StringBuilder
    while ($queue.TryDequeue([ref]$line)) {
        [void]$sb.Append($line).Append("`r`n")
        if ($line -match 'Mã kỳ: ([0-9a-fA-F-]{36})') { $script:RunPeriodId = $Matches[1] }
        elseif ($line -match '^Không tìm thấy kỳ') { $script:RunNoPeriod = $true }
    }
    if ($sb.Length -gt 0) { $txtLog.AppendText($sb.ToString()) }
    if ($script:Proc -and $script:Proc.HasExited -and $queue.IsEmpty) {
        $script:Proc.WaitForExit()
        $code = $script:Proc.ExitCode
        $script:Proc = $null
        $timer.Stop()
        $btnRun.Enabled = $true; $btnStop.Enabled = $false
        # Kỳ khác lần trước (kỳ cũ bị xóa, vừa tạo kỳ mới) hoặc không còn kỳ: bỏ tiến độ cũ
        if ($script:RunPeriodId -and $script:RunPeriodId -ne $saved['kyId']) {
            $saved['kyId'] = $script:RunPeriodId; $saved['daXong'] = -1
            $saved['kyNam'] = $script:RunNam; $saved['kyQuy'] = $script:RunQuy
        } elseif ($script:RunNoPeriod) { $saved['kyId'] = $null; $saved['daXong'] = -1; $chkCreate.Checked = $true }
        if ($code -eq 0) {
            $lblStatus.Text = "Xong lúc $(Get-Date -Format 'HH:mm')"; $lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen
            if ($script:RunPeriodId) { $saved['daXong'] = $script:StageIndex }
            $chkCreate.Checked = $false
            Save-Form
            $cboStage.SelectedIndex = [Math]::Min($script:StageIndex + 1, $Stages.Count - 1)
        } else {
            Save-Form
            $lblStatus.Text = 'Dừng do lỗi, xem dòng cuối'; $lblStatus.ForeColor = [System.Drawing.Color]::DarkRed
        }
    }
})

$form.Add_FormClosing({
    param($sender, $e)
    if ($script:Proc -and -not $script:Proc.HasExited) {
        $answer = [System.Windows.Forms.MessageBox]::Show('Script đang chạy. Dừng và đóng cửa sổ?', 'Đang chạy', 'YesNo')
        if ($answer -ne 'Yes') { $e.Cancel = $true; return }
        try { Start-Process -FilePath 'taskkill.exe' -ArgumentList "/PID $($script:Proc.Id) /T /F" -WindowStyle Hidden -Wait } catch { }
    }
})

[void]$form.ShowDialog()
