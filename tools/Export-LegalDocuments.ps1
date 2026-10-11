param([switch]$ForPublication, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$legalRoot = Join-Path $projectRoot 'Legal'
$publisher = Get-Content -LiteralPath (Join-Path $legalRoot 'publisher.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$hasIdentity = -not [string]::IsNullOrWhiteSpace($publisher.publisherName)
$hasEmail = -not [string]::IsNullOrWhiteSpace($publisher.contactEmail)
if ($hasEmail) {
    try { $mail = [Net.Mail.MailAddress]::new($publisher.contactEmail) }
    catch { throw 'Legal/publisher.json: enter a valid contact email address.' }
    if ($mail.Address -ne $publisher.contactEmail) { throw 'Enter only the contact email address, without a display name.' }
}
if ($ForPublication -and (-not $hasIdentity -or -not $hasEmail)) {
    throw 'Publication stopped: complete publisherName and contactEmail in Legal/publisher.json. No company is required; use the actual individual publisher identity.'
}
$date = [datetime]::ParseExact($publisher.effectiveDate, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
$draft = -not ($hasIdentity -and $hasEmail)
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $legalRoot 'site' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$documents = @('privacy-policy', 'terms-of-service', 'third-party-notices', 'model-terms')
$navigation = @()
foreach ($language in @('tr', 'en')) {
    $draftText = if (-not $draft) { '' } elseif ($language -eq 'tr') {
        'YAYIN ÖNCESİ TASLAK — Yayıncı ve iletişim bilgileri tamamlanmadan yayımlanmaz.'
    } else { 'PRE-RELEASE DRAFT — Complete publisher and contact details before publication.' }
    foreach ($document in $documents) {
        $name = "$document.$language"
        $body = Get-Content -LiteralPath (Join-Path $legalRoot "templates\$name.txt") -Raw -Encoding UTF8
        $body = $body.Replace('{{PUBLISHER}}', $(if ($hasIdentity) { $publisher.publisherName } else { '[PUBLISHER_NAME / YAYINCI_ADI]' }))
        $body = $body.Replace('{{CONTACT}}', $(if ($hasEmail) { $publisher.contactEmail } else { '[CONTACT_EMAIL / ILETISIM_EPOSTASI]' }))
        $body = $body.Replace('{{DATE}}', $date.ToString('yyyy-MM-dd')).Replace('{{DRAFT}}', $draftText)
        if ($body -match '\{\{[A-Z_]+\}\}') { throw "Unresolved template token in $name" }
        if ($document -eq 'third-party-notices') {
            $body += "`n`n----- ORIGINAL LICENSE TEXTS / ORİJİNAL LİSANS METİNLERİ -----`n"
            foreach ($license in Get-ChildItem -LiteralPath (Join-Path $legalRoot 'licenses') -File -Filter '*.txt' | Sort-Object Name) {
                $body += "`n`n===== $($license.Name) =====`n" + [IO.File]::ReadAllText($license.FullName)
            }
        }
        if ($document -eq 'model-terms') {
            $body += "`n`n----- ORIGINAL JAPANESE TERMS / ORİJİNAL JAPONCA KOŞULLAR -----`n"
            foreach ($modelTerms in @('Tsukuyomi-chan-original.txt', 'Amitaro-original.txt', 'Amitaro16k-original.txt', 'AISO-original.txt')) {
                $body += "`n`n===== $modelTerms =====`n" + [IO.File]::ReadAllText((Join-Path $legalRoot ('licenses\' + $modelTerms)))
            }
        }
        $body = $body.Trim() + "`n"
        [IO.File]::WriteAllText((Join-Path $legalRoot "$name.txt"), $body, $utf8)
        [IO.File]::WriteAllText((Join-Path $OutputDirectory "$name.txt"), $body, $utf8)
        $title = ($body -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1).Trim()
        $encodedTitle = [Net.WebUtility]::HtmlEncode($title)
        $encodedBody = [Net.WebUtility]::HtmlEncode($body)
        $html = @"
<!doctype html>
<html lang="$language"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="referrer" content="no-referrer"><title>$encodedTitle</title>
<style>body{max-width:880px;margin:auto;padding:28px 20px;color:#233047;background:#fff;font:17px/1.65 system-ui,sans-serif}a{color:#5141a8}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:inherit}nav{border-bottom:1px solid #ddd;padding-bottom:16px}@media print{nav{display:none}body{max-width:none;padding:0;font-size:11pt}}</style>
</head><body><nav><a href="index.html">Ses Stüdyosu / Audio Studio</a> · <a href="$name.txt">TXT</a></nav><main><pre>$encodedBody</pre></main></body></html>
"@
        [IO.File]::WriteAllText((Join-Path $OutputDirectory "$name.html"), $html, $utf8)
        $navigation += "<li><a href=`"$name.html`">$encodedTitle ($language)</a></li>"
    }
}
$index = '<!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Ses Stüdyosu — Legal</title><style>body{max-width:880px;margin:auto;padding:28px;font:17px/1.7 system-ui,sans-serif;color:#233047}a{color:#5141a8}</style></head><body><h1>Ses Stüdyosu / Audio Studio</h1>'
if ($draft) { $index += '<p>Yayın öncesi taslak / Pre-release draft. Yayıncı ve iletişim bilgileri tamamlanmalıdır.</p>' }
$index += '<ul>' + ($navigation -join "`n") + '</ul><p>Çevrimdışı okunabilir belgeler / Offline-readable documents. No scripts, cookies or analytics are included.</p></body></html>'
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'index.html'), $index, $utf8)
if ($draft) { Write-Warning 'Draft documents generated. Fill Legal/publisher.json and rerun with -ForPublication before publishing.' }
Write-Output ('Legal website files: ' + $OutputDirectory)
