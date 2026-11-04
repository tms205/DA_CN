param(
    [string]$EvidenceRoot = (Join-Path $env:LOCALAPPDATA 'PhotocopyChain\DeliveryEvidence'),
    [string]$Server = '.\MSSQLSERVER01',
    [string]$Database = 'PhotocopyChain',
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
# Chỉ SELECT database. Phân loại theo metadata, không đoán người gửi qua ảnh/tên file.
$evidence = [IO.Path]::GetFullPath($EvidenceRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
if (-not (Test-Path -LiteralPath $evidence -PathType Container)) { throw 'Không tìm thấy thư mục bằng chứng.' }
$prefix = $evidence + [IO.Path]::DirectorySeparatorChar
$sqlcmd = (Get-Command sqlcmd -ErrorAction Stop).Source
$query = @'
SET NOCOUNT ON;
SELECT gh.MaGiaoHang, 'Shipper' AS Nguon, a.MaAnh, a.LoaiNoiDung, a.Sha256
FROM dbo.GiaoHang gh CROSS APPLY OPENJSON(COALESCE(gh.AnhBangChungJson,N'[]'))
WITH(MaAnh VARCHAR(32) '$.MaAnh',LoaiNoiDung VARCHAR(30) '$.LoaiNoiDung',Sha256 VARCHAR(64) '$.Sha256') a
UNION ALL
SELECT gh.MaGiaoHang, 'KhachHang', a.MaAnh, a.LoaiNoiDung, a.Sha256
FROM dbo.GiaoHang gh CROSS APPLY OPENJSON(COALESCE(gh.DoiChieuKhachHangJson,N'{}'),'$.Anh')
WITH(MaAnh VARCHAR(32) '$.MaAnh',LoaiNoiDung VARCHAR(30) '$.LoaiNoiDung',Sha256 VARCHAR(64) '$.Sha256') a
FOR JSON PATH;
'@
$output = & $sqlcmd -S $Server -d $Database -E -C -b -l 10 -w 65535 -y 0 -Q $query
if ($LASTEXITCODE -ne 0) { throw 'Chưa đọc được metadata bằng chứng; không chuyển file.' }
$rows = @((($output -join '').Trim()) | ConvertFrom-Json)
$plan = [Collections.Generic.List[object]]::new()
$problems = [Collections.Generic.List[object]]::new()
$already = 0
$ambiguous = @{}
foreach ($group in ($rows | Group-Object { "$($_.MaGiaoHang):$($_.MaAnh)" })) {
    if (@($group.Group.Nguon | Select-Object -Unique).Count -gt 1) { $ambiguous[$group.Name] = $true }
}
foreach ($row in $rows) {
    $id = 0; $guid = [guid]::Empty
    if (-not [int]::TryParse([string]$row.MaGiaoHang,[ref]$id) -or $id -lt 1 -or
        -not [guid]::TryParseExact([string]$row.MaAnh,'N',[ref]$guid) -or
        $row.Nguon -notin @('Shipper','KhachHang') -or $row.LoaiNoiDung -notin @('image/png','image/jpeg') -or
        $row.Sha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'Metadata không hợp lệ; không chuyển file.' }
    if ($ambiguous.ContainsKey("${id}:$($row.MaAnh)")) {
        $problems.Add([pscustomobject]@{Luot=$id;MaAnh=$row.MaAnh;Loi='Ảnh được tham chiếu bởi cả hai nguồn'});continue
    }
    $ext = if ($row.LoaiNoiDung -eq 'image/png') { '.png' } else { '.jpg' }
    $relative = Join-Path ([string]$id) ($row.MaAnh+$ext)
    $old = [IO.Path]::GetFullPath((Join-Path $evidence $relative))
    $new = [IO.Path]::GetFullPath((Join-Path (Join-Path (Join-Path $evidence ([string]$id)) $row.Nguon) ($row.MaAnh+$ext)))
    foreach ($path in @($old,$new)) { if (-not $path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Đường dẫn nằm ngoài thư mục bằng chứng.' } }
    if (Test-Path -LiteralPath $new) {
        if ((Get-FileHash -LiteralPath $new -Algorithm SHA256).Hash -eq $row.Sha256) { $already++ }
        else { $problems.Add([pscustomobject]@{Luot=$id;MaAnh=$row.MaAnh;Loi='Hash ảnh tại vị trí mới không khớp'}) }
        continue
    }
    if (-not (Test-Path -LiteralPath $old -PathType Leaf)) {
        $problems.Add([pscustomobject]@{Luot=$id;MaAnh=$row.MaAnh;Loi='Không tìm thấy ảnh cũ'});continue
    }
    if ((Get-FileHash -LiteralPath $old -Algorithm SHA256).Hash -ne $row.Sha256) {
        $problems.Add([pscustomobject]@{Luot=$id;MaAnh=$row.MaAnh;Loi='Hash ảnh cũ không khớp'});continue
    }
    $plan.Add([pscustomobject]@{Luot=$id;Nguon=$row.Nguon;MaAnh=$row.MaAnh;Sha256=$row.Sha256;Old=$old;New=$new;Relative=$relative})
}
$backup = $null; $moved = 0
if ($Apply -and $plan.Count -gt 0) {
    $backup = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA ('PhotocopyChain\Backup\DeliveryEvidence_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'_'+[guid]::NewGuid().ToString('N'))))
    if ($backup.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Backup không được nằm trong thư mục nguồn.' }
    New-Item -ItemType Directory -Path $backup | Out-Null
    Copy-Item -LiteralPath $evidence -Destination (Join-Path $backup 'DeliveryEvidence') -Recurse
    $plan | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $backup 'ke-hoach.json') -Encoding UTF8
    foreach ($item in $plan) {
        $copy = Join-Path (Join-Path $backup 'DeliveryEvidence') $item.Relative
        if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $item.Sha256 -or
            (Get-FileHash -LiteralPath $item.Old -Algorithm SHA256).Hash -ne $item.Sha256) { throw 'Ảnh/backup đã thay đổi; dừng chuyển file.' }
        if (Test-Path -LiteralPath $item.New) { throw 'Đích đã có file; không ghi đè.' }
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($item.New)) -Force | Out-Null
        Move-Item -LiteralPath $item.Old -Destination $item.New
        if ((Get-FileHash -LiteralPath $item.New -Algorithm SHA256).Hash -ne $item.Sha256) { throw 'Hash sau chuyển không khớp; kiểm tra backup.' }
        $moved++
    }
}
[pscustomobject]@{Apply=[bool]$Apply;Root=$evidence;References=$rows.Count;CanMove=$plan.Count;Moved=$moved;AlreadySorted=$already;Problems=$problems.Count;Backup=$backup;Details=@($problems.ToArray())} | ConvertTo-Json -Depth 4
