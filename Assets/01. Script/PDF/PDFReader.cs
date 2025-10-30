using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

public static class PDFReader
{
    public static string ReadTextFromPDF(string path)
    {
        using (var doc = PdfDocument.Open(path))
        {
            var sb = new System.Text.StringBuilder();
            foreach (var page in doc.GetPages())
                sb.AppendLine(page.Text);
            return sb.ToString();
        }
    }
}
