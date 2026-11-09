using UnityEngine;

namespace MV.Fx
{
    /// <summary>이펙트 기본 종류(06 §6.3 표 1~14, 04_effect 그룹 001). 프리팹 1개 = 종류 1개.</summary>
    public enum FxKind { Sparks, Debris, Dust, Trail, Impact, Boom, Bolt, Fireball, Paw, Hiss, BrickChunk, HolyBeam, Meteor, Frost }

    /// <summary>
    /// 기본 종류 1회 재생 요청(정본 함수 호출 1번). 값이 NaN이면 프리팹 기본값(= 04_effect 그룹 001 행)을 쓴다.
    /// 위치는 게임 좌표(z 전진 +, y 지면 기준 절대 높이).
    /// </summary>
    public struct FxSpawn
    {
        /// <summary>04_effect 기본 종류 key(`fx.sparks` …).</summary>
        public string Key;
        public Vector3 Pos;
        /// <summary>번개 끝점·화염구 착지점 등 두 번째 점.</summary>
        public Vector3 To;
        public bool HasColor;
        public Color32 Color;
        public float Count, Speed, Size, Life, Grav, Radius;
        public bool Crit;
        public bool NoRing;
        /// <summary>종류별 변형: trail 0 기타 / 1 fire / 2 bolt, dust 1 = 신 폭풍 불구름, brickChunk 벽돌 색 순번.</summary>
        public int Variant;
        /// <summary>벽돌 덩이 튀는 x 방향(±1).</summary>
        public float Dir;

        public static FxSpawn Of(string key, Vector3 pos) => new FxSpawn
        {
            Key = key, Pos = pos, To = pos, Count = float.NaN, Speed = float.NaN, Size = float.NaN, Life = float.NaN,
            Grav = float.NaN, Radius = float.NaN, Dir = 1f,
        };

        public FxSpawn WithColor(Color32 c) { HasColor = true; Color = c; return this; }

        public override string ToString() =>
            $"{Key}@({Pos.x:0.##},{Pos.y:0.##},{Pos.z:0.##}){(HasColor ? " #" + ColorUtility.ToHtmlStringRGB(Color) : "")} n={Count} spd={Speed} size={Size} life={Life} grav={Grav} r={Radius}{(Crit ? " crit" : "")}{(NoRing ? " noRing" : "")}";
    }

    /// <summary>
    /// <see cref="FxSpawn"/>을 Framework 요청 너머의 드라이버에 넘기는 고리 버퍼. `EffectRequest.skinIndex`에 토큰을 싣고
    /// 드라이버의 `SetSkin`이 꺼낸다(Framework 요청에 사용자 값 칸이 없어서).
    /// Framework 대체: effects 고정본에 요청별 사용자 데이터 칸이 생기면 이 버퍼를 그 칸으로 바꾼다.
    /// </summary>
    public static class MvFxParamBank
    {
        const int Size = 1024;
        static readonly FxSpawn[] s_items = new FxSpawn[Size];
        static readonly int[] s_tokens = new int[Size];
        static int s_next = 1;

        public static int Put(in FxSpawn spawn)
        {
            int token = s_next++;
            if (s_next == int.MaxValue) s_next = 1;
            int i = token & (Size - 1);
            s_items[i] = spawn;
            s_tokens[i] = token;
            return token;
        }

        /// <summary>토큰이 덮어써졌거나 0이면 false(프리팹 기본값으로 재생).</summary>
        public static bool TryGet(int token, out FxSpawn spawn)
        {
            int i = token & (Size - 1);
            if (token > 0 && s_tokens[i] == token) { spawn = s_items[i]; return true; }
            spawn = default;
            return false;
        }
    }
}
