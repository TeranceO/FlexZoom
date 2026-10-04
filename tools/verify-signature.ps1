param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
$signature = Get-AuthenticodeSignature -LiteralPath $Path
if ($signature.Status -ne 'Valid') { throw "Release signature must be Valid; received $($signature.Status)." }
if ($signature.SignerCertificate.Subject -notmatch '(^|,\s*)CN=SignPath Foundation(,|$)') {
    throw 'Release must be signed by the approved SignPath Foundation publisher.'
}
if (-not $signature.TimeStamperCertificate) { throw 'A trusted timestamp is required.' }
$metadata = (Get-Item -LiteralPath $Path).VersionInfo
if ($metadata.ProductName -cne 'Flex Zoom' -or $metadata.ProductVersion -cne $Version -or $metadata.OriginalFilename -cne 'FlexZoom.dll') {
    throw 'Signed executable metadata does not match the release.'
}
Write-Output "Verified Flex Zoom $Version, publisher $($signature.SignerCertificate.Subject), timestamp $($signature.TimeStamperCertificate.Subject)."
