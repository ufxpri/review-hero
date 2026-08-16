// Godot 런타임에 닿는 자리를 한 곳에 모은다.
//
// RunStore 는 규칙과 저장만 담당하는데, 저장 경로(user://)와 로그는 엔진 API 다.
// 여기로 모아 두면 ① 규칙 코드에 Godot 호출이 흩어지지 않고, ② 엔진이 안 떠 있는
// 상황(순수 콘솔 하네스)에서도 임시 경로로 떨어져 죽지 않는다.

namespace ReviewHero.Game.Run;

public static class Platform
{
    /// <summary>user:// 같은 Godot 가상 경로를 실제 파일 경로로 바꾼다</summary>
    public static string GlobalizePath(string path)
    {
        try
        {
            string real = Godot.ProjectSettings.GlobalizePath(path);
            if (!string.IsNullOrEmpty(real)) return real;
        }
        catch
        {
            // Godot 런타임 밖 — 아래 대체 경로로 떨어진다
        }
        string name = path.Replace("user://", "").Replace("res://", "");
        return Path.Combine(Path.GetTempPath(), "reviewhero", name);
    }

    public static void Print(string msg)
    {
        try { Godot.GD.Print(msg); }
        catch { Console.WriteLine(msg); }
    }
}
