# SignatureService

This is an ASP.NET web-server written in PowerShell

- Program.ps1 does the process initialisation
- SignatureService.ps1 handles the web request to sign a file

The fingerprint of the certificate must be provided as one of the query parameters.

The Set-AuthenticodeSignature cmdlet is used to sign the files.
