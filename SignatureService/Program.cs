// Copyright (c) 2024 Roger Brown.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using RhubarbGeekNz.AspNetForPowerShell;

var iss = InitialSessionState.CreateDefault();

iss.AddAspNetForPowerShellCmdlets();

string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
string assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
string programPS1 = Path.Combine(assemblyDirectory, "Program.ps1");

using (PowerShell powerShell = PowerShell.Create(iss))
{
    powerShell.AddCommand(programPS1);

    powerShell.AddArgument(args);

    powerShell.Invoke();
}
