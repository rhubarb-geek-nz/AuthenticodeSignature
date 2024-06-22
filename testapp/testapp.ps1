#!/usr/bin/env pwsh

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

trap
{
	throw $PSItem
}

if ( Test-Path 'test.ps1' )
{
	Remove-Item 'test.ps1'
}

Copy-Item -LiteralPath 'testapp.ps1' -Destination 'test.ps1'

$pem = @'
-----BEGIN CERTIFICATE-----
MIIDrDCCApSgAwIBAgIUTqiToBIZNd1yUEfo0miaBeySJTYwDQYJKoZIhvcNAQEL
BQAwHTEbMBkGA1UEAwwScmh1YmFyYi1nZWVrLW56IENBMCAXDTIzMDQwMTA1NTk1
MVoYDzIxMjMwMzA4MDU1OTUxWjBQMTQwMgYJKoZIhvcNAQkBFiVyaHViYXJiLWdl
ZWstbnpAdXNlcnMuc291cmNlZm9yZ2UubmV0MRgwFgYDVQQDEw9yaHViYXJiLWdl
ZWstbnowggGiMA0GCSqGSIb3DQEBAQUAA4IBjwAwggGKAoIBgQDsCelvzGsvxTOy
WHgozBArQvIaXpH+UUwwPvqQ851IuUhrmNy9q7C+TSCWkCSAL0YrE/p5LDnHuqoH
JGCtj5ToimRQaJfDv0dRjzEwZloT4/7zRg7MQwDaBXQuVN8gvWOTKKm4ucn49Bxy
p9uUdglxcvWOxqWD3J+lL4JIQ6k4t9yNLUJSqqIM87E55krtQLowtFRx6cb7y3L4
1SsEYEI3mncLT/NJ+0NvLaLtIMhhFNcuCMdC4yIlRbW9EN7sCsSjT11Si9gRTksO
LzHGBe9u/PnZv0M7euWgpjMLq5WAdHtthSqmK8T7z+Kr7Ig1053LFax6c8h6YVk8
qyM0LBxlp+n4/JYhBU/jbpMbyv9ljgfEJeF64bcd4KgLLNi5l5YwMgzfXAt0M6hD
UwGe4qRkerCqfmQQsM1kS02Afj8X37uIn6kLtC2boynLJAI8hEQwFiw7d0FMEA/Z
6CGcFUMM+wY+tqVCbMD3R9GrSS3G22SxkKnw0yfkuUv9gZcsGEcCAwEAAaMvMC0w
DAYDVR0TAQH/BAIwADAdBgNVHSUEFjAUBggrBgEFBQcDAwYIKwYBBQUHAwQwDQYJ
KoZIhvcNAQELBQADggEBABwMuceP7rESJioVuDqU1uOwbOKoQHTu6FfFfaM/csuF
/38UPnqsrq1iPijboy41/11wuiTwrkOyx0PbmwnMOWoh3PORvA/Kou+gCkZZy4Mp
aSgI68HrCwhHguUkRCEgpkNNLNrFP+6ls6SdCbqfTI0yWlQQKSMQ2ywTCHRqR0fx
RmYD45ymRELNqNQgq3MS/IZMLd/tHFKIleahBH2dPC71ppirZmpVep3QkiDDwdkG
fQtXz7vpN/rFCcFS5qt0wF02WZVUEGEPCdmxp2UmRhmeAMrGBzxq93fdEyq0/MsL
YFr03haC8XYc/ud0s9Rj2ZCxMAy+HQbXcAT2z+fKlWw=
-----END CERTIFICATE-----
'@

$codeSignCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem($pem)

Set-AuthenticodeSignature -Certificate $codeSignCertificate -TimestampServer 'http://timestamp.digicert.com' -HashAlgorithm SHA256 -FilePath 'test.ps1'

Get-AuthenticodeSignature -FilePath 'test.ps1'
