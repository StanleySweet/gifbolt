// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdarg>
#include <cstdio>
#include <cstring>
#ifdef _WIN32
#include <windows.h>
#endif

namespace GifBolt
{
inline void DebugLog(const char* format, ...)
{
    char buffer[512];
    va_list args;
    va_start(args, format);
    int written = vsnprintf(buffer, sizeof(buffer), format, args);
    va_end(args);

    if (written < 0)
        return;  // Format error

#ifdef _WIN32
    OutputDebugStringA(buffer);
    
    // Also log to file in temp directory for verification
    char logpath[MAX_PATH];
    if (GetTempPathA(sizeof(logpath), logpath) > 0)
    {
        strcat_s(logpath, sizeof(logpath), "gifbolt_debug.log");
        FILE* f = fopen(logpath, "a");
        if (f)
        {
            fwrite(buffer, 1, strlen(buffer), f);
            fflush(f);
            fclose(f);
        }
    }
#else
    fputs(buffer, stderr);
    fflush(stderr);
#endif
}

}  // namespace GifBolt
