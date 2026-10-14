$ErrorActionPreference = 'Stop'
$taskLauncher = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\scripts\chay_web.cmd'))
$taskText = [IO.File]::ReadAllText($taskLauncher)
$taskHeader = [regex]::Split($taskText, '(?m)^# POWERSHELL-BEGIN\r?$')[0]
if ([regex]::IsMatch($taskHeader, "(?<!`r)`n")) { throw 'CMD Windows phai luu header CRLF de chay dung.' }
$taskBody = [regex]::Split($taskText, '(?m)^# POWERSHELL-BEGIN\r?$')[1]
$taskTokens = $null; $taskErrors = $null
$taskAst = [Management.Automation.Language.Parser]::ParseInput($taskBody, [ref]$taskTokens, [ref]$taskErrors)
if ($taskErrors.Count) { throw 'Launcher sai cu phap PowerShell.' }
foreach ($taskFunction in $taskAst.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.FunctionDefinitionAst] }) {
    . ([scriptblock]::Create($taskFunction.Extent.Text))
}
if (-not (Get-Command Sync-PhotocopyAdbReverse -ErrorAction SilentlyContinue)) {
    throw 'Chua co co che khoi phuc reverse tung thiet bi.'
}

# Chỉ thay biên gọi ADB; logic parse/device/reverse của launcher vẫn chạy thật.
$script:taskCalls = [Collections.Generic.List[string]]::new()
$script:taskDevices = @('List of devices attached', 'phone-wifi device product:V2352A', 'phone-usb device', 'phone-off offline', 'phone-denied unauthorized')
$script:taskMappings = @{}
function Invoke-PhotocopyAdb {
    param([string]$Adb, [string[]]$Arguments)
    $script:taskCalls.Add(($Arguments -join ' '))
    if ($Arguments[0] -eq 'devices') { return [pscustomobject]@{ ExitCode=0; Output=$script:taskDevices } }
    if ($Arguments[0] -ne '-s') { throw 'Moi thao tac reverse phai chi ro serial.' }
    $taskSerial = $Arguments[1]
    if ($Arguments[3] -eq '--list') {
        return [pscustomobject]@{ ExitCode=0; Output=@($script:taskMappings[$taskSerial]) }
    }
    if (($Arguments[2..4] -join ' ') -ne 'reverse tcp:5268 tcp:5268') { throw 'Khong duoc doi cong khac.' }
    if ($taskSerial -eq 'phone-wifi' -and $script:taskFailWifi) { return [pscustomobject]@{ ExitCode=1; Output=@() } }
    $script:taskMappings[$taskSerial] = 'host-21 tcp:5268 tcp:5268'
    return [pscustomobject]@{ ExitCode=0; Output=@('5268') }
}

Sync-PhotocopyAdbReverse -Adb 'fixture-adb' | Out-Null
if ($script:taskMappings.Count -ne 2) { throw 'Chi thiet bi device duoc tao reverse.' }
if (($script:taskCalls | Where-Object { $_ -match '^(-s phone-off|-s phone-denied)' }).Count) { throw 'Khong duoc thao tac offline/unauthorized.' }
$taskCount = @($script:taskCalls | Where-Object { $_ -match 'reverse tcp:' }).Count
Sync-PhotocopyAdbReverse -Adb 'fixture-adb' | Out-Null
if (@($script:taskCalls | Where-Object { $_ -match 'reverse tcp:' }).Count -ne $taskCount) { throw 'Reverse dung khong duoc tao lai moi chu ky.' }

# Tháo/kết nối lại: mapping mất phải tự được tạo lại, thiết bị khác giữ nguyên.
$script:taskMappings.Remove('phone-wifi')
$script:taskFailWifi = $true
Sync-PhotocopyAdbReverse -Adb 'fixture-adb' | Out-Null
if ($script:taskMappings.ContainsKey('phone-wifi')) { throw 'Khong duoc coi lenh ADB loi la thanh cong.' }
$script:taskFailWifi = $false
Sync-PhotocopyAdbReverse -Adb 'fixture-adb' | Out-Null
if (-not $script:taskMappings.ContainsKey('phone-wifi')) { throw 'Ket noi lai phai khoi phuc reverse.' }

$script:taskDevices = @('List of devices attached', 'phone-off offline')
if (@(Sync-PhotocopyAdbReverse -Adb 'fixture-adb').Count) { throw 'Khong co thiet bi san sang thi khong bao da ket noi.' }
if ($taskBody -match 'Start-Job') { throw 'Khong dung job chet theo launcher khi Web thoat.' }
if ([regex]::Matches($taskBody, 'Start-PhotocopyAdbMonitor -Solution').Count -lt 2) { throw 'Web va app deu phai bat monitor.' }
if ($taskBody -notmatch '-WindowStyle Hidden' -or $taskBody -notmatch 'Mutex') { throw 'Monitor phai an va chong chay trung.' }
Write-Output 'PASS: tung serial, offline/unauthorized, nhieu thiet bi, khong lap khi dung, tu khoi phuc, loi ADB, Web/app monitor.'
