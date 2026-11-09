param([Parameter(Mandatory)][switch]$CreateIsolatedDatabase)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$db = 'PhotocopyChain_E2E_20261109'
$sql = @('-S', '.\MSSQLSERVER01', '-E', '-C', '-I', '-b', '-l', '5', '-f', '65001')

# Không chạy 00_TaoCSDL.sql vì script đó xóa và tạo lại database gốc.
& sqlcmd @sql -d master -Q "IF DB_ID(N'$db') IS NOT NULL THROW 50301, N'Database thử đã tồn tại; giữ nguyên để chạy lại.', 1;"
if ($LASTEXITCODE) { throw 'Không thể tạo database thử mới.' }
& sqlcmd @sql -d master -Q "CREATE DATABASE [$db] COLLATE Vietnamese_CI_AS;"
if ($LASTEXITCODE) { throw 'Không tạo được database thử.' }

$dbDir = Join-Path $root 'Database'
$base = @('01_TaoBang.sql', '02_DuLieuMau.sql', '03_View_Function.sql', '04_StoredProcedure.sql', '05_Trigger.sql')
$migrations = Get-ChildItem -LiteralPath $dbDir -File |
    Where-Object { $_.Name -match '^0(?:0[3-9]|1[0-9]|2[0-8])-.+\.sql$' -and $_.Name -notmatch '^008-' } |
    Sort-Object Name | Select-Object -ExpandProperty Name
$run = Join-Path $env:TEMP ('PhotocopyE2E_' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $run
$sqlFile = Join-Path $run 'prepare.sql'
try {
    foreach ($file in ($base + $migrations)) {
        $source = [IO.File]::ReadAllText((Join-Path $dbDir $file))
        $source = [regex]::Replace($source, '(?im)^\s*USE\s+(?:\[PhotocopyChain\]|PhotocopyChain)\s*;', "USE [$db];")
        if ([regex]::Matches($source, '(?im)^\s*USE\s+(?!\[PhotocopyChain_E2E_20261109\]\s*;)').Count) {
            throw "Phát hiện lệnh USE ngoài database thử trong $file"
        }
        [IO.File]::WriteAllText($sqlFile, $source, [Text.UTF8Encoding]::new($false))
        & sqlcmd @sql -d $db -i $sqlFile
        if ($LASTEXITCODE) { throw "Không dựng được database thử tại $file" }
    }
} finally {
    if (Test-Path -LiteralPath $sqlFile) { Remove-Item -LiteralPath $sqlFile }
}

# Cấu hình cục bộ, khóa sinh riêng cho lần thử. Mọi kho dữ liệu và connector tách khỏi bản đang chạy.
$cs = "Server=.\MSSQLSERVER01;Database=$db;Integrated Security=True;TrustServerCertificate=True"
$payOsKey = [guid]::NewGuid().ToString('N')
$envFile = Join-Path $run '.env'
$envLines = @(
    "ConnectionStrings__PhotocopyChain=$cs",
    "Jwt__Key=$([guid]::NewGuid().ToString('N'))$([guid]::NewGuid().ToString('N'))",
    'Jwt__Issuer=PhotocopyE2E', 'Jwt__Audience=PhotocopyE2E',
    'PayOS__ClientId=e2e-mock', 'PayOS__ApiKey=e2e-mock', "PayOS__ChecksumKey=$payOsKey",
    "PayOS__ThuMucQr=$run\qr",
    "LuuTruFile__ThuMucGoc=$run\files",
    "BaoTri__DuongDanTrangThai=$run\maintenance.json",
    "PhienDangNhap__DuongDanLuu=$run\account-sessions.json",
    "HuyDon__ThuMucNhatKy=$run\nhat-ky-huy",
    "HuyDon__ThuMucBangChung=$run\bang-chung-huy",
    "GiaoHang__ThuMucBangChung=$run\bang-chung-giao",
    "PrinterConnector__ThuMucLenh=$run\lenh-in",
    "PrinterConnector__ThuMucLienKet=$run\lien-ket",
    "PrinterConnector__ThuMucDieuKhien=$run\dieu-khien",
    "PrinterConnector__ThuMucChuyenMay=$run\chuyen-may",
    "PrinterConnector__ThuMucYeuCauCod=$run\cod",
    "PHOTOCOPY_E2E_DB=$cs", "PHOTOCOPY_E2E_PAYOS_KEY=$payOsKey", "PHOTOCOPY_E2E_FILES=$run"
)
[IO.File]::WriteAllLines($envFile, $envLines, [Text.UTF8Encoding]::new($false))
$null = New-Item -ItemType Directory -Path (Join-Path $run 'lenh-in')
Write-Host "Cấu hình thử đã lưu tại $envFile"
