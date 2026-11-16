# Use the same isolated OCR environment and package versions on both machines.
param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$python=Join-Path $repo '.tools/ocr-python/Scripts/python.exe'
if(-not(Test-Path -LiteralPath $python)){
 if($CheckOnly){$available=Get-Command py -ErrorAction SilentlyContinue;if(-not $available){throw 'Install Python 3.12 64-bit with the py launcher first.'};& $available.Source -3.12 -c 'import sys; assert sys.version_info[:2] == (3,12) and sys.maxsize > 2**32';if($LASTEXITCODE){throw 'Python 3.12 64-bit is required.'};Write-Host 'Python 3.12 available; OCR venv will be created during setup.';return}
 & py -3.12 -m venv (Join-Path $repo '.tools/ocr-python')
 if($LASTEXITCODE -ne 0){throw 'Install Python 3.12 (64-bit), then run Setup-Ocr.ps1 again.'}
}
& $python -c 'import sys; assert sys.version_info[:2] == (3,12) and sys.maxsize > 2**32, "OCR requires Python 3.12 64-bit"'
if($LASTEXITCODE -ne 0){throw 'Existing OCR environment is not Python 3.12 64-bit. Recreate the local venv with Python 3.12.'}
if($CheckOnly){& $python -m pip check;if($LASTEXITCODE){throw 'OCR dependency check failed'};Write-Host 'Existing Python 3.12 OCR environment passed.';return}
& $python -m pip install --only-binary=:all: -r (Join-Path $PSScriptRoot 'requirements-ocr-lock.txt')
if($LASTEXITCODE -ne 0){throw 'OCR package installation failed'}
& $python -c 'import numpy, cv2, PIL, pdfplumber; from rapidocr_onnxruntime import RapidOCR; RapidOCR(); print(True)'
if($LASTEXITCODE -ne 0){throw 'OCR initialization failed'}
Write-Host 'OCR ready. Rebuild/restart AI CAD Host to use the environment.'
