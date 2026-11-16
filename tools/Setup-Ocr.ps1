# Use the same isolated OCR environment and package versions on both machines.
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$python=Join-Path $repo '.tools/ocr-python/Scripts/python.exe'
if(-not(Test-Path -LiteralPath $python)){
 & py -3.12 -m venv (Join-Path $repo '.tools/ocr-python')
 if($LASTEXITCODE -ne 0){throw 'Install Python 3.12 (64-bit), then run Setup-Ocr.ps1 again.'}
}
& $python -m pip install --only-binary=:all: -r (Join-Path $PSScriptRoot 'requirements-ocr-lock.txt')
if($LASTEXITCODE -ne 0){throw 'OCR package installation failed'}
& $python -c 'import numpy, cv2, PIL, pdfplumber; from rapidocr_onnxruntime import RapidOCR; RapidOCR(); print(True)'
if($LASTEXITCODE -ne 0){throw 'OCR initialization failed'}
Write-Host 'OCR ready. Rebuild/restart AI CAD Host to use the environment.'
