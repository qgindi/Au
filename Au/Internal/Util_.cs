using System.Windows;

namespace Au.More;

static unsafe class Not_ {
	//internal static void NullCheck<T>(this T t, string paramName = null) where T : class {
	//	if (t is null) throw new ArgumentNullException(paramName);
	//}
	
	/// <summary>
	/// Same as <b>ArgumentNullException.ThrowIfNull</b>.
	/// It's pitty, they removed operator !! from C# 11.
	/// </summary>
	internal static void Null(object o,
		[CallerArgumentExpression("o")] string paramName = null) {
		if (o is null) throw new ArgumentNullException(paramName);
	}
	internal static void Null(object o1, object o2,
		[CallerArgumentExpression("o1")] string paramName1 = null,
		[CallerArgumentExpression("o2")] string paramName2 = null) {
		if (o1 is null) throw new ArgumentNullException(paramName1);
		if (o2 is null) throw new ArgumentNullException(paramName2);
	}
	internal static void Null(object o1, object o2, object o3,
		[CallerArgumentExpression("o1")] string paramName1 = null,
		[CallerArgumentExpression("o2")] string paramName2 = null,
		[CallerArgumentExpression("o3")] string paramName3 = null) {
		if (o1 is null) throw new ArgumentNullException(paramName1);
		if (o2 is null) throw new ArgumentNullException(paramName2);
		if (o3 is null) throw new ArgumentNullException(paramName3);
	}
	internal static void Null(object o1, object o2, object o3, object o4,
		[CallerArgumentExpression("o1")] string paramName1 = null,
		[CallerArgumentExpression("o2")] string paramName2 = null,
		[CallerArgumentExpression("o3")] string paramName3 = null,
		[CallerArgumentExpression("o4")] string paramName4 = null) {
		if (o1 is null) throw new ArgumentNullException(paramName1);
		if (o2 is null) throw new ArgumentNullException(paramName2);
		if (o3 is null) throw new ArgumentNullException(paramName3);
		if (o4 is null) throw new ArgumentNullException(paramName4);
	}
	internal static void Null(void* o,
		[CallerArgumentExpression("o")] string paramName = null) {
		if (o is null) throw new ArgumentNullException(paramName);
	}
	internal static T NullRet<T>(T o,
		[CallerArgumentExpression("o")] string paramName = null) where T : class {
		if (o is null) throw new ArgumentNullException(paramName);
		return o;
	}
}

static class WpfUtil_ {
	/// <summary>
	/// Parses icon string like "[*&lt;library&gt;]*pack.name[ etc]".
	/// </summary>
	/// <returns>true if starts with "*pack.name" (possibly with library).</returns>
	public static bool DetectIconString(RStr s, out (int pack, int endPack, int name, int endName) r) {
		r = default;
		if (s.Length < 8 || s[0] != '*') return false;
		int pack = 1;
		if (s[1] == '<') { // *<library>*icon
			pack = s.IndexOf('>');
			if (pack++ < 0 || s.Length - pack < 8 || s[pack] != '*') return false;
			pack++;
		}
		int name = s.IndexOf(pack, '.'); if (name < pack + 4) return false; //shortest names currently are like "Modern.At", but in the future may add a pack with a shorter name.
		int end = ++name; while (end < s.Length && s[end] != ' ') end++;
		if (end == name) return false;
		r = (pack, name - 1, name, end);
		return true;
	}
	
	/// <summary>
	/// Parses icon string like "[*&lt;library&gt;]*pack.name[ color][ @size]".
	/// </summary>
	/// <returns>true if starts with "*pack.name" (possibly with library).</returns>
	public static bool ParseIconString(string s, out (string pack, string name, string color, int size) r) {
		r = default;
		if (!DetectIconString(s, out var d)) return false;
		r.pack = s[d.pack..d.endPack];
		r.name = s[d.name..d.endName];
		if (d.endName < s.Length) {
			Span<Range> a = stackalloc Range[3];
			var span = s.AsSpan(d.endName + 1);
			int n = span.Split(a, ' ', StringSplitOptions.RemoveEmptyEntries);
			for (int k = 0; k < n; k++) {
				int start = a[k].Start.Value, end = a[k].End.Value;
				char c = span[start];
				if (c == '@') r.size = span[++start..end].ToInt_(STIFlags.DontSkipSpaces);
				else if (c == '#' || char.IsAsciiLetter(c)) r.color ??= span[a[k]].ToString();
			}
		}
		return true;
	}
	
	/// <summary>
	/// Eg "*pack.name color" -> "*pack.name".
	/// Supports library prefix, @size, no-color.
	/// </summary>
	public static string RemoveColorFromIconString(string s) {
		int i = 0;
		if (s.Starts("*<")) { i = s.IndexOf('>'); if (i++ < 0) return s; }
		return s.RxReplace(@" ++[^@]\S+", "", 1, range: i..);
	}
	
	/// <summary>
	/// From icon string like "*name color|color2" or "color|color2" removes "|color2" or "color|" depending on high contrast.
	/// Does nothing if s does not contain '|'.
	/// </summary>
	/// <param name="s"></param>
	/// <param name="onlyColor">true - s is like "color|color2". false - s is like "*name color|color2".</param>
	public static string NormalizeIconStringColor(string s, bool onlyColor) {
		int i = s.IndexOf('|');
		if (i >= 0) {
			bool dark = IsHighContrastDark;
			if (onlyColor) {
				s = dark ? s[++i..] : s[..i];
			} else {
				int j = i;
				if (dark) {
					for (j++; i > 0 && s[i - 1] != ' ';) i--;
				} else {
					while (j < s.Length && s[j] != ' ') j++;
				}
				s = s.Remove(i..j);
			}
		}
		return s;
	}
	
	public static bool SetColorInXaml(ref string xaml, string color) {
		if (color.NE()) color = SystemColors.ControlTextColor.ToString();
		else color = NormalizeIconStringColor(color, true); //color can be "normal|highContrast"
		
		s_rxColor ??= new(@"(?:Fill|Stroke)=""\K[^""]+");
		return 0 != s_rxColor.Replace(xaml, color, out xaml, 1);
	}
	
	static regexp s_rxColor;
	
	/// <summary>
	/// true if SystemParameters.HighContrast and ColorInt.GetPerceivedBrightness(SystemColors.ControlColor)&lt;=0.5.
	/// </summary>
	public static bool IsHighContrastDark {
		get {
			if (!SystemParameters.HighContrast) return false; //fast, cached
			var col = (ColorInt)SystemColors.ControlColor; //fast, cached
			var v = ColorInt.GetPerceivedBrightness(col.argb, false);
			return v <= .5;
		}
	}
}

#if !true
namespace Au
{
/// <summary>
/// 
/// </summary>
public struct TestDoc
{
	/// <summary>
	/// Sum One.
	/// </summary>
	/// <param name="i">Iiii.</param>
	/// <returns>Retto.</returns>
	/// <exception cref="ArgumentException">i is invalid.</exception>
	/// <exception cref="AuException">Failed.</exception>
	/// <remarks>Remmo.</remarks>
	/// <example>
	/// <code>TestDoc.One(1);</code>
	/// </example>
	public static int One(int i) {
		print.it(i);
		return 0;
	}

	/// <inheritdoc cref="One(int)"/>
	public static int One(int i, string s) {
		print.it(i, s);
		return 0;
	}

	/// <param name="b">Boo.</param>
	/// <exception cref="NotFoundException">Not found.</exception>
	/// <inheritdoc cref="One"/>
	public static int One(bool b, int i) {
		print.it(i, b);
		return 0;
	}

	/// <param name="b">Boo no except.</param>
	/// <inheritdoc cref="One"/>
	public static int One(string b, int i) {
		print.it(i, b);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <inheritdoc cref="One" path="/param"/>
	/// <param name="s">Last.</param>
	public static int One(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}
	///// <inheritdoc cref="One" path="/param[@name='i']"/>

	/// <summary>
	/// Sum Two.
	/// </summary>
	/// <param name="b">Boo.</param>
	/// <exception cref="TimeoutException">Timeout.</exception>
	/// <inheritdoc cref="One"/>
	public static int Two(double b, int i) {
		print.it(i, b);
		return 0;
	}

	/// <summary>
	/// Sum Two 2.
	/// </summary>
	/// <exception cref="TimeoutException">Timeout.</exception>
	/// <inheritdoc cref="One"/>
	public static int TwoNoParam(int i) {
		print.it(i);
		return 0;
	}

	/// <summary>
	/// Sum Two 2.
	/// </summary>
	/// <param name="b">Boo.</param>
	/// <inheritdoc cref="One"/>
	public static int TwoNoExcept(double b, int i) {
		print.it(i, b);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <inheritdoc cref="One" path="/param"/>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <inheritdoc cref="One(int)" path="/param"/>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara2(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <inheritdoc cref="One" path="/param[@name='i']"/>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara3(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <inheritdoc cref="One(int)" path="/param[@name='i']"/>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara4(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <param name="i"><inheritdoc cref="One"/></param>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara5(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <param name="i"><inheritdoc cref="One" path="/param"/></param>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara6(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <param name="i"><inheritdoc cref="One" path="/param[@name='i']"/></param>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara7(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <param name="i"><inheritdoc cref="One(int)" path="/param[@name='i']"/></param>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara8(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}
}
}
#endif
