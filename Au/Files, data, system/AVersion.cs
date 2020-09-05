using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
//using System.Linq;

using Au.Types;

namespace Au
{
	/// <summary>
	/// Provides various version info, for example the true Windows OS version.
	/// </summary>
	/// <remarks>
	/// The Windows version properties return true Windows version. If you need version that depends on manifest and debugger, instead use <see cref="Environment.OSVersion"/>.
	/// </remarks>
	[DebuggerStepThrough]
	public static unsafe class AVersion
	{
		static AVersion() {
			Api.RTL_OSVERSIONINFOW x = default; x.dwOSVersionInfoSize = Api.SizeOf(x);
			if (0 == Api.RtlGetVersion(ref x)) {
				_winver = AMath.MakeWord(x.dwMinorVersion, x.dwMajorVersion);
				_winbuild = (int)x.dwBuildNumber;
				//use this because Environment.OSVersion.Version (GetVersionEx) lies, even if we have correct manifest when is debugger present
			} else {
				Debug.Fail("RtlGetVersion");
				var v = Environment.OSVersion.Version;
				_winver = AMath.MakeWord(v.Minor, v.Major);
				_winbuild = v.Build;
			}

			_minWin8 = _winver >= Win8;
			_minWin8_1 = _winver >= Win8_1;
			_minWin10 = _winver >= Win10;
			if (_minWin10) _win10build = _winbuild;

			_is32BitOS = sizeof(IntPtr) == 4 && !(Api.IsWow64Process(Api.GetCurrentProcess(), out _isWow64) && _isWow64);
		}

		static readonly int _winver, _winbuild, _win10build;
		static readonly bool _minWin8, _minWin8_1, _minWin10;
		static readonly bool _is32BitOS, _isWow64;

		/// <summary>
		/// Gets classic Windows major+minor version value:
		/// Win7 (0x601), Win8 (0x602), Win8_1 (0x603), Win10 (0xA00).
		/// Example: <c>if(AVersion.WinVer >= AVersion.Win8) ...</c>
		/// </summary>
		public static int WinVer => _winver;

		/// <summary>
		/// Gets Windows build number.
		/// For example 14393 for Windows 10 version 1607.
		/// </summary>
		public static int WinBuild => _winbuild;

		/// <summary>
		/// Classic Windows version major+minor values that can be used with <see cref="WinVer"/>.
		/// Example: <c>if(AVersion.WinVer >= AVersion.Win8) ...</c>
		/// </summary>
		public const int Win7 = 0x601, Win8 = 0x602, Win8_1 = 0x603, Win10 = 0xA00;

		/// <summary>
		/// true if Windows 8.0 or later.
		/// </summary>
		public static bool MinWin8 => _minWin8;

		/// <summary>
		/// true if Windows 8.1 or later.
		/// </summary>
		public static bool MinWin8_1 => _minWin8_1;

		/// <summary>
		/// true if Windows 10 or later.
		/// </summary>
		public static bool MinWin10 => _minWin10;

		/// <summary>
		/// true if Windows 10 version 1607 or later.
		/// </summary>
		public static bool MinWin10_1607 => _win10build >= 14393;

		/// <summary>
		/// true if Windows 10 version 1703 or later.
		/// </summary>
		public static bool MinWin10_1703 => _win10build >= 15063;

		/// <summary>
		/// true if Windows 10 version 1709 or later.
		/// </summary>
		public static bool MinWin10_1709 => _win10build >= 16299;

		/// <summary>
		/// true if Windows 10 version 1803 or later.
		/// </summary>
		public static bool MinWin10_1803 => _win10build >= 17134;

		/// <summary>
		/// true if Windows 10 version 1809 or later.
		/// </summary>
		public static bool MinWin10_1809 => _win10build >= 17763;

		/// <summary>
		/// true if Windows 10 version 1903 or later.
		/// </summary>
		public static bool MinWin10_1903 => _win10build >= 18362;

		/// <summary>
		/// true if Windows 10 version 1909 or later.
		/// </summary>
		public static bool MinWin10_1909 => _win10build >= 18363;

		/// <summary>
		/// true if Windows 10 version 2004 or later.
		/// </summary>
		public static bool MinWin10_2004 => _win10build >= 19041;

		/// <summary>
		/// true if this process is 32-bit, false if 64-bit.
		/// The same as <c>sizeof(IntPtr) == 4</c>.
		/// </summary>
		public static bool Is32BitProcess => sizeof(IntPtr) == 4;

		/// <summary>
		/// true if Windows is 32-bit, false if 64-bit.
		/// </summary>
		public static bool Is32BitOS => _is32BitOS;

		/// <summary>
		/// Returns true if this process is a 32-bit process running on 64-bit Windows. Also known as WOW64 process.
		/// </summary>
		public static bool Is32BitProcessAnd64BitOS => _isWow64;
	}
}
