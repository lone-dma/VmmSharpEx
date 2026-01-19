// Issue14Native.cpp : Native repro of Issue #14 workload using MemProcFS C API (vmmdll.h)
// STRESS TEST VERSION - Attempting to reproduce .NET crash behavior

#include <Windows.h>

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstdint>
#include <cstdlib>
#include <iostream>
#include <memory>
#include <random>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

#include "vmmdll.h"

#pragma comment(lib, "lib\\vmm.lib")
#pragma comment(lib, "lib\\leechcore.lib")

namespace
{
    // Global flag to simulate "GC-like" pauses across all threads
    std::atomic<bool> g_simulateGCPause{false};
    std::atomic<int> g_totalWorkers{0};
    std::atomic<int> g_crashCount{0};

    struct PMemPageEntry
    {
        uint64_t PageBase = 0;
        uint64_t RemainingBytesInSection = 0;
    };

    class VmmSession
    {
    public:
        explicit VmmSession(std::vector<std::string> args)
        {
            std::vector<LPCSTR> argv;
            argv.reserve(args.size());
            for (auto& a : args)
            {
                argv.push_back(a.c_str());
            }

            hVmm_ = VMMDLL_Initialize(static_cast<DWORD>(argv.size()), argv.data());
            if (!hVmm_)
            {
                throw std::runtime_error("VMMDLL_Initialize failed.");
            }

            if (!VMMDLL_Map_GetPhysMem(hVmm_, &physMemMap_))
            {
                throw std::runtime_error("VMMDLL_Map_GetPhysMem failed.");
            }

            BuildPageList();
        }

        VmmSession(const VmmSession&) = delete;
        VmmSession& operator=(const VmmSession&) = delete;

        ~VmmSession()
        {
            if (physMemMap_)
            {
                VMMDLL_MemFree(physMemMap_);
                physMemMap_ = nullptr;
            }
            if (hVmm_)
            {
                VMMDLL_Close(hVmm_);
                hVmm_ = nullptr;
            }
        }

        VMM_HANDLE Handle() const noexcept { return hVmm_; }
        const std::vector<PMemPageEntry>& Pages() const noexcept { return pages_; }

        PMemPageEntry GetRandomPage(std::mt19937_64& prng) const
        {
            std::uniform_int_distribution<size_t> dist(0, pages_.size() - 1);
            return pages_[dist(prng)];
        }

        std::vector<PMemPageEntry> GetRandomPages(std::mt19937_64& prng, size_t count) const
        {
            std::vector<PMemPageEntry> result;
            result.reserve(count);
            for (size_t i = 0; i < count; i++)
            {
                result.push_back(GetRandomPage(prng));
            }
            return result;
        }

    private:
        void BuildPageList()
        {
            if (!physMemMap_ || physMemMap_->dwVersion != VMMDLL_MAP_PHYSMEM_VERSION)
            {
                throw std::runtime_error("Unexpected physmem map version.");
            }

            std::vector<PMemPageEntry> list;
            for (DWORD i = 0; i < physMemMap_->cMap; i++)
            {
                const auto& entry = physMemMap_->pMap[i];
                uint64_t p = entry.pa;
                uint64_t cbToEnd = entry.cb;

                while (cbToEnd > 0x1000)
                {
                    list.push_back(PMemPageEntry{p, cbToEnd});
                    p += 0x1000;
                    cbToEnd -= 0x1000;
                }
            }

            std::random_device rd;
            std::mt19937_64 prng(rd());
            std::shuffle(list.begin(), list.end(), prng);
            pages_ = std::move(list);
        }

        VMM_HANDLE hVmm_ = nullptr;
        PVMMDLL_MAP_PHYSMEM physMemMap_ = nullptr;
        std::vector<PMemPageEntry> pages_;
    };

    inline DWORD FlagsFromBool(bool noCache)
    {
        return noCache ? VMMDLL_FLAG_NOCACHE : 0;
    }

    // Simulate what .NET does - random yields/sleeps like GC safepoints
    inline void SimulateGCSafepoint(std::mt19937_64& prng)
    {
        // Check if a "GC" is happening
        if (g_simulateGCPause.load(std::memory_order_relaxed))
        {
            Sleep(1);  // Simulate GC pause
        }
        
        std::uniform_int_distribution<int> dist(0, 100);
        int r = dist(prng);
        if (r < 5)
        {
            SwitchToThread();  // Yield to other threads
        }
        else if (r < 10)
        {
            Sleep(0);  // Yield timeslice
        }
    }

    void DoReads(VmmSession& vmm)
    {
        thread_local std::mt19937_64 prng([] {
            std::random_device rd;
            return std::mt19937_64(rd());
        }());

        std::uniform_int_distribution<int> boolDist(0, 1);
        const DWORD flags = FlagsFromBool(boolDist(prng) == 1);

        std::uniform_int_distribution<int> countDist(4, 4096);
        const int count = countDist(prng);
        auto pages = vmm.GetRandomPages(prng, static_cast<size_t>(count));

        // Safepoint before Initialize
        SimulateGCSafepoint(prng);

        VMMDLL_SCATTER_HANDLE hS = VMMDLL_Scatter_Initialize(vmm.Handle(), static_cast<DWORD>(-1), flags);
        if (!hS)
        {
            return;
        }

        // Safepoint after Initialize
        SimulateGCSafepoint(prng);

        std::uniform_int_distribution<uint32_t> cbDist(4, 0x01E00000);
        std::vector<uint32_t> cbWants(pages.size());
        
        // STEP 1: Prepare reads with safepoints interspersed
        for (size_t i = 0; i < pages.size(); i++)
        {
            cbWants[i] = cbDist(prng);
            (void)VMMDLL_Scatter_Prepare(hS, pages[i].PageBase, cbWants[i]);
            
            // Occasional safepoint during prepare loop
            if ((i % 100) == 0)
            {
                SimulateGCSafepoint(prng);
            }
        }

        // Safepoint before Execute - THIS IS WHERE .NET CRASHES
        SimulateGCSafepoint(prng);

        // STEP 2: Execute
        (void)VMMDLL_Scatter_Execute(hS);

        // Safepoint after Execute
        SimulateGCSafepoint(prng);

        // STEP 3: Read results after execute
        for (size_t i = 0; i < pages.size(); i++)
        {
            DWORD readSize = min(cbWants[i], 0x1000);
            std::vector<uint8_t> buffer(readSize);
            DWORD cbRead = 0;
            (void)VMMDLL_Scatter_Read(hS, pages[i].PageBase, readSize, buffer.data(), &cbRead);
            if (cbRead)
            {
                volatile uint8_t sink = buffer[0];
                (void)sink;
            }
            
            // Occasional safepoint during read loop
            if ((i % 100) == 0)
            {
                SimulateGCSafepoint(prng);
            }
        }

        VMMDLL_Scatter_CloseHandle(hS);
    }

    // Long-running worker (like .NET's LongWorker)
    void LongWorker(VmmSession& vmm)
    {
        g_totalWorkers++;
        while (true)
        {
            try
            {
                DoReads(vmm);
            }
            catch (const std::exception& ex)
            {
                std::cerr << "Exception in LongWorker: " << ex.what() << std::endl;
            }
            catch (...)
            {
                std::cerr << "Unknown exception in LongWorker!" << std::endl;
                g_crashCount++;
            }
        }
    }

    // Transient worker that spawns/dies like .NET's TransientWorker
    void TransientWorker(VmmSession& vmm);
    
    void SpawnTransientWorker(VmmSession& vmm)
    {
        std::thread([&vmm] { TransientWorker(vmm); }).detach();
    }

    void TransientWorker(VmmSession& vmm)
    {
        g_totalWorkers++;
        
        std::random_device rd;
        std::mt19937_64 prng(rd());
        std::uniform_int_distribution<int> lifetimeDist(2000, 18000);  // 2-18 seconds like .NET
        
        auto startTime = std::chrono::steady_clock::now();
        auto lifetime = std::chrono::milliseconds(lifetimeDist(prng));
        
        while (true)
        {
            auto now = std::chrono::steady_clock::now();
            if (now - startTime > lifetime)
            {
                break;  // Time to die and respawn
            }
            
            try
            {
                DoReads(vmm);
            }
            catch (const std::exception& ex)
            {
                std::cerr << "Exception in TransientWorker: " << ex.what() << std::endl;
            }
            catch (...)
            {
                std::cerr << "Unknown exception in TransientWorker!" << std::endl;
                g_crashCount++;
            }
        }
        
        g_totalWorkers--;
        
        // Respawn like .NET does
        SpawnTransientWorker(vmm);
    }

    // GC simulation thread - periodically triggers "stop the world" like events
    void GCSimulatorThread()
    {
        std::random_device rd;
        std::mt19937_64 prng(rd());
        std::uniform_int_distribution<int> intervalDist(50, 500);  // 50-500ms between "GCs"
        std::uniform_int_distribution<int> pauseDist(1, 10);       // 1-10ms pause
        
        while (true)
        {
            Sleep(intervalDist(prng));
            
            // Trigger a "GC pause"
            g_simulateGCPause.store(true, std::memory_order_release);
            Sleep(pauseDist(prng));
            g_simulateGCPause.store(false, std::memory_order_release);
        }
    }

    // Memory pressure thread - allocates/frees memory rapidly like .NET GC would cause
    void MemoryPressureThread()
    {
        std::random_device rd;
        std::mt19937_64 prng(rd());
        std::uniform_int_distribution<size_t> sizeDist(1024, 1024 * 1024);  // 1KB - 1MB
        
        while (true)
        {
            size_t size = sizeDist(prng);
            void* p = malloc(size);
            if (p)
            {
                memset(p, 0xAA, size);  // Touch the memory
                free(p);
            }
            SwitchToThread();
        }
    }
}

int main()
{
    try
    {
        std::cout << "Starting up Issue #14 Native STRESS TEST..." << std::endl;
        std::cout << "Simulating .NET-like behavior (GC pauses, thread churn, memory pressure)" << std::endl;

        std::vector<std::string> args{
            "-device",
            "fpga",
            "-waitinitialize",
            "-printf",
            "-v",
        };

        VmmSession vmm(args);

        // Start GC simulator thread
        std::thread(GCSimulatorThread).detach();
        std::cout << "Started GC simulator thread" << std::endl;

        // Start memory pressure threads
        for (int i = 0; i < 4; i++)
        {
            std::thread(MemoryPressureThread).detach();
        }
        std::cout << "Started 4 memory pressure threads" << std::endl;

        // Start long workers (like .NET's 8 LongWorkers)
        for (int i = 0; i < 8; i++)
        {
            std::thread([&vmm] { LongWorker(vmm); }).detach();
            std::cout << "Started LongWorker " << (i + 1) << std::endl;
        }

        // Start transient workers (like .NET's 8 TransientWorkers that respawn)
        for (int i = 0; i < 8; i++)
        {
            SpawnTransientWorker(vmm);
            std::cout << "Started TransientWorker " << (i + 1) << std::endl;
        }

        std::cout << "\nRunning stress test... Press Ctrl+C to stop." << std::endl;
        std::cout << "Watching for crashes..." << std::endl;

        // Monitor loop
        while (true)
        {
            Sleep(5000);
            std::cout << "[Status] Active workers: " << g_totalWorkers.load() 
                      << ", Crash count: " << g_crashCount.load() << std::endl;
        }

        return 0;
    }
    catch (const std::exception& ex)
    {
        std::cerr << "*** Unhandled Exception: " << ex.what() << std::endl;
        std::cerr << "Press any key to exit." << std::endl;
        (void)getchar();
        return 1;
    }
}
