namespace Jmodot.Tools.DocTooltips.DocLookup;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Xml;

/// <summary>
/// Property and field summaries read from a compiler-emitted XML documentation sidecar.
/// </summary>
/// <remarks>
/// The sidecar for this project is ~11.5 MB, is regenerated on every build, and is loaded on the
/// editor's main thread — so it is streamed with <see cref="XmlReader"/> and only <c>T:</c>,
/// <c>P:</c> and <c>F:</c> members are retained. <see cref="System.Xml.Linq.XDocument"/> would
/// materialise a DOM of the whole file for the same result.
///
/// Staleness is checked by last-write time, and the check is the caller's to schedule: rebuilding on
/// a stat per lookup would hit the filesystem once per property per inspector rebuild.
/// </remarks>
public sealed class XmlDocIndex
{
    private readonly string _path;
    private readonly Dictionary<string, string> _summaries = new(StringComparer.Ordinal);
    private DateTime _loadedStamp = DateTime.MinValue;
    private bool _loaded;

    /// <param name="path">Absolute path to the XML sidecar. It need not exist.</param>
    public XmlDocIndex(string path)
    {
        this._path = path;
    }

    /// <summary>Absolute path this index reads from.</summary>
    public string Path => this._path;

    /// <summary>Number of retained members. Zero until the first successful load.</summary>
    public int Count => this._summaries.Count;

    /// <summary>
    /// Reloads if the file's last-write time has moved since the last load. Safe to call when the
    /// file is absent — that is reported as "not loaded", never an exception.
    /// </summary>
    /// <returns><c>true</c> if the index was rebuilt by this call.</returns>
    public bool RefreshIfStale()
    {
        if (!File.Exists(this._path))
        {
            return false;
        }

        DateTime stamp;
        try
        {
            stamp = File.GetLastWriteTimeUtc(this._path);
        }
        catch (IOException)
        {
            return false;
        }

        if (this._loaded && stamp == this._loadedStamp)
        {
            return false;
        }

        this.Load(stamp);
        return true;
    }

    /// <summary>
    /// Looks up a full doc ID (<c>P:Namespace.Type.Member</c> or <c>F:...</c>) against whatever is
    /// currently loaded. Touches the filesystem never — call <see cref="RefreshIfStale"/> first, once
    /// per batch of lookups, to load or reload. Returns false until something has been loaded.
    /// </summary>
    public bool TryGetSummary(string docId, [MaybeNullWhen(false)] out string summary)
    {
        if (this._summaries.TryGetValue(docId, out string? found))
        {
            summary = found;
            return true;
        }

        summary = null;
        return false;
    }

    private void Load(DateTime stamp)
    {
        this._summaries.Clear();
        this._loaded = true;
        this._loadedStamp = stamp;

        var settings = new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Prohibit,
            CloseInput = true,
        };

        try
        {
            using FileStream stream = File.Open(this._path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using XmlReader reader = XmlReader.Create(stream, settings);

            string? currentId = null;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "member")
                {
                    string? name = reader.GetAttribute("name");
                    currentId = IsRetainedKind(name) ? name : null;
                    continue;
                }

                if (currentId == null) { continue; }

                if (reader.NodeType == XmlNodeType.Element && reader.Name == "summary")
                {
                    string text = Collapse(reader.ReadInnerXml());
                    if (text.Length > 0)
                    {
                        this._summaries[currentId] = text;
                    }

                    currentId = null;
                }
                else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "member")
                {
                    currentId = null;
                }
            }
        }
        catch (XmlException)
        {
            // A half-written sidecar (the compiler is mid-build) is a transient state, not a defect;
            // whatever parsed before the fault is kept and the next stamp change re-reads it.
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
            // An ACL denial, or a path that resolves to a directory. Not an IOException, and this
            // runs inside a deferred editor callback where an escaping exception has no call site to
            // attribute it to.
        }
    }

    /// <summary>
    /// Types, properties and fields. Methods, events and namespaces are dropped: no editor surface
    /// this feeds asks about them, and they are the bulk of the sidecar's entries.
    /// </summary>
    private static bool IsRetainedKind(string? id)
        => id != null && id.Length > 2 && id[1] == ':'
           && (id[0] == 'T' || id[0] == 'P' || id[0] == 'F');

    /// <summary>
    /// Flattens a summary body to a single tooltip line: inner markup is reduced to its text content
    /// and runs of whitespace collapse to one space. A tooltip has no renderer for
    /// <c>&lt;see cref&gt;</c>, so the target reads as plain text.
    /// </summary>
    private static string Collapse(string innerXml)
    {
        var sb = new StringBuilder(innerXml.Length);
        bool inTag = false;
        bool pendingSpace = false;

        foreach (char c in innerXml)
        {
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (inTag) { continue; }

            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(c);
        }

        return Unescape(sb.ToString());
    }

    private static string Unescape(string s)
        => s.Contains('&')
            ? s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"")
               .Replace("&apos;", "'").Replace("&amp;", "&")
            : s;
}
