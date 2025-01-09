#!/usr/bin/env pwsh
# Copyright (c) 2024 Roger Brown.
# Licensed under the MIT License.

param($args)

trap
{
	throw $PSItem
}

$DSC = [System.IO.Path]::DirectorySeparatorChar

$dllPath = [System.String].Assembly.Location.Split($DSC)
$dllPath[-3] = 'Microsoft.AspNetCore.App'
$dllPath[-1] = 'Microsoft.AspNetCore.dll'
[System.String]::Join($DSC, $dllPath) | Import-Module

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

$script = Get-Command -Name $service | Select-Object -ExpandProperty ScriptBlock
$delegate = New-AspNetForPowerShellRequestDelegate -ScriptBlock $script -InitialSessionState $iss

$null = [Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions]::MapPost(
	$app,
	$cfg['Endpoint'],
	$delegate
)

$app.Run()
