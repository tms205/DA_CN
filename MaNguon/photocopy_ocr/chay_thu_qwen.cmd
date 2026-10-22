@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" (
  echo Chua co moi truong Python. Doc HUONG_DAN_QWEN.md.
  pause
  exit /b 1
)
echo 1. Qwen3-VL 4B - uu tien chat luong, cham hon
echo 2. Qwen3-VL 2B - nhanh hon, nhe hon
choice /C 12 /N /M "Chon model [1/2]: "
if errorlevel 2 (
  set "ocrQwenModel=2b"
) else (
  set "ocrQwenModel=4b"
)
echo Chon anh, khoanh phan chu, cho doc va xem file ket qua.
if "%~1"=="" (
  ".venv\Scripts\python.exe" thu_qwen.py --model %ocrQwenModel% --select-region --open-result
) else (
  ".venv\Scripts\python.exe" thu_qwen.py "%~1" --model %ocrQwenModel% --select-region --open-result
)
set "ocrExitCode=%errorlevel%"
pause
exit /b %ocrExitCode%
