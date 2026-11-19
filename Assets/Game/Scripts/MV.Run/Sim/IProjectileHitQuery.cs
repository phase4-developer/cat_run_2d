using System.Collections.Generic;

namespace MV.Run
{
    /// <summary>투사체 이동 구간이 지나는 대상 하나(<see cref="IProjectileHitQuery.Cast"/> 결과).</summary>
    public readonly struct ProjectileHit
    {
        public readonly RunObject Obj;
        /// <summary>판정 형상에 처음 닿을 때 투사체 위치 z. 시작점이 이미 형상에 닿아 있으면 구간 시작 z.</summary>
        public readonly double Z;

        public ProjectileHit(RunObject obj, double z)
        {
            Obj = obj;
            Z = z;
        }
    }

    /// <summary>
    /// 투사체 공간 충돌 검출. 판정 형상의 정본은 오브젝트 프리팹 안 Collider이고(Unity Physics), 구현은 화면 쪽(MV.View `RunHitWorld`)이 한다.
    /// 피해·관통·같은 대상 중복 방지·보상은 <see cref="RunSim"/>이 정한다. 이 질의는 「어디에 닿는가」만 답한다.
    /// </summary>
    public interface IProjectileHitQuery
    {
        /// <summary>틱마다 투사체 단계 직전에 한 번: 판정 형상을 지금 시뮬 위치로 옮긴다.</summary>
        void Prepare(RunState s);

        /// <summary>
        /// x에서 z0 → z1로 움직이는 투사체 구간이 닿는 대상을 <paramref name="hits"/>에 채운다. 대상마다 한 번, 진입 z 오름차순(같으면 Id 오름차순).
        /// 투사체 굵기(판정 구)는 종류(<paramref name="spear"/> = 창, 아니면 던진 장난감)에 따라 질의 구현이 정한다(투사체 프리팹 `Hitbox`).
        /// 피격 가능 여부(<c>Hittable</c>)와 이미 맞힌 대상은 거르지 않는다.
        /// </summary>
        void Cast(RunState s, double x, double z0, double z1, bool spear, List<ProjectileHit> hits);
    }
}
