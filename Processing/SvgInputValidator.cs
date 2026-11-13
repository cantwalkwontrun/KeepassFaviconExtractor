using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace FaviconExtractor
{
    internal static class SvgInputValidator
    {
        internal static XmlDocument Read(byte[] bytes)
        {
            if (bytes.Length > FaviconDiscoveryPreferences.MaxIconDownloadBytes)
                throw new InvalidOperationException("SVG content is too large.");

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = FaviconDiscoveryPreferences.MaxIconDownloadBytes,
                IgnoreComments = true
            };
            try
            {
                // Check depth before loading the DOM, rather than after allocating it.
                using (var stream = new MemoryStream(bytes))
                using (var reader = XmlReader.Create(stream, settings))
                {
                    int nodes = 0;
                    while (reader.Read())
                    {
                        if (++nodes > 4096 || reader.Depth > 64 || reader.AttributeCount > 64 || reader.Value.Length > 65536)
                            throw new InvalidOperationException("SVG content is too complex.");
                        if (reader.NodeType == XmlNodeType.ProcessingInstruction)
                            throw new InvalidOperationException("SVG processing instructions are unsupported.");
                        if (reader.NodeType != XmlNodeType.Element) continue;

                        string element = reader.LocalName;
                        if (element == "image" || element == "feImage" || element == "foreignObject" || element == "script")
                            throw new InvalidOperationException("Embedded images and active SVG content are unsupported.");
                        while (reader.MoveToNextAttribute())
                        {
                            if (reader.Value.Length > 65536)
                                throw new InvalidOperationException("SVG attribute is too large.");
                            if (reader.LocalName == "href" && (!reader.Value.Trim().StartsWith("#", StringComparison.Ordinal) || reader.Value.IndexOf('%') >= 0))
                                throw new InvalidOperationException("External or escaped SVG references are unsupported.");
                            ValidateStyle(reader.Value);
                        }
                        reader.MoveToElement();
                    }
                }
                var document = new XmlDocument { XmlResolver = null };
                using (var stream = new MemoryStream(bytes))
                using (var reader = XmlReader.Create(stream, settings)) document.Load(reader);
                if (document.DocumentElement == null || document.DocumentElement.LocalName != "svg" ||
                    document.DocumentElement.NamespaceURI != "http://www.w3.org/2000/svg")
                    throw new InvalidOperationException("Downloaded XML/HTML content is not an SVG icon.");
                foreach (XmlNode style in document.GetElementsByTagName("style", "http://www.w3.org/2000/svg"))
                    ValidateStyle(style.InnerText);
                ValidateReferenceExpansion(document);
                return document;
            }
            catch (XmlException error)
            {
                throw new InvalidOperationException("SVG XML is invalid or contains a prohibited DTD: " + error.Message, error);
            }
        }

        private static void ValidateReferenceExpansion(XmlDocument document)
        {
            var ids = new Dictionary<string, XmlElement>(StringComparer.Ordinal);
            foreach (XmlElement element in document.GetElementsByTagName("*"))
            {
                string id = element.GetAttribute("id");
                if (id.Length == 0) continue;
                if (ids.ContainsKey(id)) throw new InvalidOperationException("Duplicate SVG identifiers are unsupported.");
                ids.Add(id, element);
            }
            int expanded = 0;
            VisitReference(document.DocumentElement, ids, new HashSet<XmlElement>(), 0, ref expanded);
        }

        private static void VisitReference(XmlElement element, Dictionary<string, XmlElement> ids,
            HashSet<XmlElement> ancestors, int depth, ref int expanded)
        {
            if (depth > 64 || ++expanded > 16384)
                throw new InvalidOperationException("SVG reference expansion is too complex.");
            if (!ancestors.Add(element)) throw new InvalidOperationException("Cyclic SVG references are unsupported.");
            foreach (XmlNode child in element.ChildNodes)
            {
                var nested = child as XmlElement;
                if (nested != null) VisitReference(nested, ids, ancestors, depth + 1, ref expanded);
            }
            foreach (XmlAttribute attribute in element.Attributes)
            {
                if (attribute.LocalName != "href") continue;
                string reference = attribute.Value.Trim();
                XmlElement target;
                if (reference.StartsWith("#", StringComparison.Ordinal) && ids.TryGetValue(reference.Substring(1), out target))
                    VisitReference(target, ids, ancestors, depth + 1, ref expanded);
            }
            ancestors.Remove(element);
        }

        private static void ValidateStyle(string value)
        {
            // Reject CSS escapes/imports so obfuscated external references cannot bypass this check.
            if (value.IndexOf('\\') >= 0 || value.IndexOf("@import", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("External or escaped SVG resources are unsupported.");
            int position = 0;
            while ((position = value.IndexOf("url", position, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int start = position + 3;
                while (start < value.Length && char.IsWhiteSpace(value[start])) start++;
                position = start;
                if (start >= value.Length || value[start] != '(') continue;
                int end = value.IndexOf(')', start + 1);
                if (end < 0) throw new InvalidOperationException("SVG resource reference is malformed.");
                string target = value.Substring(start + 1, end - start - 1).Trim().Trim('\'', '"');
                if (!target.StartsWith("#", StringComparison.Ordinal))
                    throw new InvalidOperationException("External SVG resources are unsupported.");
                position = end + 1;
            }
        }
    }
}
