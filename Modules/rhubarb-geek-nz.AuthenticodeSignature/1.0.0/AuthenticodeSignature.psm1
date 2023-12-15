# Copyright (c) 2023 Roger Brown.
# Licensed under the MIT License.

function Set-AuthenticodeSignature
{
	param(
		[Parameter(Mandatory=$true)]
		[System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
		[Parameter(Mandatory=$true)]
		$FilePath,
		[Parameter(Mandatory=$true)]
		[string]$HashAlgorithm,
		[Parameter(Mandatory=$true)]
		[string]$TimestampServer
	)

	$local = [string]::Join(
			[System.IO.Path]::DirectorySeparatorChar,
			@(
				[System.Environment]::GetFolderPath('LocalApplicationData'),
				'rhubarb-geek-nz.signtool',
				'signtool.config'
			)
	)

	$xmlDoc = [System.Xml.XmlDocument](Get-Content $local)

	$Endpoint = $xmlDoc.SelectSingleNode('/SignTool/Endpoint').InnerText
	$Authorization = $xmlDoc.SelectSingleNode('/SignTool/Authorization').InnerText

	$web = New-Object -Type 'System.Net.WebClient'

	try
	{
		$web.Headers["Authorization"] = $Authorization

		$queryParam = @{
			'command' = 'sign'
			't' = $TimestampServer
			'fd' = $HashAlgorithm
			'sha1' = $Certificate.Thumbprint
		}

		$url = [string]::Join(
			'?',
			@(
				$Endpoint,
				[string]::Join(
					'&',
					(
						$queryParam.Keys | ForEach-Object {
							[string]::Join(
								'=',
								@(
									$_,
									[System.Net.WebUtility]::UrlEncode($queryParam[$_])
								)
							)
						}
					)
				)
			)
		)

		$response = $web.UploadFile($url,$FilePath)

		Set-Content -LiteralPath $FilePath -Value $response -AsByteStream
	}
	finally
	{
		$web.Dispose()
	}

	return [PSCustomObject]@{
		SignerCertificate = $Certificate.Thumbprint
		Status = [System.Management.Automation.SignatureStatus]::Valid
		StatusMessage = 'Signature verified.'
		Path = $FilePath
	}
}

Export-ModuleMember -Function Set-AuthenticodeSignature
