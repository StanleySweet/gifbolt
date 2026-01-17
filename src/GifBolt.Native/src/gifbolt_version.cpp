// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include "gifbolt_version.h"

extern "C"
{
    int gb_version_get_major(void)
    {
        return GIFBOLT_VERSION_MAJOR;
    }

    int gb_version_get_minor(void)
    {
        return GIFBOLT_VERSION_MINOR;
    }

    int gb_version_get_patch(void)
    {
        return GIFBOLT_VERSION_PATCH;
    }

    const char* gb_version_get_string(void)
    {
        return GIFBOLT_VERSION_STRING;
    }

    int gb_version_get_int(void)
    {
        return GIFBOLT_VERSION_INT;
    }

    int gb_version_check(int major, int minor, int patch)
    {
        int required = major * 10000 + minor * 100 + patch;
        return GIFBOLT_VERSION_INT >= required ? 1 : 0;
    }
}
