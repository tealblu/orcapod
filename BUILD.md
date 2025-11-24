# Build Configurations

This project supports two platform-specific build configurations:

## Windows Configuration

Build for Windows with Windows Forms support:

```bash
dotnet build -c Windows
```

Or publish a self-contained executable:

```bash
dotnet publish -c Windows --self-contained -r win-x64
```

This configuration:

-   Targets `net9.0-windows`
-   Enables Windows Forms
-   Defines the `WINDOWS` compiler constant
-   Includes Windows-specific features (tray icon, Task Scheduler, etc.)

## Linux Configuration

Build for Linux:

```bash
dotnet build -c Linux
```

Or publish a self-contained executable:

```bash
dotnet publish -c Linux --self-contained -r linux-x64
```

This configuration:

-   Targets `net9.0`
-   Defines the `LINUX` compiler constant
-   Excludes Windows-specific dependencies
-   Linux tray icon implementation is pending (TODO)

## Debug/Release Configurations

The standard Debug and Release configurations are still available and will automatically detect the platform:

```bash
dotnet build -c Debug
dotnet build -c Release
```

When using Debug/Release configurations:

-   On Windows: Builds with Windows Forms support
-   On Linux: Builds without Windows-specific features

## Platform-Specific Code

Code uses conditional compilation directives to handle platform differences:

```csharp
#if WINDOWS
    // Windows-specific code
#elif LINUX
    // Linux-specific code
#endif
```
