<#
  Chạy bộ kiểm thử giao diện trong môi trường riêng: CSDL ctd_e2e (bản sao của congtacdang_db),
  backend cổng 5400, giao diện cổng 3400. Không đụng container/CSDL đang dùng thật.
  Ví dụ:
    pwsh frontend/e2e/chay-e2e.ps1                      # chạy tất cả
    pwsh frontend/e2e/chay-e2e.ps1 -Grep "Mẫu 20"       # chỉ kịch bản khớp tên
    pwsh frontend/e2e/chay-e2e.ps1 -Headed              # xem trình duyệt chạy
    pwsh frontend/e2e/chay-e2e.ps1 -Up                  # chỉ dựng môi trường rồi để chạy (soi tay hoặc chạy lại nhanh)
    pwsh frontend/e2e/chay-e2e.ps1 -Down                # dừng môi trường dựng bằng -Up và xóa ctd_e2e
#>
param(
    [string]$Grep = '',
    [switch]$Headed,
    [switch]$Up,
    [switch]$Down,
    # CSDL nguồn chỉ được đọc (pg_dump); đổi khi muốn thử trên dữ liệu khác
    [string]$SourceDb = 'congtacdang_db',
    [string]$Container = 'attech-dangbo-postgres',
    [string]$EnvFile = ''
)
$ErrorActionPreference = 'Stop'
$Db = 'ctd_e2e'
$BackendPort = 5400
$WebPort = 3400
$Password = 'Thu12345a'   # mật khẩu thử, chỉ dùng cho ctd_e2e

$frontendDir = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $frontendDir
$resultDir = Join-Path $frontendDir 'e2e-ket-qua'
New-Item -ItemType Directory -Force $resultDir | Out-Null

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

# Tìm docker/.env: trong worktree, nếu không có thì ở bản checkout chính (worktree dùng chung .git)
if (-not $EnvFile) {
    $EnvFile = Join-Path $repoRoot 'docker/.env'
    if (-not (Test-Path $EnvFile)) {
        $common = (& git -C $repoRoot rev-parse --path-format=absolute --git-common-dir).Trim()
        $EnvFile = Join-Path (Split-Path -Parent $common) 'docker/.env'
    }
}
if (-not (Test-Path $EnvFile)) { throw "Không thấy docker/.env ($EnvFile); truyền -EnvFile." }
$envMap = @{}
Get-Content $EnvFile | ForEach-Object {
    if ($_ -match '^\s*([A-Za-z0-9_]+)\s*=\s*(.*)$') { $envMap[$Matches[1]] = $Matches[2].Trim().Trim('"') }
}
$pgUser = $envMap['POSTGRES_USER']; $pgPass = $envMap['POSTGRES_PASSWORD']
$pgPort = if ($envMap['POSTGRES_PORT']) { $envMap['POSTGRES_PORT'] } else { '5434' }
if (-not $pgUser -or -not $pgPass) { throw 'docker/.env thiếu POSTGRES_USER/POSTGRES_PASSWORD.' }

function Invoke-Psql([string]$database, [string]$sql) {
    $sql | docker exec -i -e "PGPASSWORD=$pgPass" $Container psql -U $pgUser -d $database -v ON_ERROR_STOP=1 -q -At
    if ($LASTEXITCODE -ne 0) { throw "psql lỗi trên $database" }
}

$script:procs = @()
function Stop-ProcessId($id) { cmd.exe /c "taskkill /T /F /PID $id >nul 2>&1" }
function Stop-Tree($p) { if ($p -and -not $p.HasExited) { Stop-ProcessId $p.Id } }
function Cleanup([bool]$dropDb) {
    Write-Step 'Dọn dẹp'
    foreach ($p in $script:procs) { Stop-Tree $p }
    if ($dropDb) {
        try { Invoke-Psql 'postgres' "DROP DATABASE IF EXISTS $Db WITH (FORCE);" } catch { Write-Warning "Xóa $Db không thành công: $_" }
    }
}

function Wait-Url([string]$url, [int]$timeoutSec, [string]$name) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        try { $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5; if ($r.StatusCode -lt 500) { return } } catch {
            if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -lt 500) { return }
        }
        Start-Sleep -Seconds 2
    }
    throw "$name không sẵn sàng sau $timeoutSec giây ($url)"
}

if ($Down) {
    foreach ($port in @($BackendPort, $WebPort)) {
        Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | ForEach-Object { Stop-ProcessId $_.OwningProcess }
    }
    Invoke-Psql 'postgres' "DROP DATABASE IF EXISTS $Db WITH (FORCE);"
    Write-Host 'Đã dừng môi trường thử và xóa ctd_e2e.'
    exit 0
}

foreach ($port in @($BackendPort, $WebPort)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
        throw "Cổng $port đang bị chiếm; dừng tiến trình đó trước."
    }
}

$exit = 1
$keep = $false
try {
    Write-Step "Tạo CSDL $Db từ $SourceDb (chỉ đọc nguồn)"
    Invoke-Psql 'postgres' "DROP DATABASE IF EXISTS $Db WITH (FORCE);"
    Invoke-Psql 'postgres' "CREATE DATABASE $Db;"
    docker exec -e "PGPASSWORD=$pgPass" $Container sh -c "pg_dump -U $pgUser -Fc $SourceDb | pg_restore -U $pgUser -d $Db --no-owner --no-privileges"
    if ($LASTEXITCODE -ne 0) { throw 'pg_dump | pg_restore không thành công' }

    Write-Step 'Đặt mật khẩu thử cho mọi tài khoản'
    $out = Invoke-Psql $Db @"
CREATE EXTENSION IF NOT EXISTS pgcrypto;
UPDATE party_member_profiles
   SET "PasswordHash" = crypt('$Password', gen_salt('bf', 10)),
       "MustChangePassword" = false, "FailedLoginCount" = 0, "LockoutEnd" = NULL;
"@
    Write-Host ($out -join ' ')

    Write-Step "Chạy backend cổng $BackendPort"
    $secretBytes = New-Object byte[] 48
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($secretBytes)
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__Default = "Host=localhost;Port=$pgPort;Database=$Db;Username=$pgUser;Password=$pgPass"
    $env:Jwt__Secret = [Convert]::ToBase64String($secretBytes)
    $env:Database__AutoMigrate = 'true'
    $env:Database__SeedSampleData = 'false'
    $env:Security__RateLimit__Auth__PermitLimit = '1000'
    $env:Cors__AllowedOrigins__0 = "http://localhost:$WebPort"
    $be = Start-Process -FilePath 'dotnet' -PassThru -NoNewWindow `
        -ArgumentList @('run', '--project', (Join-Path $repoRoot 'backend/src/CongTacDang.Api'), '--no-launch-profile', '--urls', "http://localhost:$BackendPort") `
        -RedirectStandardOutput (Join-Path $resultDir 'backend.log') -RedirectStandardError (Join-Path $resultDir 'backend.err.log')
    $script:procs += $be
    Wait-Url "http://localhost:$BackendPort/healthz" 240 'Backend'

    Write-Step "Chạy giao diện cổng $WebPort"
    $env:BACKEND_INTERNAL_URL = "http://localhost:$BackendPort"
    $env:NEXT_TELEMETRY_DISABLED = '1'
    $fe = Start-Process -FilePath 'cmd.exe' -PassThru -NoNewWindow -WorkingDirectory $frontendDir `
        -ArgumentList @('/c', "npx next dev -p $WebPort") `
        -RedirectStandardOutput (Join-Path $resultDir 'frontend.log') -RedirectStandardError (Join-Path $resultDir 'frontend.err.log')
    $script:procs += $fe
    Wait-Url "http://localhost:$WebPort/login" 240 'Giao diện'
    # Biên dịch sẵn các trang chính để lần mở đầu trong kịch bản không bị chậm
    foreach ($p in '/work-queue', '/periods', '/quality-control', '/evaluations') {
        try { Invoke-WebRequest -Uri "http://localhost:$WebPort$p" -UseBasicParsing -TimeoutSec 120 -MaximumRedirection 0 | Out-Null } catch { }
    }

    if ($Up) {
        Write-Host "Môi trường sẵn sàng: http://localhost:$WebPort (mật khẩu thử $Password). Dừng bằng -Down." -ForegroundColor Green
        $keep = $true; $exit = 0
        return
    }
    Write-Step 'Chạy Playwright'
    $env:E2E_BASE_URL = "http://localhost:$WebPort"
    $env:E2E_PASSWORD = $Password
    $args = @('playwright', 'test')
    if ($Grep) { $args += @('--grep', $Grep) }
    if ($Headed) { $args += '--headed' }
    Push-Location $frontendDir
    try { & npx @args; $exit = $LASTEXITCODE } finally { Pop-Location }
}
catch {
    Write-Host "LỖI: $_" -ForegroundColor Red
    $exit = 1
}
finally {
    if (-not $keep) { Cleanup $true }
}
Write-Host "Báo cáo: npx playwright show-report e2e-ket-qua/bao-cao  (trong thư mục frontend)"
exit $exit
