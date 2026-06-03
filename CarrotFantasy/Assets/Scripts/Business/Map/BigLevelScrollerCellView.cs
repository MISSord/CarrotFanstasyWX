using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class BigLevelScrollerCellView : SelectableScrollerCellView<BigLevelInfo>
    {
        private Transform imgPage;
        private Text txtPage;

        protected override void LoadCallBack()
        {
            imgPage = this.nameTableDic["img_page"].transform;
            txtPage = this.nameTableDic["txt_page"].GetComponent<Text>();
        }

        protected override void OnSetData(BigLevelInfo data, int dataIndex)
        {
            GameLogController.Error(data.bigLevel.ToString());

            RawImage rawimg = this.nameTableDic["raw_bg"].GetComponent<RawImage>();
            string asset = string.Format("themescene2_{0}", data.bigLevel);
            string bundle = ResPath.GetRawImagePath(asset);
            rawimg.SetTexture(bundle, asset);

            this.nameTableDic["img_lock"].gameObject.SetActive(data.isLock);

            Text txt_page = this.nameTableDic["txt_page"].GetComponent<Text>();
            txt_page.text = LanguageUtil.Instance.GetFormatString(105, data.unlockCount.ToString(), data.count.ToString());

        }

    }
}
