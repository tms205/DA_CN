@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" (
  echo Chua co moi truong Python. Doc README.md de cai dat.
  pause
  exit /b 1
)
echo 1. Anh chup vo o ly / co duong ke
echo 2. Anh scan nen trang / khong duong ke
choice /C 12 /N /T 15 /D 1 /M "Chon loai anh [1/2, mac dinh 1 sau 15 giay]: "
set "ocrPaperFlag=--notebook"
if errorlevel 2 set "ocrPaperFlag="
echo Chon anh, sau do keo chuot khoanh sat phan chu can doc.
echo Ket qua va anh danh so dong se duoc luu trong reports.
if "%~1"=="" (
  ".venv\Scripts\python.exe" thu_ocr.py page --select-region %ocrPaperFlag% --open-result
) else (
  ".venv\Scripts\python.exe" thu_ocr.py page "%~1" --select-region %ocrPaperFlag% --open-result
)
set "ocrExitCode=%errorlevel%"
pause
exit /b %ocrExitCode%
