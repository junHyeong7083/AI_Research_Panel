using System.Collections.Generic;
using System.Linq;

public class QuestionFlattener
{
    // SurveySimulator.QuestionData 와 맞춰야 해서 여기도 같은 구조 정의
    public class QuestionData
    {
        public string id;
        public string question;
        public string type;
        public List<string> options;
        public List<RowData> rows;
        public List<string> scale;
    }

    public class RowData
    {
        public string id;
        public string label;
    }

    public class FlattenedQuestion
    {
        public string id;
        public string question;
        public string type;     // "multi" / "text" / "table_row"
        public List<string> options;
    }

    public List<FlattenedQuestion> Flatten(List<GptSurveyStructurer.QuestionData> src)
    {
        var list = new List<FlattenedQuestion>();

        foreach (var q in src)
        {
            if (q == null) continue;

            // table이 아니면 그대로
            if (!string.Equals(q.type, "table", System.StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new FlattenedQuestion
                {
                    id = q.id,
                    question = q.question,
                    type = string.IsNullOrEmpty(q.type) ? "text" : q.type,
                    options = q.options ?? new List<string>()
                });
                continue;
            }

            // table이면 행마다 하나
            if (q.rows != null && q.rows.Count > 0)
            {
                int idx = 1;
                foreach (var row in q.rows)
                {
                    string rowId = !string.IsNullOrEmpty(row.id) ? row.id : $"{q.id}_{idx}";
                    string fullQ = $"{q.question} - {row.label}";

                    var opts = (q.scale != null && q.scale.Count > 0)
                        ? new List<string>(q.scale)
                        : (q.options ?? new List<string>());

                    list.Add(new FlattenedQuestion
                    {
                        id = rowId,
                        question = fullQ,
                        type = "table_row",
                        options = opts
                    });

                    idx++;
                }
            }
            else
            {
                // 행이 없으면 1개짜리로
                list.Add(new FlattenedQuestion
                {
                    id = q.id,
                    question = q.question,
                    type = "table_row",
                    options = q.scale ?? q.options ?? new List<string>()
                });
            }
        }

        return list;
    }
}
