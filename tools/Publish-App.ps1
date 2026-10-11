param([switch]$SkipBuild, [switch]$ForPublication)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildOutput = Join-Path $projectRoot 'bin\Release'
& (Join-Path $PSScriptRoot 'Export-LegalDocuments.ps1') -ForPublication:$ForPublication
if (-not $SkipBuild) {
    & msbuild (Join-Path $projectRoot 'ses uyg.csproj') /t:Build /p:Configuration=Release /verbosity:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
}
$appFiles = @((Join-Path $buildOutput 'ses uyg.exe'), (Join-Path $buildOutput 'ses uyg.exe.config'))
foreach ($appFile in $appFiles) { if (-not (Test-Path -LiteralPath $appFile)) { throw ('Release file missing: ' + $appFile) } }
$appFiles += Get-ChildItem -LiteralPath $buildOutput -Filter '*.dll' -File | ForEach-Object { $_.FullName }
$appFiles += Join-Path $projectRoot 'AI-SES.md'
$appFiles += Join-Path $projectRoot 'README.md'
$appFiles += Join-Path $projectRoot 'LICENSE'
# Require the bundled offline policies and the exported documents to be identical,
# including when the caller reuses a build with -SkipBuild.
$assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $buildOutput 'ses uyg.exe')))
foreach ($document in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Legal') -Filter '*.txt' -File) {
    $stream = $assembly.GetManifestResourceStream('ses_uyg.Legal.' + $document.Name)
    if ($null -eq $stream) { throw ('Missing embedded policy: ' + $document.Name + '. Rebuild Release.') }
    $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8)
    try { $embedded = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($embedded -cne [IO.File]::ReadAllText($document.FullName)) { throw ('Stale embedded policy: ' + $document.Name + '. Rebuild Release.') }
}
$destination = Join-Path $projectRoot 'dist'
[IO.Directory]::CreateDirectory($destination) | Out-Null
$archivePath = Join-Path $destination 'Ses-Studyosu.zip'
$temporaryPath = Join-Path $destination ('Ses-Studyosu-' + [Guid]::NewGuid().ToString('N') + '.tmp')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    $archive = [IO.Compression.ZipFile]::Open($temporaryPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($appFile in $appFiles) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $appFile, [IO.Path]::GetFileName($appFile), [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
        $legalRoot = Join-Path $projectRoot 'Legal'
        $legalFiles = @(Get-ChildItem -LiteralPath $legalRoot -Filter '*.txt' -File)
        $legalFiles += Get-ChildItem -LiteralPath (Join-Path $legalRoot 'licenses') -Recurse -File
        $legalFiles += Get-ChildItem -LiteralPath (Join-Path $legalRoot 'site') -Recurse -File
        foreach ($legalFile in $legalFiles) {
            $entry = 'Legal/' + $legalFile.FullName.Substring($legalRoot.Length + 1).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $legalFile.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $archive.Dispose() }
    if (Test-Path -LiteralPath $archivePath) { [IO.File]::Replace($temporaryPath, $archivePath, [NullString]::Value) }
    else { [IO.File]::Move($temporaryPath, $archivePath) }
} finally { if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath } }
Write-Output ('Application package: ' + $archivePath)
