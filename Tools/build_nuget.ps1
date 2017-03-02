
function setVersionInfo() {
	$assemblyVersion = git describe --abbrev=0 --tags
	$assemblyInformationalVersion = git describe --tags --long	
	$json = Get-Content '.\tusdotnet\project.json' | ConvertFrom-Json
	$json.version = $assemblyVersion + "-*"
	$json | ConvertTo-Json -Depth 100 | set-content '.\tusdotnet\project.json'
}

function removeInternalVisibleTo() {
	$replace = '[assembly: InternalsVisibleTo("tusdotnet.test")]';
	$file = '.\tusdotnet\Properties\AssemblyInfo.cs'
	(Get-Content $file).replace($replace, '') | Set-Content $file
}

function makeBuildFolder() {
	if(Test-Path -Path ".\tusdotnet\" ){
		Remove-Item -Recurse -Force .\tusdotnet\
	}
	
	New-Item -ItemType directory .\tusdotnet\
	robocopy /E ..\Source\tusdotnet\ .\tusdotnet\ /MIR
	copy ..\Source\global.json .\tusdotnet\
}

function verifyNuget() {
	if(![bool](Get-Command dotnet -errorAction SilentlyContinue)) {
		Throw "Could not find dotnet command"
	}
}

function createPackage() {
	cd tusdotnet\
	& dotnet pack -c Release
	cd..
}

function movePackage() {
	move tusdotnet\bin\Release\*.nupkg .\
}

function cleanup() {
	Remove-Item -Recurse -Force .\tusdotnet\
}

verifyNuget
Write-Host Copying files for build...
makeBuildFolder
Write-Host Setting version info...
setVersionInfo
Write-Host Version info set
Write-Host Removing InternalsVisibleTo attribute...
removeInternalVisibleTo
Write-Host Creating package...
createPackage
movePackage
Write-Host Package created
Write-Host Cleaning up...
cleanup
Write-Host Done!
