/*
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

// Dummy shell program to act as a target for unit tests
// Run on target system, and is connected to via DMA

#include <iostream>
#include <Windows.h>

LPVOID pvBuffer = nullptr; // 0x40A0

int main()
{
	const SIZE_T cbBuffer = 17ull << 12; // Size within default working set
    pvBuffer = VirtualAlloc(NULL, cbBuffer, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (!pvBuffer) {
        std::cerr << "VirtualAlloc failed with error: " << GetLastError() << std::endl;
		return 1;
    }
    if (!VirtualLock(pvBuffer, cbBuffer)) {
		// Lock the buffer into physical memory
		std::cerr << "VirtualLock failed with error: " << GetLastError() << std::endl;
        return 1;
    }
    const wchar_t msg[] = L"hello :)";
    memcpy(pvBuffer, msg, sizeof(msg));
    std::cout << "Ready. Close this window when testing has completed." << std::endl;
	Sleep(-1); // Sleep indefinitely
    return 0;
}