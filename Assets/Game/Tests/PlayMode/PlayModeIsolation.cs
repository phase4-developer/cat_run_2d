using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MV.Tests
{
    /// <summary>
    /// PlayMode 시험끼리 남기는 상태를 끊는다. 게임은 한 번만 부팅하지만 시험은 시험마다 Boot 씬을 다시 열어 부팅한다.
    /// 이펙트 관리자(DontDestroyOnLoad 싱글턴)가 앞 시험의 풀·로드를 들고 있으면 다음 부팅의 공급자 지정이
    /// 「Configure the provider before loading effects.」로 막힌다. 그래서 시험 끝마다 게임 씬을 먼저 닫고(루프가 연출 재생기를 내린다)
    /// 관리자를 없애 다음 부팅이 새로 만들게 한다.
    /// 이 시험 어셈블리는 이펙트 패키지를 참조하지 않으므로(asmdef는 통합 Writer) 형식 이름으로 찾는다.
    /// </summary>
    internal static class PlayModeIsolation
    {
        const string EffectManagerType = "jjevol.Effect.EffectManager, JJEvol.Effects.Runtime";

        /// <summary>[UnityTearDown]에서 부른다. 빈 씬 하나만 남기고 나머지 씬을 닫은 뒤 이펙트 관리자를 없앤다.</summary>
        public static IEnumerator EndGame()
        {
            var empty = SceneManager.CreateScene("PlayModeIsolation_" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            var others = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s != empty && s.isLoaded) others.Add(s);
            }
            foreach (var s in others)
            {
                var op = SceneManager.UnloadSceneAsync(s);
                while (op != null && !op.isDone) yield return null;
            }
            yield return null;   // 파괴된 루프의 OnDestroy가 끝난 뒤

            var type = Type.GetType(EffectManagerType);
            if (type == null) throw new InvalidOperationException("이펙트 관리자 형식을 찾지 못했다: " + EffectManagerType);
            var found = UnityEngine.Object.FindFirstObjectByType(type);
            if (found is Component c) UnityEngine.Object.DestroyImmediate(c.gameObject);
            yield return null;
        }
    }
}
