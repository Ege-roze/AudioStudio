param([string]$Destination = (Join-Path $PSScriptRoot '..\AiRuntime'))
$ErrorActionPreference = 'Stop'
$Destination = [IO.Path]::GetFullPath($Destination)
[IO.Directory]::CreateDirectory($Destination) | Out-Null
$archive = Join-Path $Destination 'vcclient-1.5.3.18a.zip'
$url = 'https://huggingface.co/wok000/vcclient000/resolve/main/MMVCServerSIO_win_onnxgpu-cuda_v.1.5.3.18a.zip'
$hash = 'D3B3458A3E7F9944336F446477D10A0987CA12FC97B2E6F5AB0AA75E50577CEB'
if (-not (Test-Path -LiteralPath (Join-Path $Destination 'installed.txt'))) {
    if (-not (Test-Path -LiteralPath $archive) -or (Get-Item -LiteralPath $archive).Length -ne 3308026202) {
        Write-Output 'Downloading official VCClient CUDA runtime (3.3 GB)...'
        & curl.exe --location --fail --retry 3 --continue-at - --output $archive $url
        if ($LASTEXITCODE -ne 0) { throw 'AI runtime download failed. Run this script again to resume.' }
    }
    Write-Output 'Verifying SHA-256...'
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $hash) { throw 'AI runtime hash mismatch.' }
    Write-Output 'Extracting AI runtime...'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($entry in $zip.Entries) {
            $target = [IO.Path]::GetFullPath((Join-Path $Destination $entry.FullName))
            if (-not $target.StartsWith($Destination.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid archive path.' }
            if ($entry.Name -eq '') { [IO.Directory]::CreateDirectory($target) | Out-Null }
            else {
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
            }
        }
    } finally { $zip.Dispose() }
    $server = Get-ChildItem -LiteralPath $Destination -Filter 'MMVCServerSIO.exe' -Recurse -File | Select-Object -First 1
    if (-not $server) { throw 'AI server executable missing.' }
    [IO.File]::WriteAllText((Join-Path $Destination 'installed.txt'), 'VCClient 1.5.3.18a CUDA - SHA256 ' + $hash)
}
Write-Output 'AI runtime installed.'
