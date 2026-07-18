using Godot;
using System.Collections.Generic;

public partial class MainBoard : Control
{
    [Export] public PackedScene CourseRowPrefab;
    // 上一次课程变化后的计算结果
    private float _lastScore = 0.0f;
    private float _lastGPA = 0.0f;
    // 上一次课程数据快照
    private string _lastCourseSnapshot = "";
    // 是否存在有效历史结果
    private bool _hasLastResult = false;
    private bool isSaving = false;

    public override void _Ready()
    {
        GetNode<Button>("CalculationArea/CalculateButton").Pressed += OnCalculatePressed;

        GetNode<Button>("AddCourseButton").Pressed += OnAddCoursePressed;


        var filePicker = GetNode<FileDialog>("FilePicker");

        filePicker.Theme = GD.Load<Theme>("res://样式/t2.tres");

        filePicker.FileSelected += OnFileSelected;
    }

    private void OnAddCoursePressed()
    {
        var newRow = CourseRowPrefab.Instantiate();

        GetNode<VBoxContainer>(
            "SemesterTabs/SemesterPanel/CourseList"
        ).AddChild(newRow);
    }

    private void OnCalculatePressed()
    {
        var courseList =
            GetNode<VBoxContainer>(
                "SemesterTabs/SemesterPanel/CourseList"
            );

        float totalCredits = 0;

        List<float> scores = new List<float>();
        List<float> credits = new List<float>();

        string currentSnapshot = "";

        foreach (var child in courseList.GetChildren())
        {
            if (child is CourseRow row)
            {
                var data = row.GetRowData();


                float s = (float)data["score"];

                float c = (float)data["credits"];

                string name = (string)data["name"];

                if (!string.IsNullOrEmpty(name)
                    || c > 0
                    || s > 0)
                {
                    scores.Add(s);

                    credits.Add(c);

                    totalCredits += c;
                }

                currentSnapshot +=
                    name +
                    c.ToString() +
                    s.ToString();
            }
        }

        float avgScore =
            GradeCalculator.CalculateWeightedAverageScore(
                scores,
                credits
            );


        float avgGPA =
            GradeCalculator.CalculateWeightedGPA(
                scores,
                credits
            );

        GetNode<Label>(
            "CalculationArea/TotalCreditsLabel"
        ).Text =
            $"当前总学分为：{totalCredits:F1}";

        GetNode<Label>(
            "CalculationArea/ScoreLabel"
        ).Text =
            $"加权均分: {avgScore:F2}";

        GetNode<Label>(
            "CalculationArea/GPALabel"
        ).Text =
            $"加权GPA: {avgGPA:F2}";

        if (currentSnapshot != _lastCourseSnapshot)
        {

            UpdateGrowthUI(avgScore, avgGPA);
            _lastScore = avgScore;
            _lastGPA = avgGPA;
            _lastCourseSnapshot = currentSnapshot;
            _hasLastResult = true;

        }
        else
        {

            GetNode<Label>(
                "CalculationArea/GrowthLabel"
            ).Text =
                "成绩未发生变化";

        }
    }

    private void UpdateGrowthUI(
        float currentScore,
        float currentGPA
    )
    {
        var growthLabel =
            GetNode<Label>(
                "CalculationArea/GrowthLabel"
            );

        if (!_hasLastResult)
        {
            growthLabel.Text =
                "首次计算，无历史记录";

            return;
        }

        float sDiff =
            currentScore - _lastScore;

        float sPct =
            (_lastScore > 0)
            ? (sDiff / _lastScore) * 100
            : 0;

        float gDiff =
            currentGPA - _lastGPA;

        float gPct =
            (_lastGPA > 0)
            ? (gDiff / _lastGPA) * 100
            : 0;

        growthLabel.Text =
            $"均分: {sDiff:+0.0;-0.0;0}" +
            $"({sPct:+0.0;-0.0;0}%) | " +

            $"绩点: {gDiff:+0.00;-0.00;0}" +
            $"({gPct:+0.0;-0.0;0}%)";

        growthLabel.Modulate =
            (sDiff >= 0 && gDiff >= 0)

            ? new Color(0,1,0)

            : new Color(1,0,0);
    }

    private void ExecuteSave(string path)
    {

        var allData = new Godot.Collections.Array();


        var courseList =
            GetNode<VBoxContainer>(
                "SemesterTabs/SemesterPanel/CourseList"
            );

        foreach (var child in courseList.GetChildren())
        {

            if (child is CourseRow row)
            {
                allData.Add(row.GetRowData());
            }

        }
        // 保存当前计算结果
        List<float> scores = new List<float>();
        List<float> credits = new List<float>();
        foreach (var child in courseList.GetChildren())
        {

            if (child is CourseRow row)
            {

                var data = row.GetRowData();
                scores.Add(
                    (float)data["score"]
                );
                credits.Add(
                    (float)data["credits"]
                );

            }

        }

        var finalData =
            new Godot.Collections.Dictionary
            {

                {
                    "courses",
                    allData
                },
                {
                    "last_score",
                    GradeCalculator.CalculateWeightedAverageScore(
                        scores,
                        credits
                    )
                },
                {
                    "last_gpa",
                    GradeCalculator.CalculateWeightedGPA(
                        scores,
                        credits
                    )
                }

            };
        using var file =
            FileAccess.Open(
                path,
                FileAccess.ModeFlags.Write
            );
        file.StoreString(
            Json.Stringify(finalData)
        );
        ShowStatus(
            "已保存至: " + path
        );

    }

    private void ExecuteLoad(string path)
    {

        using var file =
            FileAccess.Open(
                path,
                FileAccess.ModeFlags.Read
            );
        if (file == null)
            return;
        var json = new Json();
        if (json.Parse(file.GetAsText()) == Error.Ok)
        {

            var data =
                (Godot.Collections.Dictionary)json.Data;
            // 注意：
            // 存档里的成绩不能作为当前增幅比较基准
            _lastScore = 0;
            _lastGPA = 0;
            _lastCourseSnapshot = "";
            _hasLastResult = false;
            var courses =
                (Godot.Collections.Array)data["courses"];
            var courseList =
                GetNode<VBoxContainer>(
                    "SemesterTabs/SemesterPanel/CourseList"
                );

            foreach (var child in courseList.GetChildren())
            {

                child.QueueFree();

            }
            // 加载课程

            foreach (var course in courses)
            {

                var courseDict =
                    (Godot.Collections.Dictionary)course;
                var newRow =
                    CourseRowPrefab.Instantiate<CourseRow>();
                courseList.AddChild(newRow);
                newRow.Setup(
                    (string)courseDict["name"],

                    (float)courseDict["credits"],

                    (float)courseDict["score"]
                );

            }

            OnCalculatePressed();
            ShowStatus(
                "读取成功！"
            );

        }

    }

    private async void ShowStatus(string message)
    {
        var statusLabel =
            GetNode<Label>(
                "CalculationArea/StatusLabel"
            );
        statusLabel.Text = message;
        statusLabel.HorizontalAlignment =
            HorizontalAlignment.Center;
        statusLabel.VerticalAlignment =
            VerticalAlignment.Center;
        statusLabel.SetAnchorsPreset(
            Control.LayoutPreset.FullRect
        );

        statusLabel.Size =
            GetViewportRect().Size;

        statusLabel.AddThemeFontSizeOverride(
            "font_size",
            30
        );

        var styleBox =
            new StyleBoxFlat();

        styleBox.BgColor =
            new Color(
                0,
                0,
                0,
                0.7f
            );

        styleBox.CornerRadiusBottomLeft = 10;
        styleBox.CornerRadiusBottomRight = 10;
        styleBox.CornerRadiusTopLeft = 10;
        styleBox.CornerRadiusTopRight = 10;
        statusLabel.AddThemeStyleboxOverride(
            "normal",
            styleBox
        );

        statusLabel.Modulate =
            new Color(
                1,
                1,
                1,
                0
            );

        for (
            float t = 0;
            t <= 0.3f;
            t += 0.01f
        )
        {

            statusLabel.Modulate =
                new Color(
                    1,
                    1,
                    1,
                    t / 0.3f
                );


            await ToSignal(
                GetTree(),
                "process_frame"
            );

        }

        statusLabel.Modulate =
            new Color(
                1,
                1,
                1,
                1
            );

        await ToSignal(
            GetTree().CreateTimer(2.0f),
            "timeout"
        );

        for (
            float t = 0;
            t <= 0.3f;
            t += 0.01f
        )
        {

            statusLabel.Modulate =
                new Color(
                    1,
                    1,
                    1,
                    1 - t / 0.3f
                );


            await ToSignal(
                GetTree(),
                "process_frame"
            );

        }

        statusLabel.Modulate =
            new Color(
                1,
                1,
                1,
                0
            );

    }


    private void OnFileSelected(string path)
    {
        if (isSaving)
        {
            ExecuteSave(path);
        }
        else
        {
            ExecuteLoad(path);
        }

    }
    // 保存按钮

    private void OnSavePressed()
    {

        isSaving = true;
        var filePicker =
            GetNode<FileDialog>(
                "FilePicker"
            );

        filePicker.FileMode =
            FileDialog.FileModeEnum.SaveFile;

        filePicker.PopupCentered();

    }
    // 读取按钮

    private void OnLoadPressed()
    {

        isSaving = false;
        var filePicker =
            GetNode<FileDialog>(
                "FilePicker"
            );
        filePicker.FileMode =
            FileDialog.FileModeEnum.OpenFile;
        filePicker.PopupCentered();

    }

}