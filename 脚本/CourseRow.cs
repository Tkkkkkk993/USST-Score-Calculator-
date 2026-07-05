using Godot;

public partial class CourseRow : HBoxContainer
{
    public override void _Ready()
    {
        // 绑定删除按钮信号，点击即销毁这行数据
        GetNode<Button>("DeleteButton").Pressed += () => QueueFree();
    }

    public Godot.Collections.Dictionary GetRowData()
    {
        // 获取输入的文本并去除两端空格
        string creditText = GetNode<LineEdit>("CreditInput").Text.Trim();
        string scoreText = GetNode<LineEdit>("ScoreInput").Text.Trim();

        // 使用 float.TryParse: 
        // 如果转换成功，结果存入 credits/score；如果失败（如空字符串），则设为 0
        float.TryParse(creditText, out float credits);
        float.TryParse(scoreText, out float score);

        return new Godot.Collections.Dictionary
        {
            {"name", GetNode<LineEdit>("NameInput").Text.Trim()},
            {"credits", credits},
            {"score", score}
        };
    }

    public void Setup(string name, float credits, float score)
    {
        GetNode<LineEdit>("NameInput").Text = name;
        GetNode<LineEdit>("CreditInput").Text = credits.ToString();
        GetNode<LineEdit>("ScoreInput").Text = score.ToString();
    }
}