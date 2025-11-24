# OrcaPod Project

OrcaPod is a tool designed to backup and sync configuration files for Orca Slicer, with compatibility for other applications. It helps users maintain consistent settings across devices and ensures easy restoration of configs.

![image](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)

## Features

-   **Automatic Process Monitoring**: OrcaPod automatically detects when Orca Slicer exits and shuts down gracefully
-   Backup Orca Slicer config files
-   Sync configs across multiple devices
-   Support for additional app configs
-   Easy restore and migration
-   Cross-platform support (Windows, Linux, macOS)

## Roadmap

-   ~~Basic config syncing~~
-   ~~Autostart when OrcaSlicer starts~~
-   ~~Auto-quit when OrcaSlicer exits~~
-   Autostart on Linux
-   UI for managing settings
-   Migrate from console UI to GUI
-   Cloud sync

## Getting Started

1. Clone this repository:
    ```bash
    git clone https://github.com/yourusername/orcapod-proj.git
    ```
2. Setup instructions coming in the future!

## Usage

-   Run OrcaPod with `--console` flag for debugging: `./orcapod --console`
-   OrcaPod will automatically monitor for Orca Slicer and quit when it exits
-   Configure process monitoring and backup paths in `settings.json`
-   See [PROCESS_MONITORING.md](PROCESS_MONITORING.md) for detailed information about process monitoring

### Configuration

Edit `settings.json` to customize OrcaPod's behavior:

```json
{
    "General": {
        "Interval": "00:00:30",
        "MonitorProcesses": true
    },
    "ProgramTargets": ["OrcaSlicer"],
    "Mappings": {
        "source/path": "backup/path"
    }
}
```

## Compatibility

-   Orca Slicer
-   Other supported apps

## Contributing

Contributions are welcome! Please submit issues or pull requests.

## License

See [LICENSE](./LICENSE) for details.

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=tealblu/orcapod&type=date&legend=top-left)](https://www.star-history.com/#tealblu/orcapod&type=date&legend=top-left)
