//TODO: test on ARM64, maybe process hangs when calling Cpp_Arch too.
// Because, when calling Cpp_Unload, hangs for up to 1 minute (x64) and creates 2-3 processes.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#if 1 //small exe file. Use .c file. Project properties > Linker > Advanced > Entry point = main.
#define EXPORT __declspec(dllimport)
EXPORT void Cpp_Arch(LPCWSTR a0, LPCWSTR a1);
EXPORT void Cpp_Unload(DWORD flags);

void main() {
	LPCWSTR a = GetCommandLine();
	if (*a == '"') {
		while (*(++a) != '"') if (*a == 0) return;
		a++;
	} else {
		while (*a != ' ' && *a != 0) a++;
	}
	while (*a == ' ') a++;
	//MessageBox(0, a, L"test", 0);

	if (*a == 0) {
		Cpp_Unload(1);
	} else {
		LPCWSTR a1 = a; while (*a1 != 0 && *a1 != ' ') a1++;
		if (a1 > a && *a1 == ' ') a1++; else return;
		Cpp_Arch(a, a1);
	}

	//MessageBox(0, a0, L"test", 0);
}
#else
#define EXPORT extern "C" __declspec(dllimport)
EXPORT void Cpp_Arch(LPCWSTR a0, LPCWSTR a1);
EXPORT void Cpp_Unload(DWORD flags);

int APIENTRY wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPWSTR pCmdLine, int nCmdShow) {

	if (*pCmdLine == 0) {
		Cpp_Unload(1);
	} else {
		LPCWSTR a1 = pCmdLine; while (*a1 != 0 && *a1 != ' ') a1++;
		if (a1 > pCmdLine && *a1 == ' ') a1++; else return 1;
		Cpp_Arch(pCmdLine, a1);
	}

	MessageBox(0, pCmdLine, L"test", 0);

	return 0;
}
#endif
