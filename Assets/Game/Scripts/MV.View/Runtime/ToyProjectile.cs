using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 던진 장난감(정본 draw2D L3804, 02 §2.1.4): 빌보드 아래에 마법탄 프리팹(`300 proj.prefab`, <see cref="ProjectileRig"/>)을 끼우고
    /// proj_Image에 등급 그림, 그 아래에 등급 꼬리·가운데 이펙트를 넣는다. 그림만 로컬 z 회전, 크기는 프리팹 그대로.
    /// </summary>
    public sealed class ToyProjectile : ProjectileSlot
    {
        ProjectileRig _rig;

        /// <summary>
        /// 자리·그림을 맞춘다. 마법탄 프리팹이나 그림이 아직 없으면 숨기고 false.
        /// </summary>
        /// <param name="rigPrefab">마법탄 프리팹(처음 한 번 인스턴스로 만든다).</param>
        /// <param name="imageMaterial">proj_Image 머티리얼(월드 스프라이트와 같게, null이면 프리팹 그대로).</param>
        public bool Place(GameObject rigPrefab, string rigName, Material imageMaterial, Sprite sprite,
            string tail, GameObject tailPrefab, string center, GameObject centerPrefab, float spinDeg, Vector3 pos)
        {
            if (_rig == null && rigPrefab != null)
            {
                var go = Instantiate(rigPrefab, _billboard, false);
                go.name = rigName;
                _rig = go.GetComponent<ProjectileRig>();
                if (_rig != null && _rig.Image != null && imageMaterial != null) _rig.Image.sharedMaterial = imageMaterial;
            }
            if (_rig == null || sprite == null)
            {
                Hide();
                return false;
            }
            _rig.SetSprite(sprite);
            _rig.SetTail(tail, tailPrefab);
            _rig.SetCenter(center, centerPrefab);
            _rig.SetSpin(spinDeg);
            ShowAt(pos);
            if (Fresh)
            {
                Fresh = false;
                _rig.ResetTrails();
            }
            return true;
        }
    }
}
