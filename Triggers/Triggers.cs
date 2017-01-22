﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Win32;
using System.Runtime.ExceptionServices;
//using System.Windows.Forms;
//using System.Drawing;
//using System.Linq;

using Catkeys;
using static Catkeys.NoClass;

namespace Catkeys.Triggers
{
public static class Trigger
{
	public static readonly HotkeyTriggers Hotkey=new HotkeyTriggers();

	[AttributeUsage(AttributeTargets.Method)]
	public class HotkeyAttribute : Attribute
	{
		public readonly string hotkey;

		public HotkeyAttribute(string hotkey)
		{
			this.hotkey=hotkey;
		}
	}

}
}
