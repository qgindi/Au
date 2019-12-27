﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
//using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Au;
using Au.Types;
using static Au.AStatic;
using Au.Triggers;
using Au.Controls;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Runtime;
using Microsoft.Win32;
using System.Runtime.InteropServices.ComTypes;
using System.Numerics;
using System.Globalization;
//using AutoItX3Lib;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Windows.Forms.VisualStyles;

using TheArtOfDev.HtmlRenderer.WinForms;
using TheArtOfDev.HtmlRenderer.Core.Entities;

[module: DefaultCharSet(CharSet.Unicode)]

class Script : AScript
{

	class TestGC
	{
		~TestGC()
		{
			if(Environment.HasShutdownStarted) return;
			if(AppDomain.CurrentDomain.IsFinalizingForUnload()) return;
			Print("GC", GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
			//ATimer.After(1, _ => new TestGC());
			//var f = Program.MainForm; if(!f.IsHandleCreated) return;
			//f.BeginInvoke(new Action(() => new TestGC()));
			new TestGC();
		}
	}
	static bool s_debug2;

	void _MonitorGC()
	{
		//return;
		if(!s_debug2) {
			s_debug2 = true;
			new TestGC();

			//ATimer.Every(50, _ => {
			//	if(!s_debug) {
			//		s_debug = true;
			//		ATimer.After(100, _ => new TestGC());
			//	}
			//});
		}
	}

	//unsafe class MapArray
	//{
	//	public int[] _a;
	//	public Vector128<int>[] _v;

	//	public MapArray(int n)
	//	{
	//		_a = new int[n];
	//		for(int i = 0; i < _a.Length; i++) _a[i] = i;

	//		_v = new Vector128<int>[n];
	//	}

	//	public void Move(int i)
	//	{
	//		int n = _a.Length - i - 1;
	//		Array.Copy(_a, i, _a, i + 1, n);

	//		//fixed(int* p = _a) Api.memmove(p + i + 1, p + 1, n * 4); //same speed
	//	}

	//	[MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
	//	public void Inc(int i, int add)
	//	{
	//		//for(; i < _a.Length; i++) _a[i]+=add;

	//		long add2 = add; add2 = add2 << 32 | add2;
	//		fixed(int* ip = _a) {
	//			var p = (long*)ip;
	//			for(int n = _a.Length / 2; i < n; i++) {
	//				//var v = p[i];
	//				p[i] += add2;
	//			}

	//		}

	//		//var va = Vector128.Create(add);
	//		//for(;  i < _v.Length; i++) {
	//		//	_v[i]=Sse2.Add(_v[i], va);
	//		//}
	//	}

	//	//public void Insert(int i, int add)
	//	//{
	//	//	for(; i < _a.Length; i++) _a[i]+=add;
	//	//}

	//	public void PrintVector()
	//	{
	//		Print(_a);

	//		//for(int i=0; i < _v.Length; i++) {
	//		//	Print(_v[i].GetElement(0), _v[i].GetElement(1), _v[i].GetElement(2), _v[i].GetElement(3));
	//		//}
	//	}
	//}

	class JSettings
	{
		public string OneTwo { get; set; }
		public int ThreeFour { get; set; }
		public int Five { get; set; }
		public bool Six { get; set; }
		public string Seven { get; set; }
		public string Eight { get; set; } = "def";
	}

	void TestJson()
	{
		var file = @"Q:\test\sett.json";
		var file2 = @"Q:\test\sett.xml";

		var v = new JSettings { OneTwo = "text ąčę", ThreeFour = 100 };

		for(int i = 0; i < 5; i++) {
			//100.ms();
			//APerf.First();
			//var k1 = new JsonSerializerOptions { IgnoreNullValues = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true };
			//var b1 = JsonSerializer.SerializeToUtf8Bytes(v, k1);
			//APerf.Next();
			//File.WriteAllBytes(file, b1);
			//APerf.NW();

			100.ms();
			APerf.First();
			var b2 = File.ReadAllBytes(file);
			APerf.Next();
			var k2 = new JsonSerializerOptions { IgnoreNullValues = true };
			APerf.Next();
			v = JsonSerializer.Deserialize<JSettings>(b2, k2);
			APerf.NW('J');
		}

		for(int i = 0; i < 5; i++) {
			//100.ms();
			//APerf.First();
			//var r1 = new XElement("r");
			//r1.Add(new XElement("OneTwo", v.OneTwo));
			//r1.Add(new XElement("ThreeFour", v.ThreeFour.ToString()));
			//APerf.Next();
			//r1.Save(file2);
			//APerf.NW();

			100.ms();
			APerf.First();
			var r2 = XElement.Load(file2);
			APerf.Next();
			v = new JSettings();
			v.OneTwo = r2.Element("OneTwo").Value;
			var s2 = r2.Element("ThreeFour").Value;
			APerf.NW('X');
			v.ThreeFour = s2.ToInt();
		}

		Print(v.OneTwo, v.ThreeFour, v.Five, v.Six, v.Seven, v.Eight);

		//JsonDocument d; d.RootElement.
	}

	[DllImport("CppE")]
	static extern int Cpp_Install(int step, string dir);

	[DllImport("CppE")]
	static extern int Cpp_Uninstall();


	void TestMenu()
	{
		var m = new AMenu();
		m["One"] = o => Print(o);
		m["Two"] = o => Print(o);
		m.Submenu("Submenu 1", _ => {
			Print("adding items of " + m.CurrentAddMenu.OwnerItem);
			m["Three"] = o => Print(o);
			m["Four"] = o => Print(o);
			m.Submenu("Submenu 2", _ => {
				Print("adding items of " + m.CurrentAddMenu.OwnerItem);
				m["Five"] = o => Print(o);
				m["Six"] = o => Print(o);
			});
			m["Seven"] = o => Print(o);
		});
		m["Eight"] = o => Print(o);
		m.Show();

	}

	//void TestMenu2()
	//{
	//	var m = new AMenu();
	//	m["One"] = o => Print(o);
	//	m["Two"] = o => Print(o);
	//	m.LazySubmenu("Submenu 1").Fill = _ => {
	//		Print("adding items of " + m.CurrentAddMenu.OwnerItem);
	//		m["Three"] = o => Print(o);
	//		m["Four"] = o => Print(o);
	//		m.Submenu("Submenu 2", _ => {
	//			Print("adding items of " + m.CurrentAddMenu.OwnerItem);
	//			m["Five"] = o => Print(o);
	//			m["Six"] = o => Print(o);
	//		});
	//		m["Seven"] = o => Print(o);
	//	};
	//	m["Eight"] = o => Print(o);
	//	m.Show();

//}

//void TestMenu2()
//{
//	var m = new AMenu();
//	m["One"] = o => Print(o);
//	m["Two"] = o => Print(o);
//	m.LazySubmenu("Submenu 1");
//	m.LazyFill = _ => {
//		Print("adding items of " + m.CurrentAddMenu.OwnerItem);
//		m["Three"] = o => Print(o);
//		m["Four"] = o => Print(o);
//		m.Submenu("Submenu 2", _ => {
//			Print("adding items of " + m.CurrentAddMenu.OwnerItem);
//			m["Five"] = o => Print(o);
//			m["Six"] = o => Print(o);
//		});
//		m["Seven"] = o => Print(o);
//	};
//	m["Eight"] = o => Print(o);
//	m.Show();

//}

#if false
	void TestToolbar()
	{
		for(int i = 0; i < 1; i++) {
			var t = new AToolbar("123");
			//t.NoText = true;
			//t.Border= TBBorder.Sizable3;t.Control.Text = "Toolbar";
			//t.Border = TBBorder.SizableWithCaptionX;

			//t["Find", @"Q:\app\find.ico"] = o => Print(o);
			//t["Copy", @"Q:\app\copy.ico"] = o => Print(o);
			//t.Separator("Tpi group");
			//t["Delete", @"Q:\app\delete.ico"] = o => Print(o);
			//t["No image"] = o => Print(o);
			//t["TT", tooltip: "WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW "] = o => Print(o);
			////t.LastButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
			////t.LastButton.AutoToolTip = false;
			////t.LastButton.ToolTipText = "ggg";
			//t.Separator();
			//t["Run", @"Q:\app\run.ico"] = o => Print(o);
			//t.Separator("");
			//t["Paste text", @"Q:\app\paste.ico"] = o => Print(o);
			//t.LastButton.ToolTipText = "Toooooltip";

			//t.ExtractIconPathFromCode = true;
			//t["Auto icon"] = o => Print("notepad.exe");
			//t["Failed icon", @"Q:\app\-.ico"] = o => Print(o);
			////t.LastButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
			////t.Separator("");
			////t.Add(new ToolStripTextBox { ToolTipText= "ToolStripTextBox", AutoSize=false, Width=50 });
			////t.Add(new ToolStripComboBox { ToolTipText= "ToolStripComboBox", AutoSize=false, Width=50 });
			////t.Add(new ToolStripTextBox());
			////t.Add(new ToolStripTextBox());
			////t.Add(new ToolStripTextBox());
			////t.Add(new ToolStripButton("aaa"));
			////t.Add(new ToolStripButton("bbb"));
			////t["Multi\r\nline"] = o => Print(o);

			//t["None"] = o => _B(TBBorder.None);
			//t["SWC"] = o => _B(TBBorder.SizableWithCaption);
			//t["Sizable1"] = o => _B(TBBorder.Sizable1);
			//t["Sizable2"] = o => _B(TBBorder.Sizable2);
			//t["Sizable3"] = o => _B(TBBorder.Sizable3);
			//t["Sizable3D"] = o => _B(TBBorder.Sizable3D);
			//t["Sizable"] = o => _B(TBBorder.Sizable);
			//t["FixedWithCaption"] = o => _B(TBBorder.FixedWithCaption);
			//t["SizableWithCaption"] = o => _B(TBBorder.SizableWithCaption);
			//t["Close"] = o => t.Close();

#if false
			var dd = new ToolStripDropDownButton("DD");
			t.Add(dd, @"Q:\app\find.ico");
			dd.DropDownOpening += (unu, sed) => {
				var m = new AMenu(dd);
				m["one"] = o => Print(o);
				using(m.Submenu("Sub")) {
					m["si"] = o => Print(o);
				}
			};
			var sb = new ToolStripSplitButton("SB");
			t.Add(sb, @"Q:\app\copy.ico", o => Print(o));
#elif true
			//t.Control.Font = new Font("Courier New", 16);
			//t.Control.RightToLeft = RightToLeft.Yes;
			t.MenuButton("DD", m => {
				Print("dd");
				//m.MultiShow = false;
				m["one"] = o => Print(o);
				using(m.Submenu("Sub")) {
					m["si"] = o => Print(o);
				}
			}, @"Q:\app\find.ico", "MenuButton");
			t.SplitButton("SB", m => {
				m["one"] = o => Print(o);
				//var sb = m.Control.OwnerItem as ToolStripSplitButton;
				//Print(sb);
				//sb.DefaultItem = m.LastItem;
				using(m.Submenu("Sub")) {
					m["si"] = o => Print(o);
				}
			}, @"Q:\app\copy.ico", "SplitButton", o => Print(o));
			t.Separator("");
			t[true, "DD2", @"Q:\app\delete.ico"] = m => {
				Print("create menu");
				//m.MultiShow = false;
				m["one"] = o => Print(o);
				using(m.Submenu("Sub")) {
					m["si"] = o => Print(o);
				}
			};
			//t.SplitButton("SB", o => {
			//	Print(o);
			//}, m => {
			//	m["one"] = o => Print(o);
			//	using(m.Submenu("Sub")) {
			//		m["si"] = o => Print(o);
			//	}
			//}, @"Q:\app\copy.ico", "SplitButton");
			//Action<AMenu> menu1 = m => {
			//	m["one"] = o => Print(o);
			//	using(m.Submenu("Sub")) {
			//		m["si"] = o => Print(o);
			//	}
			//};
			//t.MenuButton("DD", menu1, @"Q:\app\find.ico", "MenuButton");
#elif false
			t.MenuButton("DD", @"Q:\app\find.ico");
			t.Menu = m => {
				m["one"] = o => Print(o);
				using(m.Submenu("Sub")) {
					m["si"] = o => Print(o);
				}
			};
#else
			t.MenuButton("DD", @"Q:\app\find.ico").Menu = m => {
				Print("dd");
				//m.MultiShow = false;
				m["one"] = o => Print(o);
				using(m.Submenu("Sub")) {
					m["two"] = o => Print(o);
				}
			};
			t.SplitButton("SB", o => Print(o), @"Q:\app\copy.ico").Menu = m => {
				Print("dd");
				m["one"] = o => Print(o);
				using(m.Submenu("Sub")) {
					m["two"] = o => Print(o);
				}
			};
#endif
			//t.Separator("");
			////t["GC"] = o => GC.Collect();

			//var dd = new ToolStripSplitButton("SB2", null, (unu,sed)=>Print("click"));
			//t.Add(dd, @"Q:\app\delete.ico");
			//dd.DropDownOpening += (unu, sed) => {
			//	var m = new AMenu();
			//	dd.DropDown = m.Control;
			//	m["one"] = o => Print(o);
			//};
			//dd.ButtonClick += (unu, sed) => Print("button click");
			//dd.DoubleClickEnabled = true;
			//dd.ButtonDoubleClick += (unu, sed) => Print("button double click");

			//ATimer.After(3000, _ => {
			//	var c = t.Control.Items[0];
			//	c.Select();
			//});

			//void _B(TBBorder b){
			//	t.Border = b;
			//	//Print(AWnd.More.BorderWidth((AWnd)t.Control));
			//}

			//t.Bounds = new Rectangle(i * 300 + 700, 200, 200, 200);
			t.Show();
			//t.Window.ActivateLL();
			ATime.SleepDoEvents(200);

			//for(int j = 1; j <= (int)TBBorder.SizableWithCaptionX; j++) {
			//	ATime.SleepDoEvents(1000);
			//	t.Border = (TBBorder)j;
			//}

			//ATime.SleepDoEvents(1000);
			//t.Border = TBBorder.FixedWithCaption;
			//ATime.SleepDoEvents(3000);
			//t.Border = TBBorder.SizableWithCaption;

			//var m = new AMenu();
			//using(m.Submenu("Sub")) {

			//}
			//m.Show()
		}

		//var c = new System.Windows.Forms.VisualStyles.VisualStyleRenderer(VisualStyleElement.Window.FrameLeft.Inactive).GetColor(ColorProperty.BorderColor);
		//Print((uint)c.ToArgb());

		//ATimer.After(500, _ => {
		//	var w = (AWnd)t.Control;
		//	//w.SetStyle(WS.DLGFRAME, SetAddRemove.Add);
		//});

		ADialog.Options.TopmostIfNoOwnerWindow = true;
		ADialog.Show();

		//ATimer.After(10000, _ => Application.Exit());
		//Application.Run();
	}
#endif
	[MethodImpl(MethodImplOptions.NoInlining)]
	void TestCallerArgumentExpression(string so, [CallerArgumentExpression("so")] string ca = null) //does not work
	{
		Print(so, ca);
	}

	unsafe void _Main()
	{
		Application.SetCompatibleTextRenderingDefault(false);

		//TestToolbar();
		//TestMenu();
		//TestCallerArgumentExpression("FF" + 5); var v = "gg"; TestCallerArgumentExpression(v);
	}

	[STAThread] static void Main(string[] args) { new Script(args); }
	Script(string[] args)
	{
		AOutput.QM2.UseQM2 = true;
		AOutput.Clear();
		Au.Util.LibAssertListener.Setup();

		//APerf.First();
		try {
			_Main();
		}
		catch(Exception ex) { Print(ex); }
	}
}
