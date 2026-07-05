using Godot;
using System.Collections.Generic;

public partial class MainBoard : Control
{
    [Export] public PackedScene CourseRowPrefab;

    // --- 新增：类成员变量，用于跨函数访问对比数据 ---
    private float _lastScore = 0.0f;
    private float _lastGPA = 0.0f;
    public override void _Ready()
    {
        GetNode<Button>("CalculationArea/CalculateButton").Pressed += OnCalculatePressed;
        GetNode<Button>("AddCourseButton").Pressed += OnAddCoursePressed;

        var filePicker = GetNode<FileDialog>("FilePicker");
        filePicker.Theme = GD.Load<Theme>("res://样式/t2.tres");
        filePicker.FileSelected += OnFileSelected;
    }

    private bool isSaving = false;

    private void OnAddCoursePressed()
    {
        var newRow = CourseRowPrefab.Instantiate();
        GetNode<VBoxContainer>("SemesterTabs/SemesterPanel/CourseList").AddChild(newRow);
    }

    private void OnCalculatePressed()
    {
        var courseList = GetNode<VBoxContainer>("SemesterTabs/SemesterPanel/CourseList");
        float totalCredits = 0;
        List<float> scores = new List<float>();
        List<float> credits = new List<float>();

        foreach (var child in courseList.GetChildren())
        {
            if (child is CourseRow row)
            {
                var data = row.GetRowData();
                float s = (float)data["score"];
                float c = (float)data["credits"];
                string name = (string)data["name"];

                if (!string.IsNullOrEmpty(name) || c > 0 || s > 0)
                {
                    scores.Add(s);
                    credits.Add(c);
                    totalCredits += c;
                }
            }
        }

        float avgScore = GradeCalculator.CalculateWeightedAverageScore(scores, credits);
        float avgGPA = GradeCalculator.CalculateWeightedGPA(scores, credits);

        // 更新基础 UI
        GetNode<Label>("CalculationArea/TotalCreditsLabel").Text = $"当前总学分为：{totalCredits:F1}";
        GetNode<Label>("CalculationArea/ScoreLabel").Text = $"加权均分: {avgScore:F2}";
        GetNode<Label>("CalculationArea/GPALabel").Text = $"加权GPA: {avgGPA:F2}";

        // --- 新增：历史对比 UI 更新 ---
        UpdateGrowthUI(avgScore, avgGPA);
    }

    private void UpdateGrowthUI(float currentScore, float currentGPA)
    {
        var growthLabel = GetNode<Label>("CalculationArea/GrowthLabel");
        if (_lastScore > 0 || _lastGPA > 0)
        {
            float sDiff = currentScore - _lastScore;
            float sPct = (_lastScore > 0) ? (sDiff / _lastScore) * 100 : 0;
            float gDiff = currentGPA - _lastGPA;
            float gPct = (_lastGPA > 0) ? (gDiff / _lastGPA) * 100 : 0;

            growthLabel.Text = $"均分: {sDiff:+0.0;-0.0;0}({sPct:+0.0;-0.0;0}%) | 绩点: {gDiff:+0.00;-0.00;0}({gPct:+0.0;-0.0;0}%)";
            growthLabel.Modulate = (sDiff >= 0 && gDiff >= 0) ? new Color(0, 1, 0) : new Color(1, 0, 0);
        }
        else
        {
            growthLabel.Text = "暂无历史记录对比";
        }
    }

    private void ExecuteSave(string path)
    {
        var allData = new Godot.Collections.Array();
        var courseList = GetNode<VBoxContainer>("SemesterTabs/SemesterPanel/CourseList");

        foreach (var child in courseList.GetChildren())
        {
            if (child is CourseRow row) allData.Add(row.GetRowData());
        }

        // 计算当前均分与绩点并存入存档
        float totalCredits = 0;
        List<float> scores = new List<float>();
        List<float> credits = new List<float>();
        foreach (var child in courseList.GetChildren())
        {
            if (child is CourseRow row)
            {
                var data = row.GetRowData();
                scores.Add((float)data["score"]);
                credits.Add((float)data["credits"]);
            }
        }
        
        var finalData = new Godot.Collections.Dictionary { 
            { "courses", allData },
            { "last_score", GradeCalculator.CalculateWeightedAverageScore(scores, credits) },
            { "last_gpa", GradeCalculator.CalculateWeightedGPA(scores, credits) }
        };

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(Json.Stringify(finalData));
        ShowStatus("已保存至: " + path);
    }

	private void ExecuteLoad(string path)
		{
			using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			if (file == null) return; // 增加防错

			var json = new Json();
			if (json.Parse(file.GetAsText()) == Error.Ok)
			{
				var data = (Godot.Collections.Dictionary)json.Data;
				
				// 1. 读取历史值到成员变量 (如果没找到，默认保持为 0)
				_lastScore = data.ContainsKey("last_score") ? (float)data["last_score"] : 0.0f;
				_lastGPA = data.ContainsKey("last_gpa") ? (float)data["last_gpa"] : 0.0f;

				var courses = (Godot.Collections.Array)data["courses"];
				var courseList = GetNode<VBoxContainer>("SemesterTabs/SemesterPanel/CourseList");
				
				// 2. 清空旧列表
				foreach (var child in courseList.GetChildren()) child.QueueFree();

				// 3. 填充新列表
				foreach (var course in courses)
				{
					var courseDict = (Godot.Collections.Dictionary)course;
					var newRow = CourseRowPrefab.Instantiate<CourseRow>();
					courseList.AddChild(newRow);
					newRow.Setup((string)courseDict["name"], (float)courseDict["credits"], (float)courseDict["score"]);
				}
				
				// 4. 【核心修复】读取数据后，立即强制刷新一次计算结果与对比 UI
				OnCalculatePressed(); 
				
				ShowStatus("读取成功！");
			}
		}

	private async void ShowStatus(string message)
	{
		// 直接获取现有 Label，设置居中文本
		var statusLabel = GetNode<Label>("CalculationArea/StatusLabel");
		statusLabel.Text = message;
		
		// 设置文字居中
		statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
		statusLabel.VerticalAlignment = VerticalAlignment.Center;
		
		// 让 Label 覆盖整个屏幕实现居中
		statusLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		statusLabel.Size = GetViewportRect().Size;
		
		// 设置字体大小
		statusLabel.AddThemeFontSizeOverride("font_size", 30);
		
		// 添加半透明背景
		var styleBox = new StyleBoxFlat();
		styleBox.BgColor = new Color(0, 0, 0, 0.7f);
		styleBox.CornerRadiusBottomLeft = 10;
		styleBox.CornerRadiusBottomRight = 10;
		styleBox.CornerRadiusTopLeft = 10;
		styleBox.CornerRadiusTopRight = 10;
		statusLabel.AddThemeStyleboxOverride("normal", styleBox);
		
		// 初始透明度为0
		statusLabel.Modulate = new Color(1, 1, 1, 0);
		
		// 渐显
		for (float t = 0; t <= 0.3f; t += 0.01f)
		{
			statusLabel.Modulate = new Color(1, 1, 1, t / 0.3f);
			await ToSignal(GetTree(), "process_frame");
		}
		statusLabel.Modulate = new Color(1, 1, 1, 1);
		
		// 显示2秒
		await ToSignal(GetTree().CreateTimer(2.0f), "timeout");
		
		// 渐隐
		for (float t = 0; t <= 0.3f; t += 0.01f)
		{
			statusLabel.Modulate = new Color(1, 1, 1, 1 - t / 0.3f);
			await ToSignal(GetTree(), "process_frame");
		}
		statusLabel.Modulate = new Color(1, 1, 1, 0);
	}

	// 2. 添加 OnFileSelected 方法，处理信号触发后的逻辑
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

	// 3. 修改你的保存和读取调用，确保它们设置了 isSaving 状态
	private void OnSavePressed() // 假设这是你的保存按钮绑定的函数
	{
		isSaving = true;
		var filePicker = GetNode<FileDialog>("FilePicker");
		filePicker.FileMode = FileDialog.FileModeEnum.SaveFile;
		filePicker.PopupCentered();
	}

	private void OnLoadPressed() // 假设这是你的读取按钮绑定的函数
	{
		isSaving = false;
		var filePicker = GetNode<FileDialog>("FilePicker");
		filePicker.FileMode = FileDialog.FileModeEnum.OpenFile;
		filePicker.PopupCentered();
	}
}