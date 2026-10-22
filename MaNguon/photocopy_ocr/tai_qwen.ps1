param([ValidateSet('2b', '4b')][string]$Model = '2b')
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot

function Download-Verified([string]$Url, [string]$Target, [string]$Sha256) {
    if ((Test-Path -LiteralPath $Target) -and
        (Get-FileHash -LiteralPath $Target -Algorithm SHA256).Hash.ToLowerInvariant() -eq $Sha256) {
        Write-Host "Da co file hop le: $Target"
        return
    }
    $partial = $Target + '.part'
    & curl.exe --fail -L --retry 3 --connect-timeout 30 -C - -o $partial $Url
    if ($LASTEXITCODE -ne 0) { throw "Khong tai duoc: $Url. Co the chay lai de tai tiep." }
    if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Sha256) {
        throw "SHA256 khong khop: $partial. Khong nap file nay."
    }
    Move-Item -LiteralPath $partial -Destination $Target -Force
}

if ($Model -eq '2b') {
    $revision = '52d6c8ffea26cc873ac5ad116f8631268d7eb503'
    $number = '2B'
    $modelHash = '089d75c52f4b7ffc56ba998ffc50aae89fcafc755f9e7208aacca281dca6c2ae'
    $projectorHash = 'f9a68fabba69c3b81e153367b2c7521030b0fa8bb0de400c9599c8e6725f9c82'
} else {
    $revision = '1cd86afb9a95c410a6038ab3b40d8b578c892266'
    $number = '4B'
    $modelHash = '66358cb18bb6b3b1b6675aa412c7a88ef01d228f481184d13668e5201c730a0a'
    $projectorHash = '30ba2c7dd3127a4561b6cba9d13d0f711c91bdb38742e2f56d73c8cb596bd06d'
}
$modelDir = Join-Path $taskRoot "models\qwen3-vl-$Model"
$runtimeDir = Join-Path $taskRoot 'third_party\llama-b6907'
New-Item -ItemType Directory -Force -Path $modelDir, $runtimeDir | Out-Null
$base = "https://huggingface.co/Qwen/Qwen3-VL-$number-Instruct-GGUF/resolve/$revision"
Download-Verified "$base/Qwen3VL-$number-Instruct-Q4_K_M.gguf" (Join-Path $modelDir "Qwen3VL-$number-Instruct-Q4_K_M.gguf") $modelHash
Download-Verified "$base/mmproj-Qwen3VL-$number-Instruct-Q8_0.gguf" (Join-Path $modelDir "mmproj-Qwen3VL-$number-Instruct-Q8_0.gguf") $projectorHash
Invoke-WebRequest "$base/README.md" -OutFile (Join-Path $modelDir 'README.md')
Invoke-WebRequest 'https://raw.githubusercontent.com/QwenLM/Qwen3-VL/main/LICENSE' -OutFile (Join-Path $modelDir 'LICENSE')
$archive = Join-Path $runtimeDir 'llama-cuda.zip'
Download-Verified 'https://github.com/ggml-org/llama.cpp/releases/download/b6907/llama-b6907-bin-win-cuda-12.4-x64.zip' $archive 'bbf51c51ad84341e31b78bfdf34a77704084c3eb103b6977b6f6b814b2abb4ba'
Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $runtimeDir 'bin') -Force
Invoke-WebRequest 'https://raw.githubusercontent.com/ggml-org/llama.cpp/b6907/LICENSE' -OutFile (Join-Path $runtimeDir 'LICENSE')
Write-Host 'Da tai va kiem SHA256. Doc HUONG_DAN_QWEN.md de chay.'
