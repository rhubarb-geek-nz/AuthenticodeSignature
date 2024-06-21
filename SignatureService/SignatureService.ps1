# Copyright (c) 2024 Roger Brown.
# Licensed under the MIT License.

param(
    [parameter(Mandatory=$true)]
    $context,
    [parameter(ValueFromPipeline=$true,Mandatory=$false)]
    $pipelineInput
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

trap
{
	throw $PSItem
}

$request = $context.Request
$response = $context.Response
$response.StatusCode = 200

if ( -not ( $Authorization -ccontains $request.Headers.Authorization) )
{
    $response.StatusCode = 401;
    $response.Headers.WWWAuthenticate = $WWWAuthenticate
    Exit
}

[string]$guid = New-Guid

$command = $request.Query['command'][0]

if (($command -ne 'sign') -and ($command -ne 'verify'))
{
    throw ( 'invalid command {0}' -f $command )
}

$container = Join-Path -Path $WebRootPath -ChildPath $guid

$Null = New-Item -Path $container -ItemType Directory

try
{
    $pipelineInput.Files | ForEach-Object {
        $baseName = $_.FileName
        $fileName = Join-Path -Path $container -ChildPath (Split-Path -Path $baseName -Leaf )

        $stream = [System.IO.File]::Open($fileName, [System.IO.FileMode]::Create )

        try
        {
            try
            {
                $_.CopyTo($stream)
            }
            finally
            {
                $stream.Dispose()
            }

            switch ($command)
            {
                'sign' {
                    $sha1 = $request.Query['sha1'][0]
                    $fileDigest = $request.Query['fd'][0]
                    $timeServer = $request.Query['t'][0]

                    [Microsoft.Extensions.Logging.LoggerExtensions]::LogInformation($Logger,('Sign "{0}" with "{1}"' -f $baseName, $sha1), $Null)

                    $codeSignCertificate = Get-ChildItem -path Cert:\ -Recurse -CodeSigningCert | Where-Object { $_.Thumbprint -eq $sha1 }

                    if (-not $codeSignCertificate)
                    {
                        throw ( 'no certificate for {0}' -f $sha1 )
                    }

                    $result = Set-AuthenticodeSignature -FilePath $fileName -HashAlgorithm $fileDigest -Certificate $codeSignCertificate -TimestampServer $timeServer

                    [Microsoft.Extensions.Logging.LoggerExtensions]::LogInformation($Logger,('Set-AuthenticodeSignature {0} {1} {2} {3}' -f $result.SignerCertificate.Thumbprint, $result.Status, $result.StatusMessage, $result.Path), $Null)

                    $response.ContentType = [Net.Mime.MediaTypeNames+Application]::Octet

                    $contentDisposition = New-Object -TypeName System.Net.Mime.ContentDisposition -ArgumentList 'attachment'

                    $contentDisposition.FileName = $baseName

                    $response.Headers.ContentDisposition = [string]$contentDisposition

                    Get-Content -LiteralPath $fileName -AsByteStream -ReadCount 0
                }

                'verify' {
                    [Microsoft.Extensions.Logging.LoggerExtensions]::LogInformation($Logger,('Verify "{0}"' -f $baseName), $Null)

                    $result = Get-AuthenticodeSignature -FilePath $fileName

                    $TimeStamperCertificate = $result.TimeStamperCertificate
                    $SignerCertificate = $result.SignerCertificate

                    $hash = @{
                        IsOSBinary = $result.IsOSBinary
                        Path = $baseName
                        SignatureType = $result.SignatureType.ToString()
                        Status = $result.Status.ToString()
                        StatusMessage = $result.StatusMessage
                    }

                    if ($TimeStamperCertificate)
                    {
                        $hash.TimeStamperCertificate = New-Object -TypeName System.String -ArgumentList (,[System.Security.Cryptography.PemEncoding]::Write('CERTIFICATE',$TimeStamperCertificate.RawData))
                    }

                    if ($SignerCertificate)
                    {
                        $hash.SignerCertificate = New-Object -TypeName System.String -ArgumentList (,[System.Security.Cryptography.PemEncoding]::Write('CERTIFICATE',$SignerCertificate.RawData))
                    }

                    $hash | ConvertTo-Json
                }
            }
        }
        finally
        {
            Remove-Item -LiteralPath $fileName
        }

        break
    }
}
finally
{
    Remove-Item -LiteralPath $container
}
