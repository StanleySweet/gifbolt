// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <atomic>
#include <condition_variable>
#include <functional>
#include <future>
#include <memory>
#include <mutex>
#include <queue>
#include <thread>
#include <vector>

namespace GifBolt
{

/// \class ThreadPool
/// \brief Simple thread pool for parallel task execution.
/// \details Manages a fixed number of worker threads processing tasks from a queue.
class ThreadPool
{
   public:
    /// \brief Constructs a thread pool with the specified number of threads.
    /// \param numThreads Number of worker threads (default: hardware concurrency).
    explicit ThreadPool(size_t numThreads = std::thread::hardware_concurrency()) : _stop(false)
    {
        _workers.reserve(numThreads);
        for (size_t i = 0; i < numThreads; ++i)
        {
            _workers.emplace_back([this] { this->WorkerThread(); });
        }
    }

    /// \brief Destroys the thread pool and waits for all tasks to complete.
    ~ThreadPool()
    {
        {
            std::unique_lock<std::mutex> lock(this->_queueMutex);
            this->_stop = true;
        }
        this->_condition.notify_all();
        for (std::thread& worker : this->_workers)
        {
            if (worker.joinable())
            {
                worker.join();
            }
        }
    }

    /// \brief Enqueues a task for execution.
    /// \tparam F Function type.
    /// \tparam Args Argument types.
    /// \param f Function to execute.
    /// \param args Arguments to pass to the function.
    /// \return A future for retrieving the result.
    template <typename F, typename... Args>
    auto Enqueue(F&& f, Args&&... args)
        -> std::future<typename std::invoke_result<F, Args...>::type>
    {
        using ReturnType = typename std::invoke_result<F, Args...>::type;

        auto task = std::make_shared<std::packaged_task<ReturnType()>>(
            std::bind(std::forward<F>(f), std::forward<Args>(args)...));

        std::future<ReturnType> result = task->get_future();
        {
            std::unique_lock<std::mutex> lock(this->_queueMutex);

            // Don't allow enqueueing after stopping the pool
            if (this->_stop)
            {
                throw std::runtime_error("Cannot enqueue on stopped ThreadPool");
            }

            this->_tasks.emplace([task]() { (*task)(); });
        }
        this->_condition.notify_one();
        return result;
    }

    /// \brief Gets the number of worker threads.
    /// \return Number of threads in the pool.
    size_t GetThreadCount() const
    {
        return this->_workers.size();
    }

   private:
    /// \brief Worker thread function that processes tasks from the queue.
    void WorkerThread()
    {
        while (true)
        {
            std::function<void()> task;
            {
                std::unique_lock<std::mutex> lock(this->_queueMutex);
                this->_condition.wait(lock,
                                      [this] { return this->_stop || !this->_tasks.empty(); });

                if (this->_stop && this->_tasks.empty())
                {
                    return;
                }

                if (!this->_tasks.empty())
                {
                    task = std::move(this->_tasks.front());
                    this->_tasks.pop();
                }
            }

            if (task)
            {
                task();
            }
        }
    }

    std::vector<std::thread> _workers;         ///< Worker threads
    std::queue<std::function<void()>> _tasks;  ///< Task queue
    std::mutex _queueMutex;                    ///< Mutex for queue access
    std::condition_variable _condition;        ///< Condition variable for task availability
    std::atomic<bool> _stop;                   ///< Stop flag
};

}  // namespace GifBolt
