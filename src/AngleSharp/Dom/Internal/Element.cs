namespace AngleSharp.Dom
{
    using AngleSharp.Common;
    using AngleSharp.Css.Parser;
    using AngleSharp.Dom.Events;
    using AngleSharp.Text;
    using System;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Represents an element node.
    /// </summary>
    public abstract class Element : Node, IElement
    {
        #region Fields

        private static readonly AttachedProperty<Element, IShadowRoot> ShadowRootProperty = new AttachedProperty<Element, IShadowRoot>();

        private readonly NamedNodeMap _attributes;
        private readonly String _namespace;
        private readonly String _prefix;
        private readonly String _localName;

        private HtmlCollection<IElement> _elements;
        private TokenList _classList;

        #endregion

        #region ctor

        /// <inheritdoc />
        public Element(Document owner, String localName, String prefix, String namespaceUri, NodeFlags flags = NodeFlags.None)
            : this(owner, prefix != null ? String.Concat(prefix, ":", localName) : localName, localName, prefix, namespaceUri, flags)
        {
        }

        /// <inheritdoc />
        public Element(Document owner, String name, String localName, String prefix, String namespaceUri, NodeFlags flags = NodeFlags.None)
            : base(owner, name, NodeType.Element, flags)
        {
            _localName = localName;
            _prefix = prefix;
            _namespace = namespaceUri;
            _attributes = new NamedNodeMap(this);
        }

        #endregion

        #region Internal Properties

        internal IBrowsingContext Context => Owner?.Context;

        internal NamedNodeMap Attributes => _attributes;

        #endregion

        #region Properties

        /// <inheritdoc />
        public IElement AssignedSlot
        {
            get { return ParentElement?.ShadowRoot?.GetAssignedSlot(Slot); }
        }

        /// <inheritdoc />
        public String Slot
        {
            get { return this.GetOwnAttribute(AttributeNames.Slot); }
            set { this.SetOwnAttribute(AttributeNames.Slot, value); }
        }

        /// <inheritdoc />
        public IShadowRoot ShadowRoot
        {
            get { return ShadowRootProperty.Get(this); }
        }

        /// <inheritdoc />
        public String Prefix
        {
            get { return _prefix; }
        }

        /// <inheritdoc />
        public String LocalName
        {
            get { return _localName; }
        }

        /// <inheritdoc />
        public String NamespaceUri
        {
            get { return _namespace ?? this.GetNamespaceUri(); }
        }

        /// <inheritdoc />
        public override String TextContent
        {
            get
            {
                var sb = StringBuilderPool.Obtain();

                foreach (var child in this.GetDescendants().OfType<IText>())
                {
                    sb.Append(child.Data);
                }

                return sb.ToPool();
            }
            set
            {
                var node = !String.IsNullOrEmpty(value) ? new TextNode(Owner, value) : null;
                ReplaceAll(node, false);
            }
        }

        /// <inheritdoc />
        public ITokenList ClassList
        {
            get
            {
                if (_classList == null)
                {
                    _classList = new TokenList(this.GetOwnAttribute(AttributeNames.Class));
                    _classList.Changed += value => UpdateAttribute(AttributeNames.Class, value);
                }

                return _classList;
            }
        }

        /// <inheritdoc />
        public String ClassName
        {
            get { return this.GetOwnAttribute(AttributeNames.Class); }
            set { this.SetOwnAttribute(AttributeNames.Class, value); }
        }

        /// <inheritdoc />
        public String Id
        {
            get { return this.GetOwnAttribute(AttributeNames.Id); }
            set { this.SetOwnAttribute(AttributeNames.Id, value); }
        }

        /// <inheritdoc />
        public String TagName
        {
            get { return NodeName; }
        }

        /// <inheritdoc />
        public IElement PreviousElementSibling
        {
            get
            {
                var parent = Parent;

                if (parent != null)
                {
                    var found = false;

                    for (var i = parent.ChildNodes.Length - 1; i >= 0; i--)
                    {
                        if (Object.ReferenceEquals(parent.ChildNodes[i], this))
                        {
                            found = true;
                        }
                        else if (found && parent.ChildNodes[i] is IElement)
                        {
                            return (IElement)parent.ChildNodes[i];
                        }
                    }
                }

                return null;
            }
        }

        /// <inheritdoc />
        public IElement NextElementSibling
        {
            get
            {
                var parent = Parent;

                if (parent != null)
                {
                    var n = parent.ChildNodes.Length;
                    var found = false;

                    for (var i = 0; i < n; i++)
                    {
                        if (Object.ReferenceEquals(parent.ChildNodes[i], this))
                        {
                            found = true;
                        }
                        else if (found && parent.ChildNodes[i] is IElement)
                        {
                            return (IElement)parent.ChildNodes[i];
                        }
                    }
                }

                return null;
            }
        }

        /// <inheritdoc />
        public Int32 ChildElementCount
        {
            get
            {
                var children = ChildNodes;
                var n = children.Length;
                var count = 0;

                for (var i = 0; i < n; i++)
                {
                    if (children[i].NodeType == NodeType.Element)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <inheritdoc />
        public IHtmlCollection<IElement> Children
        {
            get { return _elements ?? (_elements = new HtmlCollection<IElement>(this, deep: false)); }
        }

        /// <inheritdoc />
        public IElement FirstElementChild
        {
            get
            {
                var children = ChildNodes;
                var n = children.Length;

                for (var i = 0; i < n; i++)
                {
                    var child = children[i] as IElement;

                    if (child != null)
                    {
                        return child;
                    }
                }

                return null;
            }
        }

        /// <inheritdoc />
        public IElement LastElementChild
        {
            get
            {
                var children = ChildNodes;

                for (int i = children.Length - 1; i >= 0; i--)
                {
                    var child = children[i] as IElement;

                    if (child != null)
                    {
                        return child;
                    }
                }

                return null;
            }
        }

        /// <inheritdoc />
        public String InnerHtml
        {
            get { return ChildNodes.ToHtml(); }
            set { ReplaceAll(new DocumentFragment(this, value), false); }
        }

        /// <inheritdoc />
        public String OuterHtml
        {
            get { return this.ToHtml(); }
            set
            {
                var parentNode = Parent;

                if (parentNode != null)
                {
                    switch (parentNode.NodeType)
                    {
                        case NodeType.Document:
                            throw new DomException(DomError.NoModificationAllowed);
                        case NodeType.DocumentFragment:
                            parentNode = new Html.Dom.HtmlBodyElement(Owner);
                            break;
                    }
                }

                var parent = parentNode as Element ?? throw new DomException(DomError.NotSupported);
                parent.InsertChild(parent.IndexOf(this), new DocumentFragment(parent, value));
                parent.RemoveChild(this);
            }
        }

        INamedNodeMap IElement.Attributes
        {
            get { return _attributes; }
        }

        /// <inheritdoc />
        public Boolean IsFocused
        {
            get { return Object.ReferenceEquals(Owner?.FocusElement, this); }
            protected set
            {
                var document = Owner;

                if (document != null)
                {
                    if (value)
                    {
                        document.SetFocus(this);
                        this.Fire<FocusEvent>(m => m.Init(EventNames.Focus, false, false));
                    }
                    else
                    {
                        document.SetFocus(null);
                        this.Fire<FocusEvent>(m => m.Init(EventNames.Blur, false, false));
                    }
                }
            }
        }

        #endregion

        #region Methods

        /// <inheritdoc />
        public IShadowRoot AttachShadow(ShadowRootMode mode = ShadowRootMode.Open)
        {
            if (TagNames.AllNoShadowRoot.Contains(_localName))
                throw new DomException(DomError.NotSupported);

            if (ShadowRoot != null)
                throw new DomException(DomError.InvalidState);

            var root = new ShadowRoot(this, mode);
            ShadowRootProperty.Set(this, root);
            return root;
        }

        /// <inheritdoc />
        public IElement QuerySelector(String selectors)
        {
            return ChildNodes.QuerySelector(selectors, this);
        }

        /// <inheritdoc />
        public IHtmlCollection<IElement> QuerySelectorAll(String selectors)
        {
            return ChildNodes.QuerySelectorAll(selectors, this);
        }

        /// <inheritdoc />
        public IHtmlCollection<IElement> GetElementsByClassName(String classNames)
        {
            return ChildNodes.GetElementsByClassName(classNames);
        }

        /// <inheritdoc />
        public IHtmlCollection<IElement> GetElementsByTagName(String tagName)
        {
            return ChildNodes.GetElementsByTagName(tagName);
        }

        /// <inheritdoc />
        public IHtmlCollection<IElement> GetElementsByTagNameNS(String namespaceURI, String tagName)
        {
            return ChildNodes.GetElementsByTagName(namespaceURI, tagName);
        }

        /// <inheritdoc />
        public Boolean Matches(String selectorText)
        {
            var parser = Context.GetService<ICssSelectorParser>();
            var sg = parser.ParseSelector(selectorText);

            if (sg == null)
                throw new DomException(DomError.Syntax);

            return sg.Match(this, this);
        }

        /// <inheritdoc />
        public IElement Closest(String selectorText)
        {
            var parser = Context.GetService<ICssSelectorParser>();
            var sg = parser.ParseSelector(selectorText) ?? throw new DomException(DomError.Syntax);

            IElement node = this;

            while (node != null)
            {
                if (sg.Match(node, node))
                {
                    return node;
                }
                else
                {
                    node = node.ParentElement;
                }
            }
            return null;
        }

        /// <inheritdoc />
        public Boolean HasAttribute(String name)
        {
            if (_namespace.Is(NamespaceNames.HtmlUri))
            {
                name = name.HtmlLower();
            }

            return _attributes.GetNamedItem(name) != null;
        }

        /// <inheritdoc />
        public Boolean HasAttribute(String namespaceUri, String localName)
        {
            if (String.IsNullOrEmpty(namespaceUri))
            {
                namespaceUri = null;
            }

            return _attributes.GetNamedItem(namespaceUri, localName) != null;
        }

        /// <inheritdoc />
        public String GetAttribute(String name)
        {
            if (_namespace.Is(NamespaceNames.HtmlUri))
            {
                name = name.HtmlLower();
            }

            return _attributes.GetNamedItem(name)?.Value;
        }

        /// <inheritdoc />
        public String GetAttribute(String namespaceUri, String localName)
        {
            if (String.IsNullOrEmpty(namespaceUri))
            {
                namespaceUri = null;
            }

            return _attributes.GetNamedItem(namespaceUri, localName)?.Value;
        }

        /// <inheritdoc />
        public void SetAttribute(String name, String value)
        {
            if (value != null)
            {
                if (!name.IsXmlName())
                    throw new DomException(DomError.InvalidCharacter);

                if (_namespace.Is(NamespaceNames.HtmlUri))
                {
                    name = name.HtmlLower();
                }

                this.SetOwnAttribute(name, value);
            }
            else
            {
                RemoveAttribute(name);
            }
        }

        /// <inheritdoc />
        public void SetAttribute(String namespaceUri, String name, String value)
        {
            if (value != null)
            {
                GetPrefixAndLocalName(name, ref namespaceUri, out var prefix, out var localName);
                _attributes.SetNamedItem(new Attr(prefix, localName, value, namespaceUri));
            }
            else
            {
                RemoveAttribute(namespaceUri, name);
            }
        }

        /// <summary>
        /// Adds an attribute.
        /// </summary>
        /// <param name="attr">The attribute to add.</param>
        public void AddAttribute(Attr attr)
        {
            _attributes.FastAddItem(attr);
        }

        /// <inheritdoc />
        public Boolean RemoveAttribute(String name)
        {
            if (_namespace.Is(NamespaceNames.HtmlUri))
            {
                name = name.HtmlLower();
            }

            return _attributes.RemoveNamedItemOrDefault(name) != null;
        }

        /// <inheritdoc />
        public Boolean RemoveAttribute(String namespaceUri, String localName)
        {
            if (String.IsNullOrEmpty(namespaceUri))
            {
                namespaceUri = null;
            }

            return _attributes.RemoveNamedItemOrDefault(namespaceUri, localName) != null;
        }

        /// <inheritdoc />
        public void Prepend(params INode[] nodes)
        {
            this.PrependNodes(nodes);
        }

        /// <inheritdoc />
        public void Append(params INode[] nodes)
        {
            this.AppendNodes(nodes);
        }

        /// <inheritdoc />
        public override Boolean Equals(INode otherNode)
        {
            var otherElement = otherNode as IElement;

            if (otherElement != null)
            {
                return NamespaceUri.Is(otherElement.NamespaceUri) &&
                    _attributes.SameAs(otherElement.Attributes) &&
                    base.Equals(otherNode);
            }

            return false;
        }

        /// <inheritdoc />
        public void Before(params INode[] nodes)
        {
            this.InsertBefore(nodes);
        }

        /// <inheritdoc />
        public void After(params INode[] nodes)
        {
            this.InsertAfter(nodes);
        }

        /// <inheritdoc />
        public void Replace(params INode[] nodes)
        {
            this.ReplaceWith(nodes);
        }

        /// <inheritdoc />
        public void Remove()
        {
            this.RemoveFromParent();
        }

        /// <inheritdoc />
        public void Insert(AdjacentPosition position, String html)
        {
            var useThis = position == AdjacentPosition.AfterBegin || position == AdjacentPosition.BeforeEnd;
            var context = useThis ? this : Parent as Element;

            if (context == null)
                throw new DomException("The element has no parent.");

            var nodes = new DocumentFragment(context, html);

            switch (position)
            {
                case AdjacentPosition.BeforeBegin:
                    Parent.InsertBefore(nodes, this);
                    break;

                case AdjacentPosition.AfterEnd:
                    Parent.InsertChild(Parent.IndexOf(this) + 1, nodes);
                    break;

                case AdjacentPosition.AfterBegin:
                    InsertChild(0, nodes);
                    break;

                case AdjacentPosition.BeforeEnd:
                    AppendChild(nodes);
                    break;
            }
        }

        /// <inheritdoc />
        public override void ToHtml(TextWriter writer, IMarkupFormatter formatter)
        {
            var selfClosing = (Flags & NodeFlags.SelfClosing) == NodeFlags.SelfClosing;
            writer.Write(formatter.OpenTag(this, selfClosing));

            if (!selfClosing)
            {
                if (((Flags & NodeFlags.LineTolerance) == NodeFlags.LineTolerance) && FirstChild is IText)
                {
                    var text = (IText)FirstChild;

                    if (text.Data.Has(Symbols.LineFeed))
                    {
                        writer.Write(Symbols.LineFeed);
                    }
                }

                foreach (var child in ChildNodes)
                {
                    child.ToHtml(writer, formatter);
                }
            }

            writer.Write(formatter.CloseTag(this, selfClosing));
        }

        /// <inheritdoc />
        public override Node Clone(Document owner, Boolean deep)
        {
            var node = new AnyElement(owner, LocalName, _prefix, _namespace, Flags);
            CloneElement(node, owner, deep);
            return node;
        }

        #endregion

        #region Internal Methods

        internal virtual void SetupElement()
        {
            var attrs = _attributes;

            if (attrs.Length > 0)
            {
                var observers = Context.GetServices<IAttributeObserver>();

                foreach (var attr in attrs)
                {
                    var name = attr.LocalName;
                    var value = attr.Value;

                    foreach (var observer in observers)
                    {
                        observer.NotifyChange(this, name, value);
                    }
                }
            }
        }

        internal void AttributeChanged(String localName, String namespaceUri, String oldValue, String newValue)
        {
            if (namespaceUri == null)
            {
                var observers = Context.GetServices<IAttributeObserver>();

                foreach (var observer in observers)
                {
                    observer.NotifyChange(this, localName, newValue);
                }
            }

            Owner.QueueMutation(MutationRecord.Attributes(
                target: this,
                attributeName: localName,
                attributeNamespace: namespaceUri,
                previousValue: oldValue));
        }

        internal void UpdateClassList(String value)
        {
            _classList?.Update(value);
        }

        #endregion

        #region Helpers

        /// <inheritdoc />
        protected void UpdateAttribute(String name, String value)
        {
            this.SetOwnAttribute(name, value, suppressCallbacks: true);
        }

        /// <inheritdoc />
        protected sealed override String LocateNamespace(String prefix)
        {
            return this.LocateNamespaceFor(prefix);
        }

        /// <inheritdoc />
        protected sealed override String LocatePrefix(String namespaceUri)
        {
            return this.LocatePrefixFor(namespaceUri);
        }

        /// <inheritdoc />
        protected void CloneElement(Element element, Document owner, Boolean deep)
        {
            CloneNode(element, owner, deep);

            foreach (var attribute in _attributes)
            {
                var attr = new Attr(attribute.Prefix, attribute.LocalName, attribute.Value, attribute.NamespaceUri);
                element._attributes.FastAddItem(attr);
            }

            element.SetupElement();
        }

        #endregion
    }
}
