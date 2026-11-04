[CmdletBinding()]
param([Parameter(Mandatory)][string]$Folder,[string]$Manifest,[switch]$LapDanhSach)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Folder).TrimEnd('\')+'\'
if(!(Test-Path -LiteralPath $root -PathType Container)){throw 'Không thấy thư mục cần kiểm chứng.'}
if(!$Manifest){$Manifest=Join-Path $root 'manifest.sha256.json'}
if($LapDanhSach){
    if(Test-Path -LiteralPath $Manifest){throw 'Manifest đã tồn tại; không thay mốc đối chiếu.'}
    $entries=@(Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object {
        [pscustomobject]@{Path=[IO.Path]::GetRelativePath($root,$_.FullName);Length=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
    [pscustomobject]@{CreatedUtc=[DateTime]::UtcNow.ToString('o');Files=$entries} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $Manifest -Encoding utf8
}
$data=Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
foreach($entry in $data.Files){
    $target=[IO.Path]::GetFullPath((Join-Path $root $entry.Path))
    if(!$target.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'Manifest tham chiếu ngoài thư mục.'}
    if(!(Test-Path -LiteralPath $target -PathType Leaf)){throw "Thiếu file: $($entry.Path)"}
    if((Get-Item -LiteralPath $target).Length -ne $entry.Length -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $entry.Sha256){throw "File không khớp: $($entry.Path)"}
}
Write-Output "Đạt SHA256: $($data.Files.Count) file. Chỉ xác minh file; không thay cho RESTORE/CHECKDB."
