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

if ( -not $Authorization.Contains($request.Headers.Authorization) )
{
    $response.StatusCode = 401;
    $response.Headers.WWWAuthenticate = $WWWAuthenticate
    Exit
}

[string]$guid = New-Guid

$command = $request.Query['command'][0]
$sha1 = $request.Query['sha1'][0]
$fileDigest = $request.Query['fd'][0]
$timeServer = $request.Query['t'][0]

if ( $command -ne 'sign')
{
    throw ( 'invalid command {0}' -f $command )
}

$container = Join-Path -Path $WebRootPath -ChildPath $guid

$null = New-Item -Path $container -ItemType Directory

try
{
    $pipelineInput.Files | ForEach-Object {
        [Microsoft.Extensions.Logging.LoggerExtensions]::LogInformation($Logger,('Sign "{0}" with "{1}"' -f $_.FileName, $sha1),$null)

        $fileName = Join-Path -Path $container -ChildPath (Split-Path -Path $_.FileName -Leaf )

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

            $codeSignCertificate = Get-ChildItem -path Cert:\ -Recurse -CodeSigningCert | Where-Object { $_.Thumbprint -eq $sha1 }

            if (-not $codeSignCertificate)
            {
                throw ( 'no certificate for {0}' -f $sha1 )
            }

            $result = Set-AuthenticodeSignature -FilePath $fileName -HashAlgorithm $fileDigest -Certificate $codeSignCertificate -TimestampServer $timeServer

            [Microsoft.Extensions.Logging.LoggerExtensions]::LogInformation($Logger,('Set-AuthenticodeSignature {0} {1} {2} {3}' -f $result.SignerCertificate.Thumbprint, $result.Status, $result.StatusMessage, $result.Path), $null )

            $response.ContentType = [Net.Mime.MediaTypeNames+Application]::Octet

            $contentDisposition = New-Object -TypeName System.Net.Mime.ContentDisposition -ArgumentList 'attachment'

            $contentDisposition.FileName = $_.FileName

            $response.Headers.ContentDisposition = [string]$contentDisposition

            Get-Content -LiteralPath $fileName -AsByteStream -ReadCount 0
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
