// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>
#include <vector>

namespace GifBolt
{

/// \class PixelBuffer
/// \brief Heap-allocated pixel buffer for safe C#/C++ interop.
/// Manages pixel data with manual memory management (delete on C# side).
class PixelBuffer
{
   public:
    /// \brief Creates a new pixel buffer with the specified size.
    /// \param sizeInBytes Size of the buffer in bytes.
    PixelBuffer(size_t sizeInBytes);

    /// \brief Destructor.
    ~PixelBuffer() = default;

    /// \brief Gets a pointer to the pixel data.
    /// \return Pointer to the buffer data.
    const uint8_t* Data() const { return m_Data.data(); }

    /// \brief Non-const access to data for initialization.
    /// \return Mutable pointer to the buffer data.
    uint8_t* Data() { return m_Data.data(); }

    /// \brief Gets the size of the pixel data in bytes.
    /// \return Size in bytes.
    size_t SizeInBytes() const { return m_Data.size(); }

    /// \brief Copies pixel data into this buffer.
    /// \param source Pointer to source data.
    /// \param sizeInBytes Number of bytes to copy.
    void CopyFrom(const void* source, size_t sizeInBytes);

    // Non-copyable
    PixelBuffer(const PixelBuffer&) = delete;
    PixelBuffer& operator=(const PixelBuffer&) = delete;

    // Moveable
    PixelBuffer(PixelBuffer&&) = default;
    PixelBuffer& operator=(PixelBuffer&&) = default;

   private:
    std::vector<uint8_t> m_Data;  ///< Pixel data buffer
};

/// \brief Opaque handle to a pixel buffer for C API.
typedef void* gb_pixel_buffer_t;

}  // namespace GifBolt
