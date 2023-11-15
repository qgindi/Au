using Au.Controls;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Completion;

class CiComplItem : ITreeViewItem {
	CompletionItem _ci;
	public readonly CiItemKind kind;
	public readonly CiItemAccess access;
	readonly CiComplProvider _provider;
	public CiComplItemHiddenBy hidden;
	public CiComplItemMoveDownBy moveDown;
	public ulong hilite; //bits for max 64 characters
	public int group;
	public int commentOffset;
	string _dtext;
	object _symbols; //ISymbol or List<ISymbol> or IReadonlyList<ISymbol> or null
	
	public CompletionItem ci {
		get => _ci;
		set {
			_ci = value;
			_ci.Attach = this;
		}
	}
	
	public CiComplItem(CiComplProvider provider, CompletionItem ci) {
		_provider = provider;
		_symbols = ci.Symbols;
		this.ci = ci;
		CiUtil.TagsToKindAndAccess(ci.Tags, out kind, out access);
		//ci.DebugPrint();
	}
	
	//not used
	//public CiComplItem(CiComplProvider provider, TextSpan span, ISymbol sym, CiItemKind kind, string name = null) {
	//	_provider = provider;
	//	_symbols = sym;
	//	this.kind = kind;
	//	bool gen = sym switch { INamedTypeSymbol nt => nt.IsGenericType, IMethodSymbol ms => ms.IsGenericMethod, _ => false };
	//	ci = CompletionItem.Create(name ?? sym.JustName(), displayTextSuffix: gen ? "<>" : null);
	//}
	
	public CiComplItem(CiComplProvider provider, TextSpan span, string name, CiItemKind kind, CiItemAccess access = default) {
		_provider = provider;
		this.kind = kind;
		this.access = access;
		ci = CompletionItem.Create(name);
		_ci.Span = span;
	}
	
	internal void AddOverload(ISymbol sym) {
		switch (_symbols) {
		case ISymbol s:
			_symbols = new List<ISymbol> { s, sym };
			break;
		case List<ISymbol> a:
			a.Add(sym);
			break;
		default:
			Debug.Assert(false);
			break;
		}
	}
	
	public IEnumerable<ISymbol> Symbols {
		get {
			if (_symbols is ISymbol sym) _symbols = new ISymbol[1] { sym };
			return _symbols as IEnumerable<ISymbol>;
		}
	}
	
	public ISymbol FirstSymbol => _symbols switch { ISymbol sym => sym, IEnumerable<ISymbol> en => en.FirstOrDefault(), _ => null };
	
	/// <summary>
	/// Gets displayed text without prefix, suffix (eg generic) and green comments (group or inline description).
	/// In most cases it is simple name, but in some cases can be eg "Namespace.Name", "Name(parameters)", etc.
	/// </summary>
	public string Text => _ci.DisplayText;
	
	public CiComplProvider Provider => _provider;
	
	#region ITreeViewItem
	string ITreeViewItem.DisplayText => _dtext;
	
	object ITreeViewItem.Image => ImageResource(kind);
	
	#endregion
	
	public void SetDisplayText(string comment) {
		var desc = _ci.InlineDescription; if (desc.NE()) desc = comment;
		bool isComment = !desc.NE();
		if (_dtext != null && !isComment && commentOffset == 0) return;
		_dtext = this.Text + _ci.DisplayTextSuffix + (isComment ? "    //" : null) + desc;
		if (!_ci.DisplayTextPrefix.NE()) {
			_dtext = _ci.DisplayTextPrefix + _dtext;
			Debug_.PrintIf(_ci.DisplayTextPrefix != "(", $"{_dtext}, {_ci.DisplayTextPrefix}"); //seen only of casts, eg "(" + "int" + ")"
		}
		commentOffset = isComment ? _dtext.Length - desc.Length - 6 : 0;
	}
	
	public static string ImageResource(CiItemKind kind) => kind switch {
		CiItemKind.Class => "resources/ci/class.xaml",
		CiItemKind.Constant => "resources/ci/constant.xaml",
		CiItemKind.Delegate => "resources/ci/delegate.xaml",
		CiItemKind.Enum => "resources/ci/enum.xaml",
		CiItemKind.EnumMember => "resources/ci/enummember.xaml",
		CiItemKind.Event => "resources/ci/event.xaml",
		CiItemKind.ExtensionMethod => "resources/ci/extensionmethod.xaml",
		CiItemKind.Field => "resources/ci/field.xaml",
		CiItemKind.Interface => "resources/ci/interface.xaml",
		CiItemKind.Keyword => "resources/ci/keyword.xaml",
		CiItemKind.Label => "resources/ci/label.xaml",
		CiItemKind.LocalVariable => "resources/ci/localvariable.xaml",
		CiItemKind.Method => "resources/ci/method.xaml",
		CiItemKind.Namespace => "resources/ci/namespace.xaml",
		CiItemKind.Operator => "resources/ci/operator.xaml",
		CiItemKind.Property => "resources/ci/property.xaml",
		CiItemKind.Snippet => "resources/ci/snippet.xaml",
		CiItemKind.Structure => "resources/ci/structure.xaml",
		CiItemKind.TypeParameter => "resources/ci/typeparameter.xaml",
		CiItemKind.LocalMethod => "resources/ci/localmethod.xaml",
		CiItemKind.Region => "resources/ci/region.xaml",
		_ => null
	};
	
	public string AccessImageSource => AccessImageResource(access);
	
	public static string AccessImageResource(CiItemAccess access) => access switch {
		CiItemAccess.Private => "resources/ci/overlayprivate.xaml",
		CiItemAccess.Protected => "resources/ci/overlayprotected.xaml",
		CiItemAccess.Internal => "resources/ci/overlayinternal.xaml",
		_ => null
	};
	
	public string ModifierImageSource => _ModifierImageResource(this);
	
	static string _ModifierImageResource(CiComplItem ci) {
		var sym = ci.FirstSymbol;
		if (sym != null) {
			if (sym.IsStatic && ci.kind is not (CiItemKind.Constant or CiItemKind.EnumMember or CiItemKind.Namespace)) return "resources/ci/overlaystatic.xaml";
			if (ci.kind == CiItemKind.Class && sym.IsAbstract) return "resources/ci/overlayabstract.xaml";
		} else {
			//if (ci.Provider == CiComplProvider.Winapi && ci.kind == CiItemKind.Method) return "resources/ci/overlaystatic.xaml"; //no
		}
		return null;
	}
}

enum CiComplProvider : byte {
	Other,
	Symbol,
	Keyword,
	Cref,
	XmlDoc,
	EmbeddedLanguage, //Regex, DateTime format, maybe more
	Override,
	//ExternAlias,
	//ObjectAndWithInitializer,
	//AttributeNamedParameter,
	
	//ours
	Snippet,
	Winapi,
}

enum CiComplResult {
	/// <summary>
	/// No completion.
	/// </summary>
	None,
	
	/// <summary>
	/// Inserted text displayed in the popup list. Now caret is after it.
	/// </summary>
	Simple,
	
	/// <summary>
	/// Inserted more text than displayed in the popup list, eg "(" or "{  }" or override. Now caret probably is somewhere in middle of it. Also if regex.
	/// Only if ch == ' ', '\n' (Enter) or default (Tab).
	/// </summary>
	Complex,
}

[Flags]
enum CiComplItemHiddenBy : byte { FilterText = 1, Kind = 2, Always = 4 }

[Flags]
enum CiComplItemMoveDownBy : sbyte { Name = 1, Obsolete = 2, FilterText = 4 }

//The order must match CiUtil.ItemKindNames. In this order are displayed group buttons in the completion popup. See also code in CiWinapi.cs: " WHERE kind<=4".
enum CiItemKind : sbyte {
	//types
	Class, Structure, Interface, Enum, Delegate,
	//functions, events
	Method, ExtensionMethod, Property, Operator, Event,
	//data
	Field, LocalVariable, Constant, EnumMember,
	//other
	Namespace, Keyword, Label, Snippet, TypeParameter,
	//not in autocomplete. Not in CiUtil.ItemKindNames.
	LocalMethod, Region,
	None
}

//don't reorder!
enum CiItemAccess : sbyte { Public, Private, Protected, Internal }

class CiNamespaceSymbolEqualityComparer : IEqualityComparer<INamespaceSymbol> {
	public bool Equals(INamespaceSymbol x, INamespaceSymbol y) {
		for (; ; ) {
			if (x.MetadataName != y.MetadataName) return false;
			x = x.ContainingNamespace;
			y = y.ContainingNamespace;
			if (x == null) return y == null;
			if (y == null) return false;
		}
	}
	
	public int GetHashCode(INamespaceSymbol obj) {
		for (int r = obj.MetadataName.GetHashCode(); ;) {
			obj = obj.ContainingNamespace;
			if (obj == null) return r;
			r ^= obj.MetadataName.GetHashCode();
		}
	}
}

class CiNamespaceOrTypeSymbolEqualityComparer : IEqualityComparer<INamespaceOrTypeSymbol> {
	public bool Equals(INamespaceOrTypeSymbol x, INamespaceOrTypeSymbol y) {
		for (; ; ) {
			if (x.MetadataName != y.MetadataName) return false;
			if ((x is INamespaceSymbol) != (y is INamespaceSymbol)) return false;
			x = x.ContainingSymbol as INamespaceOrTypeSymbol;
			y = y.ContainingSymbol as INamespaceOrTypeSymbol;
			if (x == null) return y == null;
			if (y == null) return false;
		}
	}
	
	public int GetHashCode(INamespaceOrTypeSymbol obj) {
		for (int r = obj.MetadataName.GetHashCode(); ;) {
			obj = obj.ContainingSymbol as INamespaceOrTypeSymbol;
			if (obj == null) return r;
			r ^= obj.MetadataName.GetHashCode();
		}
	}
}

struct CiStringRange {
	public readonly string code;
	public readonly int start, end;
	public readonly bool verbatim;
	
	public CiStringRange(string code, int start, int end, bool verbatim) {
		this.code = code; this.start = start; this.end = end; this.verbatim = verbatim;
	}
	
	public override string ToString() {
		var s = code[start..end];
		if (!verbatim) s.Unescape(out s);
		return s;
	}
	
	public int Length => end - start;
}
