using System.Collections.Generic;
using System.Linq;

/// <summary>
/// GPT�� ������� ���� ��������
/// table / multi�� ���� �亯 ������ ���� ���� ������� ����
/// �� "���� ��Ű��" ����� �ܰ�
/// </summary>
public interface IQuestionFlattener
{
    List<FlattenedQuestion> Flatten(List<QuestionData> src);
}

public class DefaultQuestionFlattener : IQuestionFlattener
{
    public List<FlattenedQuestion> Flatten(List<QuestionData> src)
    {
        var list = new List<FlattenedQuestion>();
        if (src == null) return list;

        foreach (var q in src)
        {
            if (q == null) continue;

            string qType = (q.type ?? "").ToLowerInvariant();

            // 1) ǥ(table)�� ��� �� rows ������ŭ ���� ����
            if (qType == "table")
            {
                if (q.rows != null && q.rows.Count > 0)
                {
                    int idx = 1;
                    foreach (var row in q.rows)
                    {
                        // row�� id�� ������ "SQ6_1" �̷� ������ �������
                        string rowId = !string.IsNullOrEmpty(row.id) ? row.id : $"{q.id}_{idx}";
                        string fullQ = $"{q.question} - {row.label}";

                        // ǥ�� ���� scale�� �ִ� ���� ���� ������(1~7 ô��)
                        var opts = (q.scale != null && q.scale.Count > 0)
                            ? new List<string>(q.scale)
                            : (q.options ?? new List<string>());

                        list.Add(new FlattenedQuestion
                        {
                            id = rowId,
                            question = fullQ,
                            options = opts,
                            type = "table_row",
                            rowLabel = row.label
                        });
                        idx++;
                    }
                }
                else
                {
                    // row ������ ���� table �� �׳� 1��������
                    list.Add(new FlattenedQuestion
                    {
                        id = q.id,
                        question = q.question,
                        options = q.options ?? new List<string>(),
                        type = "table_row",
                        rowLabel = ""
                    });
                }
                continue;
            }

            // 2) ��������(multi) �� �״�� �ϳ��� ���������� GPT�� ���� �� ���� ��Ŵ
            if (qType == "multi" || q.allowMultiple)
            {
                list.Add(new FlattenedQuestion
                {
                    id = q.id,
                    question = q.question,
                    options = q.options ?? new List<string>(),
                    type = "multi",
                    rowLabel = ""
                });
                continue;
            }

            // 3) �� �ܴ� ���� ����(text)�� ��
            list.Add(new FlattenedQuestion
            {
                id = q.id,
                question = q.question,
                options = q.options ?? new List<string>(),
                type = "text",
                rowLabel = ""
            });
        }

        return list;
    }
}
