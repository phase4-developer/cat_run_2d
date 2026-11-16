using System.Globalization;
using MV.Run;

namespace MV.UI
{
    /// <summary>
    /// 런 안내 이벤트(<see cref="RunEventType.Toast"/>) 문구. 번역 키(ui 모음) 그대로, 번역이 없으면 새 HTML `toast` 문구.
    /// </summary>
    public static class RunToastText
    {
        public const string NoStairs = "run.toast.noStairs", NoCard = "run.toast.noCard";

        /// <summary>안내 문구. 모르는 키면 키 그대로.</summary>
        public static string Of(in RunEvent e)
        {
            switch (e.Key)
            {
                case NoStairs: return MvUiText.Get("ui", NoStairs, "모은 마나가 없습니다");
                case NoCard:
                    return string.Format(CultureInfo.InvariantCulture, MvUiText.Get("ui", NoCard, "포탈 {0} · 5 미만이라 냥파워 없음"), e.A);
                default: return MvUiText.Get("ui", e.Key, e.Key);
            }
        }
    }
}
