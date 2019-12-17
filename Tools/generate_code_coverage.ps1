$output = "$PSScriptRoot\coverage.xml"
If (Test-Path $output) {
	Remove-Item $output -Force
}

dotnet test -f netcoreapp3.1 /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=$output /p:Exclude="[xunit.*]*" ../source/tusdotnet.sln

$key = Read-Host -Prompt 'Input Codecov key'
./codecov.sh -f "coverage.xml" -t $key
