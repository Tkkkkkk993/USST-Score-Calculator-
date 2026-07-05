using System;
using System.Collections.Generic;

public class GradeCalculator
{
    // 映射表：根据学校制度实现 [cite: 30, 31]
    public static float GetGradePoint(float score)
    {
        if (score >= 95) return 4.5f;
        if (score >= 90) return 4.0f;
        if (score >= 85) return 3.5f;
        if (score >= 80) return 3.0f;
        if (score >= 75) return 2.5f;
        if (score >= 70) return 2.0f;
        if (score >= 65) return 1.5f;
        if (score >= 60) return 1.0f;
        return 0.0f;
    }

 // 计算加权平均分 [cite: 9]
    public static float CalculateWeightedAverageScore(List<float> scores, List<float> credits)
    {
        float totalScoreCredits = 0;
        float totalCredits = 0;
        for (int i = 0; i < scores.Count; i++)
        {
            totalScoreCredits += scores[i] * credits[i];
            totalCredits += credits[i];
        }
        return totalCredits == 0 ? 0 : totalScoreCredits / totalCredits;
    }

    // 计算加权平均绩点 [cite: 10]
    public static float CalculateWeightedGPA(List<float> scores, List<float> credits)
    {
        float totalPointsCredits = 0;
        float totalCredits = 0;
        for (int i = 0; i < scores.Count; i++)
        {
            float point = GetGradePoint(scores[i]);
            totalPointsCredits += point * credits[i];
            totalCredits += credits[i];
        }
        return totalCredits == 0 ? 0 : totalPointsCredits / totalCredits;
    }
}