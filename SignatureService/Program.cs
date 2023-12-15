// Copyright (c) 2023 Roger Brown.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using RhubarbGeekNz.AspNetForPowerShell;

var iss = InitialSessionState.CreateDefault();

iss.AddAspNetForPowerShellCmdlets();

using (PowerShell powerShell = PowerShell.Create(iss))
{
    powerShell.AddScript(File.ReadAllText("Program.ps1"));

    powerShell.AddArgument(args);

    powerShell.Invoke();
}
