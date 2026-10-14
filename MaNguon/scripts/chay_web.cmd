@echo off
setlocal
rem Bam dup chay Web + API + may in ao va tu giu ket noi Android (USB/Wi-Fi ADB).
rem Go chay_web.cmd app de chay Flutter tren dien thoai da ket noi.
rem PowerShell duoc giu ngay trong file nay, khong can bat ky script phu nao.
set "PHOTOCOPY_LAUNCHER=%~f0"
set "PHOTOCOPY_MODE=%~1"
if not defined PHOTOCOPY_MODE set "PHOTOCOPY_MODE=web"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$script=[regex]::Split([IO.File]::ReadAllText($env:PHOTOCOPY_LAUNCHER),'(?m)^# POWERSHELL-BEGIN\r?$')[1]; & ([scriptblock]::Create($script)) -Mode $env:PHOTOCOPY_MODE -LauncherPath $env:PHOTOCOPY_LAUNCHER"
if errorlevel 1 (
    echo Khoi dong khong thanh cong. Xem thong bao o tren.
    pause
    exit /b 1
)
endlocal
exit /b 0

# POWERSHELL-BEGIN
param(
    [ValidateSet('web', 'app', 'ket-noi')][string]$Mode = 'web',
    [string]$LauncherPath,
    [int]$ApiProcessId,
    [long]$ApiStartTicks
)
$ErrorActionPreference = 'Stop'

function Get-PhotocopyEnvValue {
    param([string]$DuongDan, [string]$Khoa)
    $taskPattern = '^\s*' + [regex]::Escape($Khoa.Replace(':', '__')) + '\s*=(.*)$'
    $taskFound = @([IO.File]::ReadAllLines($DuongDan) | Where-Object { $_ -match $taskPattern })
    if ($taskFound.Count -gt 1) { throw 'File .env co khoa trung.' }
    if ($taskFound.Count -eq 0) { return $null }
    [void]($taskFound[0] -match $taskPattern)
    $taskValue = $Matches[1].Trim()
    if ($taskValue.Length -gt 0 -and ($taskValue[0] -eq [char]39 -or $taskValue[0] -eq [char]34)) {
        $taskQuote = $taskValue[0]
        $taskEnd = 1
        while ($taskEnd -lt $taskValue.Length -and
            ($taskValue[$taskEnd] -ne $taskQuote -or $taskValue[$taskEnd - 1] -eq [char]92)) { $taskEnd++ }
        if ($taskEnd -eq $taskValue.Length) { throw 'Gia tri .env thieu dau nhay dong.' }
        $taskTail = $taskValue.Substring($taskEnd + 1).Trim()
        if ($taskTail.Length -gt 0 -and -not $taskTail.StartsWith('#')) { throw 'Gia tri .env sai sau dau nhay.' }
        return $taskValue.Substring(1, $taskEnd - 1).Replace(('\' + $taskQuote), [string]$taskQuote)
    }
    return ([regex]::Replace($taskValue, '(^|\s)#.*$', '')).TrimEnd()
}

function Test-PhotocopyDatabase {
    param([string]$EnvFile)
    if (-not (Test-Path -LiteralPath $EnvFile)) { throw 'Thieu PhotocopyChain\.env. Khoi phuc file cau hinh rieng truoc khi chay.' }
    $taskConnection = Get-PhotocopyEnvValue -DuongDan $EnvFile -Khoa 'ConnectionStrings:PhotocopyChain'
    if ([string]::IsNullOrWhiteSpace($taskConnection)) { throw 'Thieu ConnectionStrings__PhotocopyChain trong .env.' }
    try {
        $taskBuilder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($taskConnection)
        if ($taskBuilder.DataSource -match '^(localhost|\.|127\.0\.0\.1)(\\(?<instance>[^,]+))?(,\d+)?$') {
            $taskInstance = $Matches['instance']
            $taskService = if ($taskInstance) { 'MSSQL$' + $taskInstance } else { 'MSSQLSERVER' }
            $taskSqlService = Get-Service -Name $taskService -ErrorAction SilentlyContinue
            if ($taskSqlService -and $taskSqlService.Status -ne 'Running') { Start-Service -Name $taskService }
        }
        $taskBuilder['Connect Timeout'] = 5
        $taskConn = New-Object System.Data.SqlClient.SqlConnection($taskBuilder.ConnectionString)
        try { $taskConn.Open() } finally { $taskConn.Dispose() }
        Write-Host 'Database da ket noi duoc.'
    }
    catch { throw 'Khong ket noi duoc database. Kiem tra .env, SQL Server; chay Administrator neu can mo dich vu SQL.' }
}

function Get-PhotocopyWebProcesses {
    param([string]$Solution, [object[]]$Processus)
    $taskRoot = [IO.Path]::GetFullPath($Solution).TrimEnd('\') + '\'
    foreach ($taskProcess in $Processus) {
        if ($taskProcess.Name -in @('PhotocopyApi.exe', 'PhotocopyPrinterSimulator.exe')) {
            $taskFolder = [IO.Path]::GetFileNameWithoutExtension($taskProcess.Name)
            $taskPrefix = $taskRoot + $taskFolder + '\'
            if ($taskProcess.ExecutablePath -and
                $taskProcess.ExecutablePath.StartsWith($taskPrefix, [StringComparison]::OrdinalIgnoreCase)) { $taskProcess }
        }
        elseif ($taskProcess.Name -eq 'dotnet.exe' -and
            $taskProcess.CommandLine -like '*Microsoft.AspNetCore.Components.WebAssembly.DevServer*') {
            $taskMatch = [regex]::Match($taskProcess.CommandLine, '(?i)--applicationpath\s+("[^"]+"|\S+)')
            $taskAppPath = $taskMatch.Groups[1].Value.Trim('"')
            if ($taskAppPath.StartsWith(($taskRoot + 'PhotocopyAdmin\'), [StringComparison]::OrdinalIgnoreCase)) { $taskProcess }
        }
    }
}

function Stop-PhotocopyWebServices {
    param([string]$Solution)
    $taskProcesses = @(Get-PhotocopyWebProcesses -Solution $Solution -Processus @(Get-CimInstance Win32_Process))
    $taskIds = @($taskProcesses | ForEach-Object { $_.ProcessId })
    $taskPorts = @(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalPort -in @(5268, 5064) })
    foreach ($taskPort in $taskPorts) {
        if ($taskPort.OwningProcess -notin $taskIds) { throw ('Cong ' + $taskPort.LocalPort + ' do ung dung khac giu. Khong dung ung dung do.') }
    }
    foreach ($taskProcess in $taskProcesses) {
        # Xac minh lai de khong dung nham PID da duoc ung dung khac tai su dung.
        $taskCurrent = @(Get-PhotocopyWebProcesses -Solution $Solution -Processus @(
            Get-CimInstance Win32_Process -Filter ('ProcessId=' + $taskProcess.ProcessId)
        ))
        if ($taskCurrent.Count -eq 1 -and $taskCurrent[0].CreationDate -eq $taskProcess.CreationDate) {
            Stop-Process -Id $taskProcess.ProcessId
        }
    }
    $taskDeadline = (Get-Date).AddSeconds(10)
    do {
        $taskRemaining = @(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
            Where-Object { $_.LocalPort -in @(5268, 5064) })
        if ($taskRemaining.Count -eq 0) { return }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $taskDeadline)
    throw 'Cong web chua duoc giai phong. Khong chay tiep de tranh dung API cu.'
}

function Wait-PhotocopyHttp {
    param([string]$Url)
    $taskDeadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $taskDeadline) {
        try {
            $taskResponse = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 2
            if ($taskResponse.StatusCode -eq 200) { return }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw ('Dich vu chua san sang: ' + $Url + '. Xem loi trong cua so API/Admin.')
}

function Start-PhotocopyService {
    param([string]$Solution, [string]$Project, [string]$Name, [switch]$Http)
    $taskCommand = '/k title PhotocopyChain ' + $Name + ' && dotnet run --no-build --project ' + $Project
    if ($Http) { $taskCommand += ' --launch-profile http' }
    # Console de nguoi dung doc log, Ctrl+C va dong tung dich vu khi ket thuc demo.
    Start-Process cmd.exe -WorkingDirectory $Solution -ArgumentList $taskCommand -WindowStyle Normal
}

function Get-PhotocopyAppDrive {
    param([string[]]$OccupiedLetters)
    foreach ($taskLetter in [char[]]'PQRSTUVWXYZ') {
        if ([string]$taskLetter -notin $OccupiedLetters) { return ([string]$taskLetter + ':') }
    }
    throw 'Khong con o dia trong P-Z de tao duong dan Android tam.'
}

function Invoke-PhotocopyAdb {
    param([string]$Adb, [string[]]$Arguments)
    try {
        $taskOutput = @(& $Adb @Arguments 2>$null)
        return [pscustomobject]@{ ExitCode=$LASTEXITCODE; Output=$taskOutput }
    }
    catch { return [pscustomobject]@{ ExitCode=1; Output=@() } }
}

function Get-PhotocopyAndroidDevices {
    param([string]$Adb)
    $taskResult = Invoke-PhotocopyAdb -Adb $Adb -Arguments @('devices', '-l')
    if ($taskResult.ExitCode -ne 0) { return }
    foreach ($taskLine in $taskResult.Output) {
        if ($taskLine -match '^(\S+)\s+device(?:\s|$)') { $Matches[1] }
    }
}

function Sync-PhotocopyAdbReverse {
    param([string]$Adb)
    foreach ($taskSerial in @(Get-PhotocopyAndroidDevices -Adb $Adb)) {
        $taskList = Invoke-PhotocopyAdb -Adb $Adb -Arguments @('-s', $taskSerial, 'reverse', '--list')
        if ($taskList.ExitCode -ne 0) { continue } # Thiết bị có thể vừa mất kết nối.
        if (($taskList.Output -join "`n") -notmatch '(?m)(^|\s)tcp:5268\s+tcp:5268(\s|$)') {
            $taskReverse = Invoke-PhotocopyAdb -Adb $Adb -Arguments @('-s', $taskSerial, 'reverse', 'tcp:5268', 'tcp:5268')
            if ($taskReverse.ExitCode -ne 0) { continue }
            $taskList = Invoke-PhotocopyAdb -Adb $Adb -Arguments @('-s', $taskSerial, 'reverse', '--list')
            if ($taskList.ExitCode -ne 0 -or ($taskList.Output -join "`n") -notmatch '(?m)(^|\s)tcp:5268\s+tcp:5268(\s|$)') { continue }
        }
        $taskSerial
    }
}

function Watch-PhotocopyAdbReverse {
    param([int]$ApiProcessId, [long]$ApiStartTicks)
    $taskAdb = Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'
    if (-not (Test-Path -LiteralPath $taskAdb)) { return }
    # Một monitor cho API cổng 5268, tồn tại độc lập với cửa sổ launcher/Flutter.
    $taskMutex = New-Object Threading.Mutex($false, 'Local\PhotocopyChain-Adb5268')
    $taskLocked = $false
    try {
        try { $taskLocked = $taskMutex.WaitOne(5000) }
        catch [Threading.AbandonedMutexException] { $taskLocked = $true }
        if (-not $taskLocked) { return }
        while ($true) {
            $taskApi = Get-Process -Id $ApiProcessId -ErrorAction SilentlyContinue
            if (-not $taskApi -or $taskApi.StartTime.Ticks -ne $ApiStartTicks) { return }
            Sync-PhotocopyAdbReverse -Adb $taskAdb | Out-Null
            Start-Sleep -Seconds 3
        }
    }
    finally {
        if ($taskLocked) { $taskMutex.ReleaseMutex() }
        $taskMutex.Dispose()
    }
}

function Start-PhotocopyAdbMonitor {
    param([string]$Solution, [string]$LauncherPath)
    $taskAdb = Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'
    if (-not (Test-Path -LiteralPath $taskAdb)) {
        Write-Warning 'Khong thay Android SDK/adb; Web van chay, Android chua duoc tao reverse.'
        return
    }
    $taskOwners = @(Get-NetTCPConnection -State Listen -LocalPort 5268 -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique)
    if ($taskOwners.Count -ne 1) { throw 'Khong xac dinh duoc API de giu ket noi Android.' }
    $taskApi = @(Get-PhotocopyWebProcesses -Solution $Solution -Processus @(
        Get-CimInstance Win32_Process -Filter ('ProcessId=' + $taskOwners[0])
    ) | Where-Object Name -eq 'PhotocopyApi.exe')
    if ($taskApi.Count -ne 1) { throw 'Cong API khong thuoc PhotocopyApi cua du an nay.' }
    $taskStart = (Get-Process -Id $taskOwners[0]).StartTime.Ticks
    $taskConnected = @(Sync-PhotocopyAdbReverse -Adb $taskAdb)
    Write-Host ('Android: ' + $taskConnected.Count + ' thiet bi da tao reverse; tu khoi phuc moi 3 giay khi ket noi lai.')
    $env:PHOTOCOPY_LAUNCHER = [IO.Path]::GetFullPath($LauncherPath)
    $taskCommand = '$taskCode=[regex]::Split([IO.File]::ReadAllText($env:PHOTOCOPY_LAUNCHER),''(?m)^# POWERSHELL-BEGIN\r?$'')[1]; & ([scriptblock]::Create($taskCode)) -Mode ket-noi -LauncherPath $env:PHOTOCOPY_LAUNCHER -ApiProcessId ' + $taskOwners[0] + ' -ApiStartTicks ' + $taskStart
    $taskEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($taskCommand))
    Start-Process powershell.exe -WorkingDirectory $Solution -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $taskEncoded) -WindowStyle Hidden
}

function Start-PhotocopyApp {
    param([string]$Solution, [string]$LauncherPath)
    $taskAdb = Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'
    if (-not (Test-Path -LiteralPath $taskAdb)) { throw 'Khong thay adb trong Android SDK.' }
    $taskDevices = @(Get-PhotocopyAndroidDevices -Adb $taskAdb)
    if ($taskDevices.Count -eq 0) { throw 'Chua co dien thoai ADB san sang. Ket noi USB/Wi-Fi, mo khoa va cho phep go loi truoc.' }
    $taskSerial = $taskDevices[0]
    if ($taskDevices.Count -gt 1) {
        for ($taskIndex = 0; $taskIndex -lt $taskDevices.Count; $taskIndex++) { Write-Host (($taskIndex + 1).ToString() + '. ' + $taskDevices[$taskIndex]) }
        $taskChoice = 0
        if (-not [int]::TryParse((Read-Host 'Chon so dien thoai de chay app'), [ref]$taskChoice) -or $taskChoice -lt 1 -or $taskChoice -gt $taskDevices.Count) { throw 'Lua chon dien thoai khong hop le.' }
        $taskSerial = $taskDevices[$taskChoice - 1]
    }
    Wait-PhotocopyHttp 'http://localhost:5268/swagger/v1/swagger.json'
    Start-PhotocopyAdbMonitor -Solution $Solution -LauncherPath $LauncherPath
    if ($taskSerial -notin @(Sync-PhotocopyAdbReverse -Adb $taskAdb)) { throw 'Chua tao duoc reverse cho dien thoai da chon. Kiem tra ADB va thu lai.' }
    $taskMobilePath = [IO.Path]::GetFullPath((Join-Path $Solution '..\photocopy_mobile'))
    $taskMappedDrive = $null
    $taskPushed = $false
    try {
        $taskRunPath = $taskMobilePath
        # Android tren Windows can duong dan ASCII; chi anh xa, khong di chuyen source.
        if ($taskMobilePath -match '[^\x00-\x7F]') {
            $taskDrive = Get-PhotocopyAppDrive -OccupiedLetters @(Get-PSDrive -PSProvider FileSystem | ForEach-Object Name)
            subst $taskDrive $taskMobilePath
            if ($LASTEXITCODE -ne 0) { throw 'Khong tao duoc o Android tam.' }
            $taskMappedDrive = $taskDrive
            $taskRunPath = $taskDrive + '\'
        }
        Push-Location $taskRunPath
        $taskPushed = $true
        flutter run -d $taskSerial
        if ($LASTEXITCODE -ne 0) { throw 'Flutter run khong thanh cong.' }
    }
    finally {
        if ($taskPushed) { Pop-Location }
        # Chi go o tam da tao thanh cong; khong go cac o ton tai tu truoc.
        if ($taskMappedDrive) {
            subst $taskMappedDrive /D
        }
    }
}

try {
    $taskSolution = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($LauncherPath)) '..\PhotocopyChain'))
    if ($Mode -eq 'ket-noi') {
        if ($ApiProcessId -gt 0) { Watch-PhotocopyAdbReverse -ApiProcessId $ApiProcessId -ApiStartTicks $ApiStartTicks }
        else { Start-PhotocopyAdbMonitor -Solution $taskSolution -LauncherPath $LauncherPath }
        exit 0
    }
    $env:PHOTOCOPY_ENV_FILE = Join-Path $taskSolution '.env'
    if ($Mode -eq 'app') { Start-PhotocopyApp -Solution $taskSolution -LauncherPath $LauncherPath; exit 0 }
    Test-PhotocopyDatabase $env:PHOTOCOPY_ENV_FILE
    Write-Host 'Khoi dong lai API, Admin va may in ao cua dung du an...'
    Stop-PhotocopyWebServices $taskSolution
    foreach ($taskProject in @('PhotocopyApi\PhotocopyApi.csproj', 'PhotocopyAdmin\PhotocopyAdmin.csproj', 'PhotocopyPrinterSimulator\PhotocopyPrinterSimulator.csproj')) {
        dotnet build (Join-Path $taskSolution $taskProject)
        if ($LASTEXITCODE -ne 0) { throw ('Build loi: ' + $taskProject + '. Khong chay ban cu.') }
    }
    Start-PhotocopyService $taskSolution 'PhotocopyApi\PhotocopyApi.csproj' 'API' -Http
    Wait-PhotocopyHttp 'http://localhost:5268/swagger/v1/swagger.json'
    Start-PhotocopyAdbMonitor -Solution $taskSolution -LauncherPath $LauncherPath
    Start-PhotocopyService $taskSolution 'PhotocopyAdmin\PhotocopyAdmin.csproj' 'Admin' -Http
    Start-PhotocopyService $taskSolution 'PhotocopyPrinterSimulator\PhotocopyPrinterSimulator.csproj' 'Printer Simulator'
    Wait-PhotocopyHttp 'http://localhost:5064/'
    Start-Process 'http://localhost:5268/swagger'
    Start-Process 'http://localhost:5064'
    Write-Host 'Da mo Web Admin, Swagger va may in ao. Dong tung console de dung dich vu.'
    Write-Host 'PayOS that: giu cloudflared chay va dung URL tunnel hien tai.'
    exit 0
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
