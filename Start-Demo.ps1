param([string]$Server='localhost',[int]$Port=5080,[switch]$SkipBuild,[switch]$SkipDatabase)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
if(-not $SkipDatabase){
 Push-Location database
 try { & sqlcmd -S $Server -E -C -I -i KU_Sports_Setup.sql -b; if($LASTEXITCODE -ne 0){throw 'Database setup failed'} } finally {Pop-Location}
}
if(-not $SkipBuild){
 & dotnet restore backend --configfile NuGet.Config; if($LASTEXITCODE -ne 0){throw 'Restore failed'}
 Push-Location frontend
 try { & npm.cmd ci; if($LASTEXITCODE -ne 0){throw 'npm install failed'}; & npm.cmd run build; if($LASTEXITCODE -ne 0){throw 'Frontend build failed'} } finally {Pop-Location}
 & dotnet build backend --no-restore; if($LASTEXITCODE -ne 0){throw 'Backend build failed'}
}
$credentialsPath=Join-Path $PSScriptRoot 'docs\DemoCredentials.local.md'
if(Test-Path -LiteralPath $credentialsPath){
 $credentialText=Get-Content -LiteralPath $credentialsPath -Raw
 $demoPassword=[regex]::Match($credentialText,'Password: ([A-Za-z0-9_-]+)').Groups[1].Value
}else{$demoPassword=''}
if(-not $demoPassword){
 $randomBytes=New-Object byte[] 18
 $rng=[Security.Cryptography.RandomNumberGenerator]::Create()
 $rng.GetBytes($randomBytes); $rng.Dispose()
 $demoPassword=[Convert]::ToBase64String($randomBytes).Replace('+','-').Replace('/','_')
}
Set-Content -LiteralPath $credentialsPath -Encoding utf8 -Value "# Local demo credentials`nUsername: admin`nPassword: $demoPassword`n`nThis file is excluded from git. Do not publish it."
$env:ConnectionStrings__SqlServer="Server=$Server;Database=KU_Sports;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
$env:Demo__AdminPassword=$demoPassword
$env:Demo__ResetAdminPassword='true'
$env:ASPNETCORE_URLS="http://localhost:$Port"
Write-Host "Website: http://localhost:$Port"
Write-Host 'Admin credentials: docs/DemoCredentials.local.md'
Push-Location backend
try { & dotnet .\bin\Debug\net10.0\backend.dll } finally { Pop-Location }
