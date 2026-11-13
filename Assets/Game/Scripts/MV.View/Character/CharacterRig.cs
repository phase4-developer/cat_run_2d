using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 캐릭터 프리팹 루트(디자이너 PDF `Local/Character/Prefab/Char_*.prefab`, MV-SPEC-02 ArtResourcePlan §5).
    /// 그림 SpriteRenderer(몸)를 들고, 그림·내려감·흔들림·세로축 회전·배율만 바꾼다. 크기·기준점은 그림의 피벗·PPU가 정하고, 배율은 표 `116_charLook.scale`.
    /// 무기·효과처럼 프리팹에 따로 붙는 스크립트는 이 루트 아래에 둔다. 위치·정렬은 <see cref="CharacterPresenter"/>가 맞춘다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterRig : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _body;
        /// <summary>정산 바퀴(프리팹 `stair_wheel`, 몸 아래 자식이라 몸과 함께 기울고 튄다). 없는 프리팹(분신)은 비워 둔다.</summary>
        [SerializeField] SpriteRenderer _wheel;

        Sprite _sprite, _wheelSprite;
        bool _back, _wheelOn = true;
        float _dip = float.NaN, _sway = float.NaN, _roll = float.NaN, _scale = float.NaN, _scaleX = float.NaN, _wheelDeg = float.NaN;

        /// <summary>이 인스턴스를 꺼낸 프리팹 주소(풀이 적는다).</summary>
        public string Address { get; internal set; }
        public SpriteRenderer Body
        {
            get
            {
                if (_body == null) _body = GetComponentInChildren<SpriteRenderer>(true);
                return _body;
            }
        }

        public SpriteRenderer Wheel => _wheel;

        /// <summary>
        /// 그림과 한 프레임 움직임을 바꾼다. 루트(카메라 평면 빌보드) 배율 = (scale·flipX, scale): 세로축 회전은 무기 등 루트 아래 전부에 걸린다.
        /// 몸은 루트 안에서 (좌우, −내려감)으로 옮기고 기울기만큼 돌린다(화면 시계 방향 +). 같은 값이면 건드리지 않는다.
        /// 뒷면 그림(<see cref="CharPose.HappyBack"/>)은 루트가 좌우로 뒤집힌 동안 보이므로 몸 그림만 다시 뒤집어 그린 방향 그대로 보인다.
        /// 바퀴는 <paramref name="wheelSprite"/>가 있고 <see cref="CharFrame.HasWheel"/>일 때만 켜고, 누적 회전 수만큼 시계 방향으로 돌린다.
        /// </summary>
        public void Apply(Sprite sprite, in CharFrame frame, double scale, Sprite wheelSprite = null)
        {
            var body = Body;
            if (body == null) return;
            if (_sprite != sprite) { _sprite = sprite; body.sprite = sprite; }
            bool back = frame.Pose == CharPose.HappyBack;
            if (back != _back) { _back = back; body.flipX = back; }
            ApplyWheel(frame, wheelSprite);
            float d = (float)frame.Dip, x = (float)frame.SwayX, r = (float)frame.Roll;
            if (d != _dip || x != _sway)
            {
                _dip = d;
                _sway = x;
                var lp = body.transform.localPosition;
                body.transform.localPosition = new Vector3(x, -d, lp.z);
            }
            if (r != _roll)
            {
                _roll = r;
                body.transform.localRotation = Quaternion.Euler(0f, 0f, -r * Mathf.Rad2Deg);
            }
            float s = (float)scale, sx = (float)(scale * frame.FlipX);
            if (s != _scale || sx != _scaleX)
            {
                _scale = s;
                _scaleX = sx;
                transform.localScale = new Vector3(sx, s, 1f);
            }
        }

        void ApplyWheel(in CharFrame frame, Sprite wheelSprite)
        {
            if (_wheel == null) return;
            bool on = wheelSprite != null && frame.HasWheel;
            if (on != _wheelOn)
            {
                _wheelOn = on;
                _wheel.enabled = on;
            }
            if (!on) return;
            if (_wheelSprite != wheelSprite) { _wheelSprite = wheelSprite; _wheel.sprite = wheelSprite; }
            float deg = (float)(-(frame.WheelTurns % 1.0) * 360.0);   // 시계 방향 = z 음수
            if (deg != _wheelDeg)
            {
                _wheelDeg = deg;
                _wheel.transform.localRotation = Quaternion.Euler(0f, 0f, deg);
            }
        }

        /// <summary>풀에 돌려줄 때: 그림을 비워 다음에 다시 맞추게 한다.</summary>
        public void Clear()
        {
            _sprite = null;
            _wheelSprite = null;
            _dip = _sway = _roll = _scale = _scaleX = _wheelDeg = float.NaN;
            if (Body != null)
            {
                Body.sprite = null;
                Body.flipX = false;
            }
            _back = false;
            if (_wheel != null)
            {
                _wheel.sprite = null;
                _wheel.enabled = false;
            }
            _wheelOn = false;
        }

        void Awake()
        {
            // 프리팹에 켜진 채 저장된 바퀴는 정산 전까지 끈다.
            if (_wheel != null) _wheel.enabled = false;
            _wheelOn = false;
        }
    }
}
