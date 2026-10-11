param([switch]$Render, [switch]$AiRuntime, [switch]$AiVoices, [switch]$AiPreviewDevices, [switch]$SkipBuild, [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $root
$compiler = Join-Path (Split-Path (Get-Command msbuild).Source) '..\Roslyn\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path (Split-Path (Get-Command msbuild).Source) 'Roslyn\csc.exe' }
$output = Join-Path $root ('bin\' + $Configuration)
$aiSources = @('AiVoiceCatalog.cs', 'AiVoiceStore.cs', 'AiVoiceClient.cs', 'AiRuntime.cs', 'AiRuntimeInstaller.cs', 'UiText.cs')
if (-not $SkipBuild) {
    & msbuild 'ses uyg.csproj' /t:Build "/p:Configuration=$Configuration" /verbosity:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
if ($AiRuntime) {
    & $compiler /nologo /target:exe "/out:$output\AiRuntimeSmokeTests.exe" /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $aiSources 'tests\AiRuntimeSmokeTests.cs'
    if ($LASTEXITCODE -ne 0) { throw 'AI smoke test compilation failed.' }
    & "$output\AiRuntimeSmokeTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'AI smoke test failed.' }
    exit
}
if ($AiVoices) {
    & $compiler /nologo /target:exe "/out:$output\AiVoicesSmokeTests.exe" /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $aiSources 'tests\AiVoicesSmokeTests.cs'
    if ($LASTEXITCODE -ne 0) { throw 'Voice smoke test compilation failed.' }
    & "$output\AiVoicesSmokeTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Voice smoke test failed.' }
    exit
}
$refs = Get-ChildItem -LiteralPath $output -Filter 'NAudio*.dll' | ForEach-Object { '/r:' + $_.FullName }
$refs += '/r:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\netstandard.dll'
if ($AiPreviewDevices) {
    & $compiler /nologo /target:exe "/out:$output\AiPreviewDeviceSmokeTests.exe" "/r:$output\ses uyg.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll $refs 'tests\AiPreviewDeviceSmokeTests.cs'
    if ($LASTEXITCODE -ne 0) { throw 'AI preview hardware test compilation failed.' }
    & "$output\AiPreviewDeviceSmokeTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'AI preview hardware test failed.' }
    exit
}
& $compiler /nologo /target:exe "/out:$output\AudioRegressionTests.exe" "/r:$output\ses uyg.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll $refs 'tests\AudioRegressionTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Audio test compilation failed.' }
& "$output\AudioRegressionTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Audio tests failed.' }
& $compiler /nologo /target:exe "/out:$output\SettingsStoreTests.exe" /r:System.Web.Extensions.dll 'ApplicationSettings.cs' 'WindowsStartup.cs' 'tests\SettingsStoreTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Settings test compilation failed.' }
& "$output\SettingsStoreTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Settings tests failed.' }
& $compiler /nologo /target:exe "/out:$output\LocalizationSmokeTests.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll 'tests\LocalizationSmokeTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'UI test compilation failed.' }
if ($Render) { & "$output\LocalizationSmokeTests.exe" "$output\ses uyg.exe" "$root\tests\previews" }
else { & "$output\LocalizationSmokeTests.exe" "$output\ses uyg.exe" "$root\tests\previews" '--headless' }
if ($LASTEXITCODE -ne 0) { throw 'UI tests failed.' }
& $compiler /nologo /target:exe "/out:$output\AppearanceRegressionTests.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll 'BackgroundAppearance.cs' 'ApplicationTheme.cs' 'PersonalizationForm.cs' 'GlassControls.cs' 'UiText.cs' 'tests\AppearanceRegressionTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Appearance test compilation failed.' }
& "$output\AppearanceRegressionTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Appearance tests failed.' }
& $compiler /nologo /target:exe "/out:$output\ThemeIntegrationTests.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll 'tests\ThemeIntegrationTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Theme test compilation failed.' }
& "$output\ThemeIntegrationTests.exe" "$output\ses uyg.exe" "$root\tests\previews"
if ($LASTEXITCODE -ne 0) { throw 'Theme tests failed.' }
& $compiler /nologo /target:exe "/out:$output\AiSessionRegressionTests.exe" /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll 'AiVoiceCatalog.cs' 'AiVoiceClient.cs' 'UiText.cs' 'tests\AiSessionRegressionTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'AI queue test compilation failed.' }
& "$output\AiSessionRegressionTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'AI queue tests failed.' }
& $compiler /nologo /target:exe "/out:$output\AiInstallerRegressionTests.exe" /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $aiSources 'tests\AiInstallerRegressionTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'AI installer test compilation failed.' }
& "$output\AiInstallerRegressionTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'AI installer tests failed.' }
& $compiler /nologo /target:exe "/out:$output\AiVoiceStoreTests.exe" /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $aiSources 'tests\AiVoiceStoreTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Voice store test compilation failed.' }
& "$output\AiVoiceStoreTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Voice store tests failed.' }
& $compiler /nologo /target:exe "/out:$output\AiVoiceManagementTests.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll 'tests\AiVoiceManagementTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Model management UI test compilation failed.' }
& "$output\AiVoiceManagementTests.exe" "$output\ses uyg.exe"
if ($LASTEXITCODE -ne 0) { throw 'Model management UI tests failed.' }
& $compiler /nologo /target:exe "/out:$output\LegalDocumentTests.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll 'tests\LegalDocumentTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Legal document test compilation failed.' }
& "$output\LegalDocumentTests.exe" "$output\ses uyg.exe" "$root\Legal\site" "$root\tests\previews"
if ($LASTEXITCODE -ne 0) { throw 'Legal document tests failed.' }
& $compiler /nologo /target:exe "/out:$output\AiPrivacyTests.exe" /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll 'AiVoiceCatalog.cs' 'AiVoiceClient.cs' 'UiText.cs' 'tests\AiPrivacyTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'AI privacy test compilation failed.' }
& "$output\AiPrivacyTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'AI privacy tests failed.' }
