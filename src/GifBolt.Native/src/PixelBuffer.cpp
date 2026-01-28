// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include "PixelBuffer.h"

#include <cstring>

namespace GifBolt
{

PixelBuffer::PixelBuffer(size_t sizeInBytes)
    : m_Data(sizeInBytes)
{
}

void PixelBuffer::CopyFrom(const void* source, size_t sizeInBytes)
{
    if (source == nullptr || sizeInBytes == 0)
    {
        m_Data.clear();
        return;
    }

    if (m_Data.size() != sizeInBytes)
    {
        m_Data.resize(sizeInBytes);
    }

    std::memcpy(m_Data.data(), source, sizeInBytes);
}

}  // namespace GifBolt
