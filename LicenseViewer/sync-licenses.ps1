# SamirinBoothInformation 内のライセンスファイルを走査し、
# LicenseViewer/licenses へ同期 + licenses.json を生成します。
# 使い方:
#   powershell -ExecutionPolicy Bypass -File .\sync-licenses.ps1

$ErrorActionPreference = "Stop"

$viewerRoot = $PSScriptRoot
$informationRoot = Join-Path (Split-Path $viewerRoot -Parent) "SamirinBoothInformation"
$licensesRoot = Join-Path $viewerRoot "licenses"
$manifestPath = Join-Path $viewerRoot "licenses.json"

if (-not (Test-Path $informationRoot)) {
  throw "SamirinBoothInformation が見つかりません: $informationRoot"
}

New-Item -ItemType Directory -Force -Path $licensesRoot | Out-Null
Get-ChildItem $licensesRoot -Directory -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

$entries = New-Object System.Collections.Generic.List[object]

$files = Get-ChildItem -Path $informationRoot -Recurse -File -Filter "*.txt" |
  Where-Object { $_.Name -match "(?i)license" }

foreach ($file in $files) {
  $rel = $file.FullName.Substring($informationRoot.Length).TrimStart("\", "/")
  $relUnix = $rel -replace "\\", "/"
  $parts = $relUnix -split "/"
  $product = if ($parts.Length -gt 1) { $parts[0] } else { [IO.Path]::GetFileNameWithoutExtension($file.Name) }

  $destDir = Join-Path $licensesRoot $product
  New-Item -ItemType Directory -Force -Path $destDir | Out-Null
  Copy-Item $file.FullName (Join-Path $destDir $file.Name) -Force

  $entries.Add([pscustomobject]@{
      id           = $product
      product      = $product
      fileName     = $file.Name
      relativePath = $relUnix
      localPath    = "licenses/$product/$($file.Name)"
      sourcePath   = "../SamirinBoothInformation/$relUnix"
      title        = $product
      updatedAt    = $file.LastWriteTime.ToString("yyyy-MM-ddTHH:mm:ssK")
    })
}

$sorted = @($entries | Sort-Object id)
$manifest = [pscustomobject]@{
  generatedAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssK")
  sourceRoot  = "../SamirinBoothInformation"
  licenses    = $sorted
}

$json = $manifest | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($manifestPath, $json, [System.Text.UTF8Encoding]::new($false))

Write-Host "Synced $($sorted.Length) license file(s)."
Write-Host "Manifest: $manifestPath"
$sorted | ForEach-Object { Write-Host (" - {0} -> {1}" -f $_.id, $_.localPath) }
