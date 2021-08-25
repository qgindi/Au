
using System.Xml;

namespace Au.More
{
	/// <summary>
	/// Base class for tree classes.
	/// The tree can be loaded/saved as XML.
	/// </summary>
	/// <remarks>
	/// Implemented in the same way as <see cref="System.Xml.Linq.XContainer"/>.
	/// </remarks>
	/// <example>
	/// Shows how to declare a <b>TreeBase</b>-derived class, load tree of nodes from an XML file, find descendant nodes, save the tree to an XML file.
	/// <code><![CDATA[
	/// using System.Xml;
	/// 
	/// class MyTree : Au.More.TreeBase<MyTree>
	/// {
	/// 	public string Name { get; set; }
	/// 	public int Id { get; private set; }
	/// 	public bool IsFolder { get; private set; }
	/// 
	/// 	public MyTree(string name, int id, bool isFolder) { Name = name; Id = id; IsFolder = isFolder; }
	/// 
	/// 	//XML element -> MyTree object
	/// 	MyTree(XmlReader x, MyTree parent)
	/// 	{
	/// 		if(parent == null) { //the root XML element
	/// 			if(x.Name != "example") throw new ArgumentException("XML root element must be 'example'");
	/// 			IsFolder = true;
	/// 		} else {
	/// 			switch(x.Name) {
	/// 			case "e": break;
	/// 			case "f": IsFolder = true; break;
	/// 			default: throw new ArgumentException("XML element must be 'e' or 'f'");
	/// 			}
	/// #if true //two ways of reading attributes
	/// 			Name = x["name"];
	/// 			Id = x["id"].ToInt();
	/// #else
	/// 			while(x.MoveToNextAttribute()) {
	/// 				var v = x.Value;
	/// 				switch(x.Name) {
	/// 				case "name": Name = v; break;
	/// 				case "id": Id = v.ToInt(); break;
	/// 				}
	/// 			}
	/// #endif
	/// 			if(Name.NE()) throw new ArgumentException("no 'name' attribute in XML");
	/// 			if(Id == 0) throw new ArgumentException("no 'id' attribute in XML");
	/// 		}
	/// 	}
	/// 
	/// 	public static MyTree Load(string file) => XmlLoad(file, (x, p) => new MyTree(x, p));
	/// 
	/// 	public void Save(string file) => XmlSave(file, (x, n) => n._XmlWrite(x));
	/// 
	/// 	//MyTree object -> XML element
	/// 	void _XmlWrite(XmlWriter x)
	/// 	{
	/// 		if(Parent == null) {
	/// 			x.WriteStartElement("example");
	/// 		} else {
	/// 			x.WriteStartElement(IsFolder ? "f" : "e");
	/// 			x.WriteAttributeString("name", Name);
	/// 			x.WriteAttributeString("id", Id.ToString());
	/// 		}
	/// 	}
	/// 
	/// 	public override string ToString() => $"{new string(' ', Level)}{(IsFolder ? 'f' : 'e')} {Name} ({Id})";
	/// }
	/// 
	/// static void TNodeExample()
	/// {
	/// 	/*
	/// 	<example>
	/// 	  <e name="one" id="1" />
	/// 	  <f name="two" id="112">
	/// 		<e name="three" id="113" />
	/// 		<e name="four" id="114" />
	/// 		<f name="five" id="120">
	/// 		  <e name="six" id="121" />
	/// 		  <e name="seven" id="122" />
	/// 		</f>
	/// 	  </f>
	/// 	  <f name="eight" id="217" />
	/// 	  <e name="ten" id="144" />
	/// 	</example>
	/// 	*/
	/// 
	/// 	var x = MyTree.Load(@"Q:\test\example.xml");
	/// 	foreach(MyTree n in x.Descendants(true)) print.it(n);
	/// 	//print.it(x.Descendants().FirstOrDefault(k => k.Name == "seven")); //find a descendant
	/// 	//print.it(x.Descendants().Where(k => k.Level > 2)); //find some descendants
	/// 	x.Save(@"Q:\test\example2.xml");
	/// }
	/// ]]></code>
	/// </example>
	public abstract class TreeBase<T> where T : TreeBase<T>
	{
		T _next;
		T _parent;
		T _lastChild;

		#region properties

		/// <summary>
		/// Returns the parent node. Can be null.
		/// </summary>
		public T Parent => _parent;

		/// <summary>
		/// Returns the root ancestor node. Its <see cref="Parent"/> is null.
		/// Returns this node if its <b>Parent</b> is null.
		/// </summary>
		public T RootAncestor {
			get {
				var p = this as T;
				while (p._parent != null) p = p._parent;
				return p;
			}
		}

		/// <summary>
		/// Gets the number of ancestors (parent, its parent and so on).
		/// </summary>
		public int Level {
			get {
				int R = 0;
				for (var p = _parent; p != null; p = p._parent) R++;
				return R;
			}
		}

		/// <summary>
		/// Returns true if this node is a descendant of node n.
		/// </summary>
		/// <param name="n">Can be null.</param>
		public bool IsDescendantOf(T n) {
			for (var p = _parent; p != null; p = p._parent) if (p == n) return true;
			return false;
		}

		/// <summary>
		/// Returns true if this node is an ancestor of node n.
		/// </summary>
		/// <param name="n">Can be null.</param>
		public bool IsAncestorOf(T n) => n?.IsDescendantOf(this as T) ?? false;

		/// <summary>
		/// Returns true if <see cref="Parent"/> is not null.
		/// </summary>
		public bool HasParent => _parent != null;

		/// <summary>
		/// Returns true if this node has child nodes.
		/// </summary>
		public bool HasChildren => _lastChild != null;

		/// <summary>
		/// Gets the last child node, or null if none.
		/// </summary>
		public T LastChild => _lastChild;

		/// <summary>
		/// Gets the first child node, or null if none.
		/// </summary>
		public T FirstChild => _lastChild?._next;

		/// <summary>
		/// Gets next sibling node, or null if none.
		/// </summary>
		public T Next => _parent == null || this == _parent._lastChild ? null : _next;

		/// <summary>
		/// Gets previous sibling node, or null if none.
		/// </summary>
		/// <remarks>
		/// Can be slow if there are many siblings. This class does not have a 'previous' field and therefore has to walk the linked list of siblings.
		/// </remarks>
		public T Previous {
			get {
				if (_parent == null) return null;
				T n = _parent._lastChild._next;
				Debug.Assert(n != null);
				T p = null;
				while (n != this) {
					p = n;
					n = n._next;
				}
				return p;
			}
		}

		/// <summary>
		/// Returns 0-based index of this node in parent.
		/// Returns -1 if no parent.
		/// </summary>
		/// <remarks>
		/// Can be slow if there are many siblings. This class does not have an 'index' field and therefore has to walk the linked list of siblings.
		/// </remarks>
		public int Index {
			get {
				var p = _parent;
				if (p != null) {
					var n = p._lastChild;
					for (int i = 0; ; i++) {
						n = n._next;
						if (n == this) return i;
					}
				}
				return -1;
			}
		}

		#endregion

		#region methods

		void _AddCommon(T n) {
			if (n == null || n._parent != null || n == RootAncestor) throw new ArgumentException();
			n._parent = this as T;
		}

		/// <summary>
		/// Adds node n to this node as a child.
		/// </summary>
		/// <param name="n"></param>
		/// <param name="first">Insert n as the first child node. If false (default), appends to the end.</param>
		/// <exception cref="ArgumentException">n is null, or has parent (need to <see cref="Remove"/> at first), or is this node, or an ancestor of this node.</exception>
		public void AddChild(T n, bool first = false) {
			_AddCommon(n);
			if (_lastChild == null) { //our first child!
				n._next = n; //n now is LastChild and FirstChild
			} else {
				n._next = _lastChild._next; //_next of _lastChild is FirstChild
				_lastChild._next = n;
				if (first) return;
			}
			_lastChild = n;
		}

		/// <summary>
		/// Inserts node n before or after this node as a sibling.
		/// </summary>
		/// <param name="n"></param>
		/// <param name="after">Insert n after this node. If false (default), inserts before this node.</param>
		/// <exception cref="ArgumentException">See <see cref="AddChild"/>.</exception>
		/// <exception cref="InvalidOperationException">This node does not have parent (<see cref="Parent"/> is null).</exception>
		public void AddSibling(T n, bool after) {
			if (_parent == null) throw new InvalidOperationException("no parent");
			_parent._Insert(n, this as T, after);
		}

		void _Insert(T n, T anchor, bool after) {
			if (after && anchor == _lastChild) { //after last child
				AddChild(n);
			} else if (!after && anchor == _lastChild._next) { //before first child
				AddChild(n, true);
			} else {
				_AddCommon(n);
				T prev, next;
				if (after) { prev = anchor; next = anchor._next; } else { prev = anchor.Previous; next = anchor; }
				n._next = next;
				prev._next = n;
			}
		}

		/// <summary>
		/// Removes this node from its parent.
		/// </summary>
		/// <remarks>
		/// After removing, the <see cref="Parent"/> property is null.
		/// Does nothing if <b>Parent</b> is null.
		/// </remarks>
		public void Remove() => _parent?._Remove(this as T);

		void _Remove(T n) {
			Debug.Assert(n?._parent == this);

			T p = _lastChild;
			while (p._next != n) p = p._next;
			if (p == n) {
				_lastChild = null;
			} else {
				if (_lastChild == n) _lastChild = p;
				p._next = n._next;
			}
			n._parent = null;
			n._next = null;
		}

		/// <summary>
		/// Gets ancestor nodes (parent, its parent and so on).
		/// The order is from <see cref="Parent"/> to <see cref="RootAncestor"/>.
		/// </summary>
		/// <param name="andSelf">Include this node.</param>
		/// <param name="noRoot">Don't include <see cref="RootAncestor"/>.</param>
		public IEnumerable<T> Ancestors(bool andSelf = false, bool noRoot = false) {
			var n = andSelf ? this as T : _parent;
			while (n != null) {
				if (noRoot && n._parent == null) break;
				yield return n;
				n = n._parent;
			}
		}

		/// <summary>
		/// Gets ancestor nodes in reverse order than <see cref="Ancestors"/>.
		/// </summary>
		/// <param name="andSelf">Include this node. Default false.</param>
		/// <param name="noRoot">Don't include <see cref="RootAncestor"/>.</param>
		public T[] AncestorsReverse(bool andSelf = false, bool noRoot = false) {
			T nFrom = andSelf ? this as T : _parent;
			//count
			int len = 0;
			for (var n = nFrom; n != null; n = n._parent) {
				if (noRoot && n._parent == null) break;
				len++;
			}
			//array
			if (len == 0) return Array.Empty<T>();
			var a = new T[len];
			for (var n = nFrom; len > 0; n = n._parent) a[--len] = n;
			return a;

			//info: can use LINQ Reverse, but this func makes less garbage.
		}

		/// <summary>
		/// Gets all direct child nodes.
		/// </summary>
		/// <param name="andSelf">Include this node. Default false.</param>
		public IEnumerable<T> Children(bool andSelf = false) {
			if (andSelf) yield return this as T;
			if (_lastChild != null) {
				var n = _lastChild;
				do {
					n = n._next;
					yield return n;
				} while (n != _lastChild);
			}
		}

		/// <summary>
		/// Gets number of direct child nodes.
		/// </summary>
		public int Count {
			get {
				int r = 0;
				if (_lastChild != null) {
					var n = _lastChild;
					do { r++; } while ((n = n._next) != _lastChild);
				}
				return r;
			}
		}

		/// <summary>
		/// Gets all descendant nodes (direct children, their children and so on).
		/// </summary>
		/// <param name="andSelf">Include this node. Default false.</param>
		public IEnumerable<T> Descendants(bool andSelf = false) {
			var n = this as T;
			if (andSelf) yield return n;
			while (true) {
				T last = n._lastChild;
				if (last != null) {
					n = last._next;
				} else {
					while (n != null && n != this && n == n._parent._lastChild) n = n._parent;
					if (n == null || n == this) break;
					n = n._next;
				}
				yield return n;
			}
		}

		#endregion

		#region XML

		/// <summary>
		/// Used with <see cref="XmlLoad"/>
		/// </summary>
		protected delegate T XmlNodeReader(XmlReader x, T parent);

		/// <summary>
		/// Used with <see cref="XmlSave"/>
		/// </summary>
		protected delegate void XmlNodeWriter(XmlWriter x, T node);

		/// <summary>
		/// Loads XML file and creates tree of nodes from it.
		/// Returns the root node.
		/// </summary>
		/// <param name="file">XML file. Must be full path. Can contain environment variables etc, see <see cref="pathname.expand"/>.</param>
		/// <param name="nodeReader">Callback function that reads current XML element and creates/returns new node. See example.</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="XmlReader.Create(string)"/>.</exception>
		/// <exception cref="XmlException">An error occurred while parsing the XML.</exception>
		/// <example><see cref="TreeBase{T}"/></example>
		protected static T XmlLoad(string file, XmlNodeReader nodeReader) {
			file = pathname.NormalizeForNET_(file);
			var xs = new XmlReaderSettings() { IgnoreComments = true, IgnoreProcessingInstructions = true, IgnoreWhitespace = true };
			using var r = filesystem.waitIfLocked(() => XmlReader.Create(file, xs));
			return XmlLoad(r, nodeReader);
		}

		/// <summary>
		/// Reads XML and creates tree of nodes.
		/// Returns the root node.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="nodeReader"></param>
		/// <exception cref="XmlException">An error occurred while parsing the XML.</exception>
		/// <remarks>More info: <see cref="XmlLoad(string, XmlNodeReader)"/>.</remarks>
		/// <example><see cref="TreeBase{T}"/></example>
		protected static T XmlLoad(XmlReader x, XmlNodeReader nodeReader) {
			if (x == null || nodeReader == null) throw new ArgumentNullException();
			T root = null, parent = null;
			while (x.Read()) {
				var nodeType = x.NodeType;
				if (nodeType == XmlNodeType.Element) {
					var n = nodeReader(x, parent);
					if (root == null) root = n;
					else parent.AddChild(n);
					x.MoveToElement();
					if (!x.IsEmptyElement) parent = n;
				} else if (nodeType == XmlNodeType.EndElement) {
					if (parent == null) break;
					if (parent == root) break;
					parent = parent._parent;
				}
			}
			return root;
		}

		/// <summary>
		/// Saves tree of nodes (this and descendants) to an XML file.
		/// </summary>
		/// <param name="file">XML file. Must be full path. Can contain environment variables etc, see <see cref="pathname.expand"/>.</param>
		/// <param name="nodeWriter">Callback function that writes node's XML start element (see <see cref="XmlWriter.WriteStartElement(string)"/>) and attributes (see <see cref="XmlWriter.WriteAttributeString(string, string)"/>). Must not write children and end element. Also should not write value, unless your reader knows how to read it.</param>
		/// <param name="sett">XML formatting settings. Optional.</param>
		/// <param name="children">If not null, writes these nodes as if they were children of this node.</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="XmlWriter.Create(string)"/> and other <b>XmlWriter</b> methods.</exception>
		/// <remarks>
		/// Uses <see cref="filesystem.save"/>. It ensures that existing file data is not damaged on exception etc.
		/// </remarks>
		/// <example><see cref="TreeBase{T}"/></example>
		protected void XmlSave(string file, XmlNodeWriter nodeWriter, XmlWriterSettings sett = null, IEnumerable<T> children = null) {
			file = pathname.NormalizeForNET_(file);
			sett ??= new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = true, IndentChars = "  " };
			filesystem.save(file, temp => {
				using var x = XmlWriter.Create(temp, sett);
				XmlSave(x, nodeWriter, children);
			});
		}

		/// <summary>
		/// Writes tree of nodes (this and descendants) to an <see cref="XmlWriter"/>.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="nodeWriter"></param>
		/// <param name="children"></param>
		/// <remarks>More info: <see cref="XmlSave(string, XmlNodeWriter, XmlWriterSettings, IEnumerable{T})"/>.</remarks>
		/// <exception cref="Exception">Exceptions of <b>XmlWriter</b> methods.</exception>
		/// <example><see cref="TreeBase{T}"/></example>
		protected void XmlSave(XmlWriter x, XmlNodeWriter nodeWriter, IEnumerable<T> children = null) {
			if (x == null || nodeWriter == null) throw new ArgumentNullException();
			x.WriteStartDocument();
			if (children == null) {
				_XmlWrite(x, nodeWriter);
			} else {
				nodeWriter(x, this as T);
				foreach (var n in children) n._XmlWrite(x, nodeWriter);
				x.WriteEndElement();
			}
			x.WriteEndDocument();
		}

		void _XmlWrite(XmlWriter x, XmlNodeWriter nodeWriter) {
			nodeWriter(x, this as T);
			if (_lastChild != null) {
				var c = _lastChild;
				do {
					c = c._next;
					c._XmlWrite(x, nodeWriter);
				} while (c != _lastChild);
			}
			x.WriteEndElement();
		}

		#endregion
	}
}
