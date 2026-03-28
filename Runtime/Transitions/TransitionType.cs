namespace Ursa.Transitions
{
    /// <summary>
    /// 基本的なトランジションタイプ Enum。
    /// （ユーザーが独自で拡張する場合は、手動で追加するか別機構を用いる）
    /// </summary>
    public enum TransitionType
    {
        Default,
        Fade,
        Wipe,
        Circle,
        Dissolve,
        Animator
    }
}
