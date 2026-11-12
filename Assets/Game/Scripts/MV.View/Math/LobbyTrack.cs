using System.Collections.Generic;
using MV.Run;

namespace MV.View
{
    /// <summary>
    /// 로비 트랙(MV-SPEC-02 01 §3 「냥법사가 트랙 위에 서 있다 … 로비와 인게임이 같은 화면」, 정본 `toLobby` L2472~2479, MV-SPEC-01 04 §4.13.1):
    /// 다음 런과 같은 레벨 배치에서 시작점부터 `lobby.objZMax`(m) 안의 오브젝트만 깐다.
    /// </summary>
    public static class LobbyTrack
    {
        /// <summary><paramref name="objs"/> 중 z &lt; baseZ + zMax인 것을 <paramref name="outList"/>에 넣는다(순서 유지).</summary>
        public static void Pick(IReadOnlyList<RunObject> objs, double baseZ, double zMax, List<RunObject> outList)
        {
            outList.Clear();
            if (objs == null) return;
            double end = baseZ + zMax;
            for (int i = 0; i < objs.Count; i++)
                if (objs[i].Z < end) outList.Add(objs[i]);
        }
    }
}
