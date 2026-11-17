using System;
using UnityEngine;

namespace MV.Tests
{
    /// <summary>
    /// PlayMode 시험끼리 남기는 상태를 끊는다. 게임은 한 번만 부팅하지만 시험은 시험마다 Boot 씬을 다시 열어 부팅한다.
    /// 이펙트 관리자(DontDestroyOnLoad 싱글턴)가 앞 시험의 풀·로드를 들고 있으면 다음 부팅의 공급자 지정이
    /// 「Configure the provider before loading effects.」로 막힌다. 그래서 시험 끝마다 관리자를 없애 다음 부팅이 새로 만들게 한다.
    /// 이 시험 어셈블리는 이펙트 패키지를 참조하지 않으므로(asmdef는 통합 Writer) 형식 이름으로 찾는다.
    /// </summary>
    internal static class PlayModeIsolation
    {
        const string EffectManagerType = "jjevol.Effect.EffectManager, JJEvol.Effects.Runtime";

        public static void ResetEffects()
        {
            var type = Type.GetType(EffectManagerType);
            if (type == null) throw new InvalidOperationException("이펙트 관리자 형식을 찾지 못했다: " + EffectManagerType);
            var found = UnityEngine.Object.FindFirstObjectByType(type);
            if (found is Component c) UnityEngine.Object.DestroyImmediate(c.gameObject);
        }
    }
}
