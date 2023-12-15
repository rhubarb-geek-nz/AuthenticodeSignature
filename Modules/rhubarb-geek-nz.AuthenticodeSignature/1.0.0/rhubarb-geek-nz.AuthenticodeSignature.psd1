@{
	RootModule = 'AuthenticodeSignature.psm1'
	ModuleVersion = '1.0.0'
	GUID = '8b37f559-214e-4112-9efd-5acd6732e7fe'
	Author = 'Roger Brown'
	CompanyName = 'rhubarb-geek-nz'
	Copyright = '2023'
	CompatiblePSEditions = @('Core')
	PowerShellVersion = '7.2'
	Description = 'Provides Set-AuthenticodeSignature using HTTP signature service'
	FunctionsToExport = @('Set-AuthenticodeSignature')
	CmdletsToExport = @()
	VariablesToExport = '*'
	AliasesToExport = @()
	PrivateData = @{
		PSData = @{
			ProjectUri = 'https://github.com/rhubarb-geek-nz/AuthenticodeSignature'
		}
	}
}
