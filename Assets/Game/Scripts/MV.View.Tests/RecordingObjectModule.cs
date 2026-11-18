using System.Collections.Generic;
using MV.Run;

namespace MV.View.Tests
{
    /// <summary>시험용 모듈: 관리자가 모르는 새 모듈이 받은 이벤트를 적는다(모듈 확장 때 관리자를 고치지 않아도 되는지 확인).</summary>
    public sealed class RecordingObjectModule : RunObjectModule
    {
        public readonly List<RunEventType> Got = new List<RunEventType>();
        public readonly List<RunObject> GotFor = new List<RunObject>();

        public override void OnRunEvent(RunObjectView view, in RunEvent e)
        {
            Got.Add(e.Type);
            GotFor.Add(view.Obj);
        }
    }
}
