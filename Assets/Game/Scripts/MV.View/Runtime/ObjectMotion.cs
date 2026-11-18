using System;
using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 맞음 눌림(punch)과 종이 쓰러짐(paperDie)(새 HTML 2026-11-13 연출 정본, updObjMesh L1468·L3663). 그림 묶음(<see cref="RunObjectView.Pose"/>)만 움직인다.
    /// 눌림 시작값 = `106_hitReaction.punch`(Hit 이벤트), 쓰러짐 = Break 이벤트. 쓰러지는 <see cref="ObjectReaction.DieSec"/>초 동안 회수를 막는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectMotion : RunObjectModule
    {
        double _punch;
        double _die = -1;
        int _dieDir;

        /// <summary>맞음 눌림 k(106.punch에서 시작, 초당 9씩 줄어듦).</summary>
        public double Punch => _punch;
        /// <summary>쓰러짐 경과 초(−1 = 살아 있음).</summary>
        public double Die => _die;

        public override bool TicksWhileDying => true;
        public override bool HoldsRelease => _die >= 0 && _die < ObjectReaction.DieSec;

        public override void OnBind(RunObjectView view)
        {
            _punch = 0;
            _die = -1;
        }

        public override void OnUnbind(RunObjectView view) => OnBind(view);

        public override void OnRunEvent(RunObjectView view, in RunEvent e)
        {
            if (e.Type == RunEventType.Hit)
            {
                var r = view.Obj?.Reaction;
                if (r != null && r.punch > 0 && _die < 0) _punch = r.punch;   // 새 HTML hit: o.punch = 106.punch
            }
            else if (e.Type == RunEventType.Break && _die < 0 && view.Obj != null)
            {
                _die = 0;
                _dieDir = ObjectReaction.DieDir(view.Obj.Z, view.Obj.X);
                _punch = 0;
                view.Dying = true;
            }
        }

        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            var pose = view.Pose;
            if (_die >= 0)
            {
                _die += f.Dt;
                double k = Math.Min(1, _die / ObjectReaction.DieSec);
                ObjectReaction.DiePose(k, _dieDir, out double hop, out double sx, out double sy, out double roll, out double alpha);
                pose.Offset += new Vector3(0, (float)hop, 0);
                pose.Scale = Vector3.Scale(pose.Scale, new Vector3((float)sx, (float)sy, 1));
                pose.Roll = (float)(-roll * 180 / Math.PI);   // 정본 캔버스 회전(시계 +) → Unity z 회전(반시계 +)
                pose.Alpha *= (float)alpha;
                return;
            }
            if (_punch > 0) _punch = ObjectReaction.PunchStep(_punch, f.Dt);
            pose.Scale = Vector3.Scale(pose.Scale, PunchScale(_punch));
        }

        /// <summary>맞음 눌림 크기 (1 + 0.07k, 1 − 0.08k, 1 + 0.07k)(updObjMesh L1468).</summary>
        public static Vector3 PunchScale(double k)
        {
            float g = (float)(k * ObjectReaction.PunchGrow), s = (float)(k * ObjectReaction.PunchSquash);
            return new Vector3(1 + g, 1 - s, 1 + g);
        }
    }
}
