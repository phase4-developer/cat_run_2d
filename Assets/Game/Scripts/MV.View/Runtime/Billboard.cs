using System.Collections.Generic;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 카메라 평면 빌보드(02 §2.1.3): 회전 = 카메라 회전 복사(구형 LookAt 아님). <see cref="DepthSorter"/>가 매 프레임
    /// 카메라 갱신 뒤 <see cref="SyncAll"/>로 맞춘다. 로컬 z 회전(장난감 등)은 자식에 둔다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Billboard : MonoBehaviour
    {
        static readonly List<Billboard> s_all = new List<Billboard>(256);
        int _index = -1;

        public static int Count => s_all.Count;

        void OnEnable()
        {
            _index = s_all.Count;
            s_all.Add(this);
        }

        void OnDisable()
        {
            // 끝 원소를 빈자리로 옮겨 O(1) 제거.
            int last = s_all.Count - 1;
            if (_index >= 0 && _index <= last)
            {
                var moved = s_all[last];
                s_all[_index] = moved;
                moved._index = _index;
                s_all.RemoveAt(last);
            }
            _index = -1;
        }

        public static void SyncAll(Quaternion cameraRotation)
        {
            for (int i = 0; i < s_all.Count; i++) s_all[i].transform.rotation = cameraRotation;
        }
    }
}
