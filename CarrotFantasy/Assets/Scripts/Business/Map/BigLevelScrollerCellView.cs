using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class BigLevelScrollerCellView : SelectableScrollerCellView<BigLevelInfo>
    {
        private Transform imgLock;
        private Transform imgPage;
        private Text txtPage;
        private Button button;

        protected override void Awake()
        {
            base.Awake();
            imgLock = transform.Find("img_lock");
            imgPage = transform.Find("img_page");
            if (imgPage != null)
            {
                var txtTrans = imgPage.Find("txt_page");
                if (txtTrans != null)
                {
                    txtPage = txtTrans.GetComponent<Text>();
                }
            }

            button = GetComponent<Button>();
        }

        protected override void OnSetData(BigLevelInfo data, int dataIndex)
        {
            if (data == null)
            {
                return;
            }

            if (data.isLock == false)
            {
                if (imgLock != null)
                {
                    imgLock.gameObject.SetActive(false);
                }

                if (imgPage != null)
                {
                    imgPage.gameObject.SetActive(true);
                }

                if (txtPage != null)
                {
                    txtPage.text = data.unlockCount.ToString() + "/" + data.count.ToString();
                }

                if (button != null)
                {
                    button.interactable = true;
                }
            }
            else
            {
                if (imgLock != null)
                {
                    imgLock.gameObject.SetActive(true);
                }

                if (imgPage != null)
                {
                    imgPage.gameObject.SetActive(false);
                }

                if (button != null)
                {
                    button.interactable = false;
                }
            }
        }

        public override void SetSelected(bool selected)
        {
        }
    }
}
