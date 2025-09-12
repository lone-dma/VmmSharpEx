// Dummy shell program to act as a target for unit tests
// Run on target system, and is connected to via DMA

#include <iostream>
#include <Windows.h>

LPVOID pvBuffer = nullptr; // 0x40A0

int main()
{
    pvBuffer = VirtualAlloc(NULL, 1024ull * 1024 * 1024, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (!pvBuffer) {
        std::cerr << "VirtualAlloc failed: " << GetLastError() << std::endl;
        return 1;
	}
    const wchar_t msg[] = L"hello :)";
    memcpy(pvBuffer, msg, sizeof(msg));
    const char* pBuffer = static_cast<char*>(pvBuffer);
    if (!pBuffer) {
        std::cerr << "static_cast failed" << std::endl;
        return 1;
    }
    std::cout << "Ready. Close this window when testing has completed." << std::endl;
    for (;;) {
        for (SIZE_T i = 0; i < 1024ull * 1024 * 1024; i += 4096) {
            volatile char hi = pBuffer[i]; // force a read from each page
		}
        Sleep(100);
    }
    return 0;
}