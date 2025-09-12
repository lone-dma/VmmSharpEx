#include <iostream>
#include <Windows.h>

LPVOID pvBuffer = nullptr; // 0x40A0

int main()
{
    std::cout << "Close this window when testing is completed." << std::endl;
    pvBuffer = VirtualAlloc(NULL, 1024ull * 1024 * 1024, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (!pvBuffer) {
        std::cerr << "VirtualAlloc failed: " << GetLastError() << std::endl;
        return 1;
	}
    volatile char* pCharBuffer = static_cast<volatile char*>(pvBuffer);
    if (!pCharBuffer) {
        std::cerr << "static_cast failed" << std::endl;
        return 1;
    }
    for (;;) {
        for (SIZE_T i = 0; i < 1024ull * 1024 * 1024; i += 4096) {
            volatile char tmp = pCharBuffer[i]; // force a read from each page
		}
        Sleep(1);
    }
    return 0;
}