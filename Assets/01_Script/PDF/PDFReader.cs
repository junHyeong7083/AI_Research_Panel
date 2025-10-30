using System.Text;
using UglyToad.PdfPig;

public static class PDFReader
{
    public static string ReadTextFromPDF(string path)
    {
        var sb = new StringBuilder();

        using (var document = PdfDocument.Open(path))
        {
            foreach (var page in document.GetPages())
                sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }
}
