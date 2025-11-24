# Build script for OrcaPod (PowerShell)

param(
    [switch]$Windows,
    [switch]$Linux,
    [switch]$All,
    [switch]$Publish,
    [switch]$Clean,
    [switch]$Help
)

function Show-Help {
    Write-Host "OrcaPod Build Script"
    Write-Host "===================="
    Write-Host ""
    Write-Host "Usage: .\build.ps1 [OPTIONS]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Windows        Build for Windows"
    Write-Host "  -Linux          Build for Linux"
    Write-Host "  -All            Build for all platforms"
    Write-Host "  -Publish        Create publish builds (self-contained)"
    Write-Host "  -Clean          Clean build artifacts"
    Write-Host "  -Help           Show this help message"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\build.ps1 -Windows"
    Write-Host "  .\build.ps1 -Windows -Publish"
    Write-Host "  .\build.ps1 -All -Publish"
}

if ($Help) {
    Show-Help
    exit 0
}

Write-Host "OrcaPod Build Script"
Write-Host "===================="
Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "Cleaning build artifacts..."
    dotnet clean
    Write-Host ""
}

# Build function
function Build-Platform {
    param(
        [string]$Config,
        [string]$Runtime
    )
    
    Write-Host "Building for $Config..."
    
    if ($Publish) {
        Write-Host "Publishing self-contained executable..."
        dotnet publish -c $Config --self-contained -r $Runtime -o "publish\$Config"
        Write-Host "Published to: publish\$Config\"
    } else {
        dotnet build -c $Config
        Write-Host "Built successfully!"
    }
    Write-Host ""
}

# Build based on platform selection
if ($All) {
    Build-Platform "Windows" "win-x64"
    Build-Platform "Linux" "linux-x64"
} elseif ($Windows) {
    Build-Platform "Windows" "win-x64"
} elseif ($Linux) {
    Build-Platform "Linux" "linux-x64"
} else {
    Write-Host "No platform specified. Use -Windows, -Linux, or -All"
    Write-Host ""
    Show-Help
    exit 1
}

Write-Host "Build complete!"
