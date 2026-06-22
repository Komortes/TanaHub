param(
    [Parameter(Mandatory = $true)]
    [string]$Runtime,
    [string]$Version = "0.1.0"
)

$Output = "artifacts/$Runtime"

dotnet publish src/TanaHub.Desktop/TanaHub.Desktop.csproj `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    -p:Version=$Version `
    --output $Output

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Published $Runtime to $Output"
