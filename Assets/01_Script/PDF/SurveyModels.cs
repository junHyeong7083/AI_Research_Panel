using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// GPT ���� ����ȭ ��� �� + Python �Ľ� ��� ��
/// </summary>
[System.Serializable]
public class QuestionData
{
    [JsonProperty("id")] public string id;
    [JsonProperty("question")] public string question;
    [JsonProperty("type")] public string type;      // text / table / multi / unknown
    [JsonProperty("options")] public List<string> options;
    [JsonProperty("rows")] public List<TableRow> rows;
    [JsonProperty("scale")] public List<string> scale;
    [JsonProperty("allow_multiple")] public bool allowMultiple;
}

public class TableRow
{
    [JsonProperty("id")] public string id;
    [JsonProperty("label")] public string label;
}

/// <summary>
/// ���� GPT ���� ������ ����� ������ ����
/// </summary>
public class FlattenedQuestion
{
    public string id;
    public string question;
    public List<string> options;
    public string type;     // text / multi / table_row
    public string rowLabel;
}

/// <summary>
/// Python script�� �ִ� ���� ���
/// </summary>
public class PythonRawResult
{
    public string status;
    public string error;
    public string pdf_path;
    public List<PythonPage> pages;
}

public class PythonPage
{
    public int page;
    public string text;
    public List<PythonTable> tables;
}

public class PythonTable
{
    public List<float> bbox;
    public string ocr_text;
}
