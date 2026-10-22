@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" (
  echo Chua co moi truong Python. Doc README.md de cai dat.
  pause
  exit /b 1
)
if "%~1"=="" (
  echo Chon anh JPG/PNG MOT DONG trong cua so chon tep.
  ".venv\Scripts\python.exe" thu_ocr.py predict
) else (
  ".venv\Scripts\python.exe" thu_ocr.py predict "%~1"
)
set "ocrExitCode=%errorlevel%"
pause
exit /b %ocrExitCode%
