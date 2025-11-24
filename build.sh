#!/bin/bash
# Build script for OrcaPod

set -e

echo "OrcaPod Build Script"
echo "===================="
echo ""

function show_help {
    echo "Usage: ./build.sh [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --windows       Build for Windows"
    echo "  --linux         Build for Linux"
    echo "  --all           Build for all platforms"
    echo "  --publish       Create publish builds (self-contained)"
    echo "  --clean         Clean build artifacts"
    echo "  --help          Show this help message"
    echo ""
    echo "Examples:"
    echo "  ./build.sh --linux"
    echo "  ./build.sh --windows --publish"
    echo "  ./build.sh --all --publish"
}

PLATFORM=""
PUBLISH=false
CLEAN=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --windows)
            PLATFORM="windows"
            shift
            ;;
        --linux)
            PLATFORM="linux"
            shift
            ;;
        --all)
            PLATFORM="all"
            shift
            ;;
        --publish)
            PUBLISH=true
            shift
            ;;
        --clean)
            CLEAN=true
            shift
            ;;
        --help)
            show_help
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

# Clean if requested
if [ "$CLEAN" = true ]; then
    echo "Cleaning build artifacts..."
    dotnet clean
    echo ""
fi

# Build function
function build_platform {
    local config=$1
    local runtime=$2
    
    echo "Building for $config..."
    
    if [ "$PUBLISH" = true ]; then
        echo "Publishing self-contained executable..."
        dotnet publish -c "$config" --self-contained -r "$runtime" -o "publish/$config"
        echo "Published to: publish/$config/"
    else
        dotnet build -c "$config"
        echo "Built successfully!"
    fi
    echo ""
}

# Build based on platform selection
case $PLATFORM in
    windows)
        build_platform "Windows" "win-x64"
        ;;
    linux)
        build_platform "Linux" "linux-x64"
        ;;
    all)
        build_platform "Windows" "win-x64"
        build_platform "Linux" "linux-x64"
        ;;
    *)
        echo "No platform specified. Use --windows, --linux, or --all"
        show_help
        exit 1
        ;;
esac

echo "Build complete!"
