using System.IO.Compression;
using System.Text;
using System.Xml;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ExamPrep.Api.Modules;

/// <summary>
/// Turns Word and PDF question banks into the Markdown bank format, keeping where each line came from
/// (paragraph or page) so provenance can point at the analysed part of the file. Content is treated as data only.
/// </summary>
public static class DocumentText
{
    public const long MaxUncompressedXml = 20 * 1024 * 1024;
    public const int MaxPdfPages = 300;
    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public record Line(string Text, string Location);

    public static List<Line> FromDocx(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var entry = zip.GetEntry("word/document.xml") ?? throw new FormatException("The .docx file has no word/document.xml.");
        if (entry.Length > MaxUncompressedXml) throw new FormatException("The document is too large once uncompressed.");
        if (entry.CompressedLength > 0 && entry.Length / Math.Max(1, entry.CompressedLength) > 200) throw new FormatException("The document compression ratio is suspicious.");
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxUncompressedXml };
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, settings);
        var lines = new List<Line>();
        var current = new StringBuilder();
        var paragraph = 0;
        var inText = false;
        while (reader.Read())
        {
            if (reader.NodeType is XmlNodeType.Text or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace) { if (inText) current.Append(reader.Value); continue; }
            if (reader.NamespaceURI != W) continue;
            switch (reader.NodeType)
            {
                case XmlNodeType.Element when reader.LocalName == "p":
                    current.Clear(); paragraph++;
                    if (reader.IsEmptyElement) lines.Add(new Line("", $"paragraph {paragraph}"));
                    break;
                case XmlNodeType.Element when reader.LocalName == "t":
                    inText = !reader.IsEmptyElement;
                    break;
                case XmlNodeType.EndElement when reader.LocalName == "t":
                    inText = false;
                    break;
                case XmlNodeType.Element when reader.LocalName == "tab":
                    current.Append('\t');
                    break;
                case XmlNodeType.Element when reader.LocalName is "br" or "cr":
                    lines.Add(new Line(current.ToString(), $"paragraph {paragraph}")); current.Clear();
                    break;
                case XmlNodeType.EndElement when reader.LocalName == "p":
                    lines.Add(new Line(current.ToString(), $"paragraph {paragraph}")); current.Clear();
                    break;
            }
        }
        return lines;
    }

    public static List<Line> FromPdf(byte[] bytes)
    {
        using var doc = PdfDocument.Open(bytes);
        if (doc.NumberOfPages > MaxPdfPages) throw new FormatException($"The PDF has more than {MaxPdfPages} pages.");
        var lines = new List<Line>();
        foreach (var page in doc.GetPages())
        {
            var text = ContentOrderTextExtractor.GetText(page);
            foreach (var l in text.Replace("\r\n", "\n").Split('\n')) lines.Add(new Line(l, $"page {page.Number}"));
        }
        return lines;
    }

    /// <summary>Normalises typical word-processor glyphs so the Markdown bank parser can read the lines.</summary>
    public static string Normalize(string line)
    {
        var t = line.Replace(' ', ' ').Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"').TrimEnd();
        var trimmed = t.TrimStart();
        foreach (var bullet in new[] { "•", "◦", "▪", "‣", "–", "—", "·" })
            if (trimmed.StartsWith(bullet + " ")) { trimmed = "- " + trimmed[(bullet.Length + 1)..]; break; }
        trimmed = trimmed.Replace("☒", "[x]").Replace("☑", "[x]").Replace("✔", "[x]").Replace("☐", "[ ]");
        if (trimmed.StartsWith("[x]") || trimmed.StartsWith("[ ]")) trimmed = "- " + trimmed;
        return trimmed;
    }
}
