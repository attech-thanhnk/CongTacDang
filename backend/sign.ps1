$cert = (Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Select-Object -First 1)
if ($null -eq $cert) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=CongTacDangLocal" -CertStoreLocation "Cert:\CurrentUser\My"
}

$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
$rootStore.Open("ReadWrite")
$rootStore.Add($cert)
$rootStore.Close()

Get-ChildItem -Path "d:\repos\CongTacDang\backend\publish\*.dll", "d:\repos\CongTacDang\backend\publish\*.exe" | ForEach-Object {
    Set-AuthenticodeSignature -Certificate $cert -FilePath $_.FullName
}
Write-Host "Signed and trusted!"
