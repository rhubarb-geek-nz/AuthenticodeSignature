#!/bin/sh -ex

PSModulePath=$(pwsh -c "\$Env:PSModulePath")
ParentDir=$(dirname $(pwd))
PSModulePath=$ParentDir/Modules:$PSModulePath pwsh -c ./testapp.ps1

tail test.ps1
