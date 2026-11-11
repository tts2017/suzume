namespace Suzume.Core;

/// <summary>
/// 制作のウィンドウの工数から、制作が完成した瞬間を見つける。
/// 工数が最大に達したときに 1 回だけ知らせ、ウィンドウが閉じたら次の制作に備える。
/// 失敗 (工数が足りないまま閉じる) は知らせない。
/// </summary>
public sealed class CraftCompletion
{
    private bool reported;

    /// <param name="open">通常制作のウィンドウが開いている</param>
    /// <param name="progress">今の工数</param>
    /// <param name="maxProgress">完成に必要な工数 (分からなければ 0)</param>
    /// <returns>この呼び出しで制作が完成した</returns>
    public bool Update(bool open, int progress, int maxProgress)
    {
        if (!open)
        {
            reported = false;
            return false;
        }

        if (reported || maxProgress <= 0 || progress < maxProgress)
            return false;

        reported = true;
        return true;
    }
}
