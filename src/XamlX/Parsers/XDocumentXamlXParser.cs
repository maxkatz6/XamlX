using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using XamlX.Ast;

namespace XamlX.Parsers
{
    public class XDocumentXamlXParserSettings
    {
        public Dictionary<string, string> CompatibleNamespaces { get; set; }
    }

    public class XDocumentXamlXParser
    {

        public static XamlXDocument Parse(string s) => Parse(new StringReader(s));

        public static XamlXDocument Parse(TextReader reader)
        {
            var root = XDocument.Load(reader, LoadOptions.SetLineInfo).Root;

            var doc = new XamlXDocument
            {
                Root = new ParserContext(root).Parse()
            };
            
            foreach(var attr in root.Attributes())
                if (attr.Name.NamespaceName == "http://www.w3.org/2000/xmlns/" ||
                    (attr.Name.NamespaceName == "" && attr.Name.LocalName == "xmlns"))
                {
                    var name = attr.Name.NamespaceName == "" ? "" : attr.Name.LocalName;
                    doc.NamespaceAliases[name] = attr.Value;
                }

            return doc;
        }


        class ParserContext
        {
            private readonly XElement _root;

            public ParserContext(XElement root)
            {
                _root = root;
            }


            XamlXAstXmlTypeReference GetTypeReference(XElement el) =>
                new XamlXAstXmlTypeReference(el.AsLi(), el.Name.NamespaceName, el.Name.LocalName);


            static List<XamlXAstXmlTypeReference> ParseTypeArguments(string args, XElement xel, IXamlXLineInfo info)
            {
                try
                {
                    XamlXAstXmlTypeReference Parse(CommaSeparatedParenthesesTreeParser.Node node)
                    {
                        var pair = node.Value.Trim().Split(new[] {':'}, 2);
                        string xmlns, name;
                        if (pair.Length == 1)
                        {
                            xmlns = xel.GetDefaultNamespace().NamespaceName;
                            name = pair[0];
                        }
                        else
                        {
                            xmlns = xel.GetNamespaceOfPrefix(pair[0])?.NamespaceName;
                            if (xmlns == null)
                                throw new XamlXParseException($"Namespace '{pair[0]}' is not recognized", info);
                            name = pair[1];
                        }
                        var rv = new XamlXAstXmlTypeReference(info, xmlns, name);

                        if (node.Children.Count != 0)
                            rv.GenericArguments = node.Children.Select(Parse).ToList();
                        return rv;
                    }
                    var tree = CommaSeparatedParenthesesTreeParser.Parse(args);
                    return tree.Select(Parse).ToList();
                }
                catch (CommaSeparatedParenthesesTreeParser.ParseException e)
                {
                    throw new XamlXParseException(e.Message, info);
                }
            }
            

            XamlXAstNewInstanceNode ParseNewInstance(XElement el, bool root)
            {
                if (el.Name.LocalName.Contains("."))
                    throw ParseError(el, "Dots aren't allowed in type names");
                var type = GetTypeReference(el);
                var i = new XamlXAstNewInstanceNode(el.AsLi(), type);
                foreach (var attr in el.Attributes())
                {
                    if (attr.Name.NamespaceName == "http://www.w3.org/2000/xmlns/" ||
                        (attr.Name.NamespaceName == "" && attr.Name.LocalName == "xmlns"))
                    {
                        if (!root)
                            throw ParseError(attr,
                                "xmlns declarations are only allowed on the root element to preserve memory");
                    }
                    else if (attr.Name.NamespaceName.StartsWith("http://www.w3.org"))
                    {
                        // Silently ignore all xml-parser related attributes
                    }
                    // Parse type arguments
                    else if (attr.Name.NamespaceName == XamlNamespaces.Xaml2006 &&
                             attr.Name.LocalName == "TypeArguments")
                        type.GenericArguments = ParseTypeArguments(attr.Value, el, attr.AsLi());
                    // Parse as a directive
                    else if (attr.Name.NamespaceName != "" && !attr.Name.LocalName.Contains("."))
                        i.Children.Add(new XamlXAstXmlDirective(el.AsLi(),
                            attr.Name.NamespaceName, attr.Name.LocalName, new[]
                            {
                                new XamlXAstTextNode(el.AsLi(), attr.Value)
                            }
                        ));
                    // Parse as a property
                    else
                    {
                        var pname = attr.Name.LocalName;
                        var ptype = i.Type;

                        if (pname.Contains("."))
                        {
                            var parts = pname.Split(new[] {'.'}, 2);
                            pname = parts[1];
                            var ns = attr.Name.Namespace == "" ? el.Name.NamespaceName : attr.Name.NamespaceName;
                            ptype = new XamlXAstXmlTypeReference(el.AsLi(), ns, parts[0]);
                        }

                        i.Children.Add(new XamlXAstXamlPropertyValueNode(el.AsLi(),
                            new XamlXAstNamePropertyReference(el.AsLi(), ptype, pname, type),
                            new XamlXAstTextNode(el.AsLi(), attr.Value)));
                    }
                }


                foreach (var node in el.Nodes())
                {
                    if (node is XElement elementNode && elementNode.Name.LocalName.Contains("."))
                    {
                        if (elementNode.HasAttributes)
                            throw ParseError(node, "Attributes aren't allowed on element properties");
                        var pair = elementNode.Name.LocalName.Split(new[] {'.'}, 2);
                        i.Children.Add(new XamlXAstXamlPropertyValueNode(el.AsLi(), new XamlXAstNamePropertyReference
                            (
                                el.AsLi(),
                                new XamlXAstXmlTypeReference(el.AsLi(), elementNode.Name.NamespaceName,
                                    pair[0]), pair[1], type
                            ),
                            ParseValueNodeChildren(elementNode)
                        ));
                    }
                    else
                    {
                        var parsed = ParseValueNode(node);
                        if (parsed != null)
                            i.Children.Add(parsed);
                    }

                }

                return i;
            }

            IXamlXAstValueNode ParseValueNode(XNode node)
            {
                if (node is XElement el)
                    return ParseNewInstance(el, false);
                if (node is XText text)
                    return new XamlXAstTextNode(node.AsLi(), text.Value);
                return null;
            }

            List<IXamlXAstValueNode> ParseValueNodeChildren(XElement parent)
            {
                var lst = new List<IXamlXAstValueNode>();
                foreach (var n in parent.Nodes())
                {
                    var parsed = ParseValueNode(n);
                    if (parsed != null)
                        lst.Add(parsed);
                }
                return lst;
            }

            Exception ParseError(IXmlLineInfo line, string message) =>
                new XamlXParseException(message, line.LineNumber, line.LinePosition);

            public XamlXAstNewInstanceNode Parse() => (XamlXAstNewInstanceNode) ParseNewInstance(_root, true);
        }
    }

    static class Extensions
    {
        class WrappedLineInfo : IXamlXLineInfo
        {
            public WrappedLineInfo(IXmlLineInfo info)
            {
                Line = info.LineNumber;
                Position = info.LinePosition;
            }
            public int Line { get; set; }
            public int Position { get; set; }
        }
        
        public static IXamlXLineInfo AsLi(this IXmlLineInfo info)
        {
            if (!info.HasLineInfo())
                throw new InvalidOperationException("XElement doesn't have line info");
            return new WrappedLineInfo(info);
        }

    }
}