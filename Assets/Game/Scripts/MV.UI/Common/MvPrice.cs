using System;
using System.Collections.Generic;
using jjevol;
using MV.Data;
using FwConsume = jjevol.ConsumeData;
using MvConsume = MV.Data.ConsumeData;

namespace MV.UI
{
    /// <summary>
    /// 게임 비용(<see cref="MvConsume"/>, 07 §7.1.6)을 Framework 가격 표시(<see cref="PriceLabel"/>·<see cref="PriceBtn"/>)에 넘기는 공용 입구.
    /// 글자(게임 숫자 표기 MV-FW-NUMFMT)·부족 색·버튼 잠금은 Framework 부품이 재화 서비스로 직접 판단한다(사용자 2026-11-19 「Framework PriceBtn 사용」).
    /// </summary>
    public static class MvPrice
    {
        /// <summary>
        /// 재화(<see cref="ConsumeType.Wealth"/>) 비용만 바꾼다. 값은 정수로 내림(<c>MvMeta.CanAfford</c>와 같은 규칙).
        /// 비었거나 재화가 아닌 항목이 있거나 서비스가 없으면 null(가격으로 그리지 않는다).
        /// </summary>
        public static FwConsume[] ToFramework(IReadOnlyList<MvConsume> price, ResourceService resources)
        {
            if (price == null || price.Count == 0 || resources == null || resources.IsDisposed) return null;
            var costs = new FwConsume[price.Count];
            for (int i = 0; i < price.Count; i++)
            {
                var c = price[i];
                if (c.Type != ConsumeType.Wealth) return null;
                costs[i] = new FwConsume(resources.WealthTypeId, c.Id, (long)Math.Floor(c.Value));
            }
            return costs;
        }

        /// <summary>
        /// 가격 버튼에 비용을 넣어 보인다. 그릴 수 없으면(비용 없음·재화 아님·서비스 없음) false를 돌려주고 버튼은 건드리지 않는다.
        /// <paramref name="inputAllowed"/>는 잔액 밖의 조건(메타 준비·최대 아님)이다.
        /// </summary>
        public static bool Show(PriceBtn button, ResourceService resources, IReadOnlyList<MvConsume> price, bool inputAllowed)
        {
            if (!button) return false;
            var costs = ToFramework(price, resources);
            if (costs == null) return false;
            button.SetInputAllowed(inputAllowed);
            button.ShowCosts(resources, costs, MvConsumePresentation.Current);
            return true;
        }
    }
}
