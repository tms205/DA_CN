$ErrorActionPreference = 'Stop'
$taskScripts = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\scripts'))
$taskLauncher = Join-Path $taskScripts 'chay_web.cmd'
$taskText = [IO.File]::ReadAllText($taskLauncher)
$taskParts = [regex]::Split($taskText, '(?m)^# POWERSHELL-BEGIN\r?$')
if ($taskParts.Count -ne 2) { throw 'Launcher phai tu chua PowerShell, khong phu thuoc cac script cu.' }
$taskTokens = $null
$taskErrors = $null
$taskAst = [Management.Automation.Language.Parser]::ParseInput($taskParts[1], [ref]$taskTokens, [ref]$taskErrors)
if ($taskErrors.Count) { throw 'PowerShell trong launcher khong hop le.' }
foreach ($taskFunction in $taskAst.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.FunctionDefinitionAst] }) {
    . ([scriptblock]::Create($taskFunction.Extent.Text))
}
if (-not (Get-Command Get-PhotocopyAppDrive -ErrorAction SilentlyContinue)) { throw 'Launcher app phai chon o ASCII tam, khong dung o dang duoc su dung.' }
if ((Get-PhotocopyAppDrive -OccupiedLetters @('C', 'P', 'Q')) -ne 'R:') { throw 'Chon nham o da duoc su dung.' }
$taskFullDrives = $false
try { Get-PhotocopyAppDrive -OccupiedLetters @('P','Q','R','S','T','U','V','W','X','Y','Z') | Out-Null } catch { $taskFullDrives = $true }
if (-not $taskFullDrives) { throw 'Phai dung neu khong con o ASCII trong.' }

# Kiem tra danh tinh tien trinh ma khong dung hay chay dich vu that.
$taskRoot = Join-Path ([IO.Path]::GetTempPath()) 'photocopy-process-fixture'
$taskAdmin = Join-Path $taskRoot 'PhotocopyAdmin\bin\Debug\net8.0\PhotocopyAdmin.dll'
$taskProcesses = @(
    [pscustomobject]@{ ProcessId=101; Name='PhotocopyApi.exe'; ExecutablePath=(Join-Path $taskRoot 'PhotocopyApi\bin\Debug\net8.0\PhotocopyApi.exe'); CommandLine='' }
    [pscustomobject]@{ ProcessId=102; Name='PhotocopyPrinterSimulator.exe'; ExecutablePath=(Join-Path $taskRoot 'PhotocopyPrinterSimulator\bin\Debug\net8.0\PhotocopyPrinterSimulator.exe'); CommandLine='' }
    [pscustomobject]@{ ProcessId=103; Name='dotnet.exe'; ExecutablePath=''; CommandLine=('dotnet Microsoft.AspNetCore.Components.WebAssembly.DevServer.dll --applicationpath "' + $taskAdmin + '"') }
    [pscustomobject]@{ ProcessId=201; Name='PhotocopyApi.exe'; ExecutablePath=(Join-Path ($taskRoot + '-other') 'PhotocopyApi\bin\Debug\net8.0\PhotocopyApi.exe'); CommandLine='' }
    [pscustomobject]@{ ProcessId=202; Name='dotnet.exe'; ExecutablePath=''; CommandLine=('dotnet other.dll --applicationpath "' + $taskAdmin + '"') }
)
$taskFound = @(Get-PhotocopyWebProcesses -Solution $taskRoot -Processus $taskProcesses)
if (($taskFound.ProcessId -join ',') -ne '101,102,103') { throw 'Launcher chon nham tien trinh ngoai du an.' }

# Du lieu gia: kiem tra dau nhay, comment, khoa trung; khong doc .env that.
$taskFixture = Join-Path ([IO.Path]::GetTempPath()) ('photocopy-launcher-' + [guid]::NewGuid().ToString('N') + '.env')
try {
    [IO.File]::WriteAllText($taskFixture, "ConnectionStrings__PhotocopyChain='Server=.;Password=a#b;' # comment")
    $taskValue = Get-PhotocopyEnvValue -DuongDan $taskFixture -Khoa 'ConnectionStrings:PhotocopyChain'
    if ($taskValue -ne 'Server=.;Password=a#b;') { throw 'Doc sai gia tri .env co dau nhay.' }
    [IO.File]::WriteAllText($taskFixture, "KEY=first`nKEY=second")
    $taskRejected = $false
    try { Get-PhotocopyEnvValue -DuongDan $taskFixture -Khoa 'KEY' | Out-Null } catch { $taskRejected = $true }
    if (-not $taskRejected) { throw 'Phai tu choi khoa .env trung.' }
}
finally { Remove-Item -LiteralPath $taskFixture -Force }

foreach ($taskProject in @('PhotocopyApi', 'PhotocopyAdmin', 'PhotocopyPrinterSimulator')) {
    if ($taskParts[1] -notmatch [regex]::Escape($taskProject + '\' + $taskProject + '.csproj')) { throw ('Thieu build/chay ' + $taskProject) }
}
if ($taskText -match '(?i)(kiem_tra_database|_dich_vu_web|_cau_hinh_env|_chay_app)\.ps1') { throw 'Con phu thuoc script da bo.' }
if ($taskParts[1].IndexOf("Wait-PhotocopyHttp 'http://localhost:5268/swagger/v1/swagger.json'") -gt $taskParts[1].IndexOf("Start-Process 'http://localhost:5268/swagger'")) { throw 'Mo Swagger truoc khi API san sang.' }
if ($taskParts[1] -notmatch "Start-Process 'http://localhost:5064'") { throw 'Chua mo Web Admin.' }
if ($taskParts[1] -notmatch "Start-Process 'http://localhost:5268/swagger'") { throw 'Chua mo Swagger.' }
$taskFiles = @(Get-ChildItem -LiteralPath $taskScripts -Force)
if ($taskFiles.Count -ne 1 -or $taskFiles[0].Name -ne 'chay_web.cmd') { throw 'Thu muc scripts phai chi con chay_web.cmd.' }
Write-Output 'Passed: mot launcher doc lap; dung tien trinh; doc .env; build 3 dich vu; mo Swagger va Web.'
