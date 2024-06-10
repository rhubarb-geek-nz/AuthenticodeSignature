# Copyright (c) 2024 Roger Brown.
# Licensed under the MIT License.

param($args)

trap
{
	throw $PSItem
}

$app = New-AspNetForPowerShellWebApplication -ArgumentList $args

$iss = [System.Management.Automation.Runspaces.InitialSessionState]::CreateDefault()
$env = $app.Services.GetService([Microsoft.AspNetCore.Hosting.IWebHostEnvironment])
$cfg = $app.Services.GetService([Microsoft.Extensions.Configuration.IConfiguration]).GetSection('Signtool')
$log = $app.Services.GetService([Microsoft.Extensions.Logging.ILogger[RhubarbGeekNz.AspNetForPowerShell.NewRequestDelegate]])

[Microsoft.Extensions.Logging.LoggerExtensions]::LogInformation($log,('WebRootPath {0}' -f $env.WebRootPath),$null)

foreach ($var in
	('ContentRootPath',$env.ContentRootPath,'Content Root Path'),
	('WebRootPath',$env.WebRootPath,'Web Root Path'),
	('Logger',$log,'Logger'),
	('Authorization',[Microsoft.Extensions.Configuration.ConfigurationBinder]::Get($cfg.GetSection('Authorization'),[System.Collections.Generic.List[string]]),'Authorization'),
	('WWWAuthenticate',$cfg['WWW-Authenticate'],'WWW-Authenticate')
)
{
	$iss.Variables.Add((New-Object -TypeName 'System.Management.Automation.Runspaces.SessionStateVariableEntry' -ArgumentList $var))
}

$service = Join-Path -Path $PSScriptRoot -ChildPath 'SignatureService.ps1'

[Microsoft.Extensions.Logging.LoggerExtensions]::LogInformation($log,('SignatureService {0}' -f $service),$null)

$delegate = Get-Command -Name $service | Select-Object -ExpandProperty ScriptBlock

[Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions]::MapPost(
	$app,
	$cfg['Endpoint'],
	(New-AspNetForPowerShellRequestDelegate -ScriptBlock $delegate -InitialSessionState $iss)
)

$app.Run()
