# Version API Usage

The GifBolt.Native library now includes full semantic versioning support.

## C++ Usage

```cpp
#include "gifbolt_version.h"

// Compile-time version check
#if GIFBOLT_VERSION_CHECK(1, 0, 0)
    // Code requiring version 1.0.0 or higher
#endif

// Runtime version check
int main() {
    printf("GifBolt.Native version: %s\n", gb_version_get_string());
    printf("Major: %d, Minor: %d, Patch: %d\n",
           gb_version_get_major(),
           gb_version_get_minor(),
           gb_version_get_patch());

    if (gb_version_check(1, 0, 0)) {
        printf("Version 1.0.0 or higher detected\n");
    }

    return 0;
}
```

## C# Usage

```csharp
using GifBolt;

// Get version information
Console.WriteLine($"Native library version: {NativeVersion.VersionString}");
Console.WriteLine($"Version object: {NativeVersion.Version}");
Console.WriteLine($"Major: {NativeVersion.Major}, Minor: {NativeVersion.Minor}, Patch: {NativeVersion.Patch}");

// Check minimum version requirements
if (NativeVersion.IsAtLeast(1, 0, 0))
{
    Console.WriteLine("Native library meets minimum requirements");
}

// Or use Version object
var requiredVersion = new Version(1, 0, 0);
if (NativeVersion.IsAtLeast(requiredVersion))
{
    Console.WriteLine("Compatible version detected");
}
```

## Version Format

GifBolt follows [Semantic Versioning 2.0.0](https://semver.org/):

- **MAJOR** version: Incompatible API changes
- **MINOR** version: Backwards-compatible functionality added
- **PATCH** version: Backwards-compatible bug fixes

Current version: **1.0.0**
