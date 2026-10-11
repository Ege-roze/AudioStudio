param([switch]$SkipBuild, [switch]$SkipAi)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $root
if (-not $SkipBuild) {
    & msbuild 'ses uyg.csproj' /t:Build /p:Configuration=Release /verbosity:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
}
$compiler = Join-Path (Split-Path (Get-Command msbuild).Source) '..\Roslyn\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path (Split-Path (Get-Command msbuild).Source) 'Roslyn\csc.exe' }
$output = Join-Path $root 'bin\Release'
$refs = Get-ChildItem -LiteralPath $output -Filter 'NAudio*.dll' | ForEach-Object { '/r:' + $_.FullName }
$refs += '/r:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\netstandard.dll'
& $compiler /nologo /optimize+ /target:exe "/out:$output\AudioPerformanceTests.exe" "/r:$output\ses uyg.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll $refs 'tests\AudioPerformanceTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Audio benchmark compilation failed.' }
& "$output\AudioPerformanceTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Audio benchmark failed.' }
if (-not $SkipAi) {
    $sources = @('AiVoiceCatalog.cs', 'AiVoiceStore.cs', 'AiVoiceClient.cs', 'AiRuntime.cs', 'AiRuntimeInstaller.cs', 'UiText.cs')
    & $compiler /nologo /optimize+ /target:exe "/out:$output\AiPerformanceTests.exe" /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $sources 'tests\AiPerformanceTests.cs'
    if ($LASTEXITCODE -ne 0) { throw 'AI benchmark compilation failed.' }
    & "$output\AiPerformanceTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'AI benchmark failed.' }
}
