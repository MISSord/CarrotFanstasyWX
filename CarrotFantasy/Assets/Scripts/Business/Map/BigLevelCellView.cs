using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class BigLevelInfo
    {
        public int bigLevel { get; set; }
        public int count { get; set; }
        public int unlockCount { get; set; }
        public bool isLock { get; set; }
    }

    public class BigLevelCellView : SelectableCellViewBase<BigLevelInfo>
    {
        RawImage rawBg;
        Transform imgLock;
        Text txtPage;

        protected override void OnAttach()
        {
            UINameTableDic nt = Shell.NameTable;
            if (nt == null)
            {
                return;
            }

            rawBg = nt.GetComponentSafely<RawImage>("raw_bg");
            txtPage = nt.GetComponentSafely<Text>("txt_page");
            GameObject lockGo = nt.GetGameObjectSafely("img_lock");
            if (lockGo != null)
            {
                imgLock = lockGo.transform;
            }
        }

        protected override void OnBind(BigLevelInfo data)
        {
            if (data == null)
            {
                return;
            }

            if (rawBg != null)
            {
                string asset = string.Format("themescene2_{0}", data.bigLevel);
                string bundle = ResPath.GetRawImagePath(asset);
                rawBg.SetTexture(bundle, asset);
            }

            if (imgLock != null)
            {
                imgLock.gameObject.SetActive(data.isLock);
            }

            if (txtPage != null)
            {
                txtPage.text = LanguageUtil.Instance.GetFormatString(
                    105,
                    data.unlockCount.ToString(),
                    data.count.ToString());
            }
        }
    }
}
