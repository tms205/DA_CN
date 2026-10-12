@echo off
setlocal
rem Bam dup de chay Web + API + may in ao. Go chay_web.cmd app de chay Flutter qua USB.
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
    [ValidateSet('web', 'app')][string]$Mode = 'web',
    [string]$LauncherPath
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

function Start-PhotocopyApp {
    param([string]$Solution)
    $taskAdb = Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'
    if (-not (Test-Path -LiteralPath $taskAdb)) { throw 'Khong thay adb trong Android SDK.' }
    if (-not ((& $taskAdb devices) -match "`tdevice$")) { throw 'Cam dien thoai USB, mo khoa va cho phep go loi USB truoc.' }
    Wait-PhotocopyHttp 'http://localhost:5268/swagger/v1/swagger.json'
    & $taskAdb reverse tcp:5268 tcp:5268 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Khong tao duoc adb reverse.' }
    $taskKeep = Start-Job -ArgumentList $taskAdb -ScriptBlock {
        param($taskAdb)
        while ($true) {
            if ((& $taskAdb devices) -match "`tdevice$" -and
                -not ((& $taskAdb reverse --list) -match 'tcp:5268 tcp:5268')) {
                & $taskAdb reverse tcp:5268 tcp:5268 | Out-Null
            }
            Start-Sleep -Seconds 3
        }
    }
    try {
        Push-Location (Join-Path $Solution '..\photocopy_mobile')
        flutter run
        if ($LASTEXITCODE -ne 0) { throw 'Flutter run khong thanh cong.' }
    }
    finally {
        Pop-Location
        Stop-Job $taskKeep -ErrorAction SilentlyContinue
        Remove-Job $taskKeep -Force -ErrorAction SilentlyContinue
    }
}

try {
    $taskSolution = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($LauncherPath)) '..\PhotocopyChain'))
    $env:PHOTOCOPY_ENV_FILE = Join-Path $taskSolution '.env'
    if ($Mode -eq 'app') { Start-PhotocopyApp $taskSolution; exit 0 }
    Test-PhotocopyDatabase $env:PHOTOCOPY_ENV_FILE
    Write-Host 'Khoi dong lai API, Admin va may in ao cua dung du an...'
    Stop-PhotocopyWebServices $taskSolution
    foreach ($taskProject in @('PhotocopyApi\PhotocopyApi.csproj', 'PhotocopyAdmin\PhotocopyAdmin.csproj', 'PhotocopyPrinterSimulator\PhotocopyPrinterSimulator.csproj')) {
        dotnet build (Join-Path $taskSolution $taskProject)
        if ($LASTEXITCODE -ne 0) { throw ('Build loi: ' + $taskProject + '. Khong chay ban cu.') }
    }
    Start-PhotocopyService $taskSolution 'PhotocopyApi\PhotocopyApi.csproj' 'API' -Http
    Wait-PhotocopyHttp 'http://localhost:5268/swagger/v1/swagger.json'
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
