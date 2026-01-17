// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>
#include <memory>
#include <memory_resource>
#include <vector>

namespace GifBolt
{
namespace Memory
{

/// \brief PMR-based memory pool for frame allocations.
/// \details Uses C++17 std::pmr::monotonic_buffer_resource for efficient
///          frame-by-frame allocations without individual deallocations.
class FrameMemoryPool
{
   public:
    /// \brief Initializes a frame memory pool with the specified initial capacity.
    /// \param initialBytes Initial buffer size in bytes (default: 4MB for ~2 full HD frames).
    explicit FrameMemoryPool(size_t initialBytes = 4 * 1024 * 1024)
        : m_Buffer(initialBytes),
          m_Resource(m_Buffer.data(), m_Buffer.size()),
          m_Allocator(&m_Resource)
    {
    }

    /// \brief Gets the PMR allocator for this pool.
    /// \return A polymorphic allocator that uses this pool's memory resource.
    std::pmr::polymorphic_allocator<std::byte> GetAllocator()
    {
        return m_Allocator;
    }

    /// \brief Resets the pool, making all allocated memory available for reuse.
    /// \details Does not free the underlying buffer, just resets the allocation pointer.
    ///          Call this between GIF loading operations to reuse memory.
    void Reset()
    {
        m_Resource.release();
    }

    /// \brief Gets the total capacity of the pool in bytes.
    size_t GetCapacity() const
    {
        return m_Buffer.size();
    }

   private:
    std::vector<std::byte> m_Buffer;                         ///< Pre-allocated buffer
    std::pmr::monotonic_buffer_resource m_Resource;          ///< PMR memory resource
    std::pmr::polymorphic_allocator<std::byte> m_Allocator;  ///< PMR allocator
};

/// \brief Small vector optimization for temporary buffers.
/// \details Uses stack storage for small sizes, heap allocation for larger sizes.
/// \tparam T Element type.
/// \tparam N Stack capacity (number of elements stored inline).
template <typename T, size_t N = 16>
class SmallVector
{
   public:
    SmallVector() : m_Size(0), m_Capacity(N), m_Data(m_StackStorage)
    {
    }

    ~SmallVector()
    {
        if (m_Data != m_StackStorage)
        {
            delete[] m_Data;
        }
    }

    // Disable copy/move for simplicity (can be implemented if needed)
    SmallVector(const SmallVector&) = delete;
    SmallVector& operator=(const SmallVector&) = delete;

    /// \brief Reserves capacity for at least n elements.
    void reserve(size_t n)
    {
        if (n <= m_Capacity)
        {
            return;
        }

        T* newData = new T[n];
        for (size_t i = 0; i < m_Size; ++i)
        {
            newData[i] = std::move(m_Data[i]);
        }

        if (m_Data != m_StackStorage)
        {
            delete[] m_Data;
        }

        m_Data = newData;
        m_Capacity = n;
    }

    /// \brief Resizes the vector to contain n elements.
    void resize(size_t n)
    {
        if (n > m_Capacity)
        {
            reserve(n);
        }
        m_Size = n;
    }

    /// \brief Adds an element to the end.
    void push_back(const T& value)
    {
        if (m_Size >= m_Capacity)
        {
            reserve(m_Capacity * 2);
        }
        m_Data[m_Size++] = value;
    }

    /// \brief Returns the number of elements.
    size_t size() const
    {
        return m_Size;
    }

    /// \brief Returns the current capacity.
    size_t capacity() const
    {
        return m_Capacity;
    }

    /// \brief Checks if the vector is using stack storage.
    bool is_inline() const
    {
        return m_Data == m_StackStorage;
    }

    /// \brief Element access.
    T& operator[](size_t index)
    {
        return m_Data[index];
    }
    const T& operator[](size_t index) const
    {
        return m_Data[index];
    }

    /// \brief Returns a pointer to the underlying data.
    T* data()
    {
        return m_Data;
    }
    const T* data() const
    {
        return m_Data;
    }

   private:
    size_t m_Size;
    size_t m_Capacity;
    T* m_Data;
    T m_StackStorage[N];  ///< Inline storage for small sizes
};

/// \brief Simple arena allocator for short-lived allocations.
/// \details Allocates memory in large chunks and frees all at once.
///          Ideal for frame decoding where all allocations are discarded together.
class ArenaAllocator
{
   public:
    /// \brief Initializes an arena with the specified chunk size.
    /// \param chunkSize Size of each arena chunk in bytes (default: 1MB).
    explicit ArenaAllocator(size_t chunkSize = 1024 * 1024)
        : m_ChunkSize(chunkSize), m_CurrentChunk(nullptr), m_CurrentOffset(0), m_CurrentChunkSize(0)
    {
    }

    ~ArenaAllocator()
    {
        Reset();
    }

    // Disable copy/move
    ArenaAllocator(const ArenaAllocator&) = delete;
    ArenaAllocator& operator=(const ArenaAllocator&) = delete;

    /// \brief Allocates memory from the arena.
    /// \param size Number of bytes to allocate.
    /// \param alignment Alignment requirement (default: alignof(std::max_align_t)).
    /// \return Pointer to allocated memory.
    void* Allocate(size_t size, size_t alignment = alignof(std::max_align_t))
    {
        // Align the current offset
        size_t alignedOffset = (m_CurrentOffset + alignment - 1) & ~(alignment - 1);

        // Check if we need a new chunk
        if (!m_CurrentChunk || alignedOffset + size > m_CurrentChunkSize)
        {
            // Allocate a new chunk (at least chunkSize, or larger if needed)
            size_t newChunkSize = std::max(m_ChunkSize, size + alignment);
            m_CurrentChunk = static_cast<uint8_t*>(::operator new(newChunkSize));
            m_CurrentChunkSize = newChunkSize;
            m_CurrentOffset = 0;
            alignedOffset = 0;

            m_Chunks.push_back(m_CurrentChunk);
        }

        void* ptr = m_CurrentChunk + alignedOffset;
        m_CurrentOffset = alignedOffset + size;

        return ptr;
    }

    /// \brief Resets the arena, freeing all allocated memory.
    void Reset()
    {
        for (uint8_t* chunk : m_Chunks)
        {
            ::operator delete(chunk);
        }
        m_Chunks.clear();
        m_CurrentChunk = nullptr;
        m_CurrentOffset = 0;
        m_CurrentChunkSize = 0;
    }

    /// \brief Gets the total number of bytes allocated across all chunks.
    size_t GetTotalAllocated() const
    {
        size_t total = 0;
        for (size_t i = 0; i < m_Chunks.size(); ++i)
        {
            if (i < m_Chunks.size() - 1)
            {
                total += m_ChunkSize;
            }
            else
            {
                total += m_CurrentOffset;
            }
        }
        return total;
    }

   private:
    size_t m_ChunkSize;
    uint8_t* m_CurrentChunk;
    size_t m_CurrentOffset;
    size_t m_CurrentChunkSize;
    std::vector<uint8_t*> m_Chunks;
};

}  // namespace Memory
}  // namespace GifBolt
