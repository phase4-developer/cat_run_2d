namespace MV.View
{
    /// <summary>
    /// 결계문·마법진 포털 흐려짐(<see cref="WarpGateLayout.Alpha"/>): 플레이어가 문 가까이 오면 흐려지고 지나가면 숨긴다(카메라를 가리지 않게).
    /// </summary>
    [UnityEngine.DisallowMultipleComponent]
    public sealed class ArchFade : RunObjectModule
    {
        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            bool shown = WarpGateLayout.Alpha(f.PlayerZ - view.Obj.Z, out double a);
            view.Pose.Alpha *= shown ? (float)a : 0f;
        }
    }
}
