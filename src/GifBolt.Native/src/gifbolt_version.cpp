// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include "gifbolt_version.h"

extern "C"
{
    gb_version_info_s gb_version_get_info(void) noexcept
    {
        return gb_version_info_s{
            GIFBOLT_VERSION_MAJOR,
            GIFBOLT_VERSION_MINOR,
            GIFBOLT_VERSION_PATCH,
            GIFBOLT_VERSION_STRING
        };
    }

    int gb_version_get_major(void) noexcept
    {
        return GIFBOLT_VERSION_MAJOR;
    }

    int gb_version_get_minor(void) noexcept
    {
        return GIFBOLT_VERSION_MINOR;
    }

    int gb_version_get_patch(void) noexcept
    {
        return GIFBOLT_VERSION_PATCH;
    }

    const char* gb_version_get_string(void) noexcept
    {
        return GIFBOLT_VERSION_STRING;
    }

    int gb_version_get_int(void) noexcept
    {
        return GIFBOLT_VERSION_INT;
    }

    int gb_version_check(int major, int minor, int patch) noexcept
    {
        int required = major * 10000 + minor * 100 + patch;
        return GIFBOLT_VERSION_INT >= required ? 1 : 0;
    }
}
