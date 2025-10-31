using System.Collections.Generic;
using System.Linq;

/// <summary>
/// GPT가 만들어준 문항 구조에서
/// table / multi를 실제 답변 가능한 단일 문항 목록으로 전개
/// 즉 "응답 스키마" 만드는 단계
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

            // 1) 표(table)인 경우 → rows 개수만큼 문항 생성
            if (qType == "table")
            {
                if (q.rows != null && q.rows.Count > 0)
                {
                    int idx = 1;
                    foreach (var row in q.rows)
                    {
                        // row에 id가 없으면 "SQ6_1" 이런 식으로 만들어줌
                        string rowId = !string.IsNullOrEmpty(row.id) ? row.id : $"{q.id}_{idx}";
                        string fullQ = $"{q.question} - {row.label}";

                        // 표는 보통 scale에 있는 값이 실제 선택지(1~7 척도)
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
                    // row 정보가 없는 table → 그냥 1문항으로
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

            // 2) 복수선택(multi) → 그대로 하나의 문항이지만 GPT가 여러 개 고르게 시킴
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

            // 3) 그 외는 단일 선택(text)로 봄
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
