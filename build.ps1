<#
.SYNOPSIS
Builds the Lookout add-in and its MSI installer.

.DESCRIPTION
1. Runs the unit tests (unless -SkipTests).
2. Builds Lookout.Addin with Visual Studio's MSBuild (a VSTO project cannot be built by
   'dotnet build'). The build signs the VSTO manifests with the certificate named in
   Lookout.Addin.csproj.
3. Extracts that certificate's public key so the installer can add it to the VSTO trust
   list (Security\Inclusion).
4. Builds LookoutSetup (WiX) into LookoutSetup\bin\<Configuration>\.

.PARAMETER Configuration
Release (default) or Debug.

.PARAMETER Version
Product version of the MSI, e.g. 1.1.0 (default). Raise it for upgrades.

.PARAMETER SkipTests
Skip running Lookout.Tests.

.PARAMETER CertPassword
Password of the manifest signing .pfx when the cert is not in the current user's store.
The VS-generated temporary key has none.
#>
param (
	[ValidateSet('Release', 'Debug')]
	[string] $Configuration = 'Release',
	[string] $Version = '1.1.0',
	[switch] $SkipTests,
	[string] $CertPassword = ''
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Find-MSBuild
{
	$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
	if (-not (Test-Path $vswhere)) { throw 'vswhere.exe not found; install Visual Studio.' }
	$path = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
	if (-not $path) { throw 'MSBuild not found.' }
	return $path
}

# the public key of the manifest signing certificate, as base64 RSA modulus and exponent
function Get-ManifestKey
{
	$csproj = [xml](Get-Content 'Lookout.Addin\Lookout.Addin.csproj')
	$ns = New-Object Xml.XmlNamespaceManager($csproj.NameTable)
	$ns.AddNamespace('m', 'http://schemas.microsoft.com/developer/msbuild/2003')
	$thumbprint = $csproj.SelectSingleNode('//m:ManifestCertificateThumbprint', $ns).InnerText
	$keyFile = $csproj.SelectSingleNode('//m:ManifestKeyFile', $ns).InnerText

	$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object Thumbprint -eq $thumbprint | Select-Object -First 1
	if (-not $cert)
	{
		$pfx = Join-Path 'Lookout.Addin' $keyFile
		if (-not (Test-Path $pfx)) { throw "Signing certificate $thumbprint not in the store and $pfx not found." }
		$cert = New-Object Security.Cryptography.X509Certificates.X509Certificate2((Resolve-Path $pfx).Path, $CertPassword)
	}

	$rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($cert)
	$p = $rsa.ExportParameters($false)
	return @{
		Modulus  = [Convert]::ToBase64String($p.Modulus)
		Exponent = [Convert]::ToBase64String($p.Exponent)
		Thumbprint = $cert.Thumbprint
	}
}

if (-not $SkipTests)
{
	Write-Host 'Running tests...' -ForegroundColor Cyan
	dotnet test Lookout.Tests --nologo -v q
	if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

Write-Host "Building Lookout.Addin ($Configuration)..." -ForegroundColor Cyan
$msbuild = Find-MSBuild
& $msbuild 'Lookout.Addin\Lookout.Addin.csproj' /restore /t:Rebuild /p:Configuration=$Configuration /v:m /nologo
if ($LASTEXITCODE -ne 0) { throw 'Add-in build failed.' }

$key = Get-ManifestKey
Write-Host "Manifest signing certificate: $($key.Thumbprint)" -ForegroundColor DarkGray

Write-Host 'Building installer...' -ForegroundColor Cyan
dotnet build 'LookoutSetup\LookoutSetup.wixproj' -c $Configuration --nologo -v m `
	"-p:ProductVersion=$Version" "-p:ManifestModulus=$($key.Modulus)" "-p:ManifestExponent=$($key.Exponent)"
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }

$msi = Get-ChildItem "LookoutSetup\bin\$Configuration" -Filter '*.msi' -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "Built $($msi.FullName)" -ForegroundColor Green
