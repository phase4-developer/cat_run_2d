using System.Threading.Tasks;
using MV.Data;
using MV.Meta;
using MV.Run;
using MV.Save;
using UnityEngine;

namespace MV.Boot
{
    /// <summary>
    /// 부팅 4·6단계 뒤의 런 코어 연결(10 §10.7, MV-G3-INT).
    /// 4단계 표 검증에 MV.Run의 컴포넌트 구현 목록을 넘기고(MV.Data는 MV.Run을 모른다),
    /// 6단계에서 저장이 열리면 영속 변경 진입점 <see cref="MvMeta.Current"/>를 만든다.
    /// </summary>
    public static class MvBootMeta
    {
        static MvTables _tables;

        /// <summary>Boot가 마지막으로 표 로드에 넘긴 검증 옵션. 시험이 읽는다.</summary>
        public static MvValidationOptions TableOptions { get; private set; }

        /// <summary><see cref="MvMeta.Current"/>가 있다(저장이 한 번이라도 열렸다).</summary>
        public static bool HasMeta => MvMeta.Current != null;

        /// <summary><see cref="MvMeta.Current"/>가 명령을 받을 수 있다(저장소·재화 서비스가 열려 있다).</summary>
        public static bool MetaReady => MvMeta.Current != null && MvMeta.Current.IsReady;

        /// <summary><see cref="MvMeta.Current"/>가 지금 저장소·재화 서비스·표를 본다.</summary>
        public static bool MetaUsesCurrent =>
            MvMeta.Current != null
            && ReferenceEquals(MvMeta.Current.Store, MvSave.Current)
            && ReferenceEquals(MvMeta.Current.Tables, MvTables.Current);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _tables = null;
            TableOptions = null;
        }

        /// <summary>4단계 표 검증 옵션: 105 컴포넌트 type이 MV.Run에 구현됐는지 확인한다(MV-RUN-SIM 요청).</summary>
        public static MvValidationOptions CreateTableOptions()
        {
            TableOptions = new MvValidationOptions { IsComponentImplemented = RunComponents.IsImplemented };
            return TableOptions;
        }

        /// <summary>4단계: 기본 원본에서 표를 읽고 위 옵션으로 검증한다. 검증 오류면 <see cref="MvTableValidationException"/>.</summary>
        public static Task<MvTables> LoadTablesAsync() =>
            MvTableLoader.LoadAsync(MvTableLoader.DefaultSource(), CreateTableOptions());

        /// <summary>
        /// 6단계(<see cref="MvBootSave.Open"/>) 뒤에 부른다. 저장이 열려 있으면 바로 <see cref="MvMeta.Current"/>를 만든다.
        /// 저장을 못 열었으면(복구·업데이트·저장소 팝업 대기) <see cref="MvMeta.Current"/>는 null로 두고 기다린다 —
        /// 진행을 바꾸는 화면은 null이면 막는다. 팝업의 「다시 시도」·「새로 시작」이 저장을 열면
        /// (<see cref="MvSave.CurrentChanged"/>) 그때 만든다. 만든 뒤 저장을 다시 열어도 같은 인스턴스를 쓴다
        /// (저장소·재화 서비스를 공급 함수로 매번 읽고, 닫힌 동안은 <see cref="MvMeta.IsReady"/>가 false).
        /// </summary>
        public static void Attach(MvTables tables)
        {
            _tables = tables;
            MvMeta.Current = null;
            MvSave.CurrentChanged -= OnSaveChanged;
            MvSave.CurrentChanged += OnSaveChanged;
            OnSaveChanged(MvSave.Current);
        }

        /// <summary>연결을 끊고 <see cref="MvMeta.Current"/>를 비운다(시험 정리용).</summary>
        public static void Detach()
        {
            MvSave.CurrentChanged -= OnSaveChanged;
            MvMeta.Current = null;
            _tables = null;
        }

        static void OnSaveChanged(MvSaveStore store)
        {
            if (store == null || store.IsDisposed || _tables == null || MvMeta.Current != null) return;
            MvMeta.Current = new MvMeta(() => MvSave.Current, () => MvBootSave.Resources, _tables);
        }
    }
}
