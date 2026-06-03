using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class NormalLevelScrollerCellView : SelectableScrollerCellView<NormalLevelListItem>
    {
        private Image levelImage;
        private Transform imgLock;
        private Transform imgCarrot;
        private Image carrotImage;
        private Transform imgAllClear;
        private Button button;
        private int boundBigLevelId;

        protected override void LoadCallBack()
        {
            CacheUiRefs();
        }

        protected override void OnNameTableMissing()
        {
            base.OnNameTableMissing();
            CacheUiRefsFromHierarchy();
        }

        private void CacheUiRefs()
        {
            //levelImage = GetUiComponent<Image>("img_level");
            //if (levelImage == null)
            //{
            //    levelImage = GetComponent<Image>();
            //}

            //imgLock = GetUiTransform("img_lock");
            //imgCarrot = GetUiTransform("img_carrot");
            //carrotImage = GetUiComponent<Image>("img_carrot");
            //imgAllClear = GetUiTransform("img_all_clear");
            //button = GetUiComponent<Button>("btn");
            //if (button == null)
            //{
            //    button = GetComponent<Button>();
            //}

            //if (imgLock == null || imgCarrot == null || imgAllClear == null)
            //{
            //    CacheUiRefsFromHierarchy();
            //}
        }

        private void CacheUiRefsFromHierarchy()
        {
            if (levelImage == null)
            {
                levelImage = GetComponent<Image>();
            }

            if (imgLock == null)
            {
                imgLock = transform.Find("img_lock");
            }

            if (imgCarrot == null)
            {
                imgCarrot = transform.Find("img_carrot");
                if (imgCarrot != null)
                {
                    carrotImage = imgCarrot.GetComponent<Image>();
                }
            }

            if (imgAllClear == null)
            {
                imgAllClear = transform.Find("img_all_clear");
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        protected override void OnSetData(NormalLevelListItem data, int dataIndex)
        {
            if (data?.mapInfo == null)
            {
                return;
            }

            boundBigLevelId = data.mapInfo.bigLevelId;
            if (levelImage != null)
            {
                levelImage.sprite = ResourceLoader.Instance.loadRes<Sprite>(data.GetLevelSpritePath(boundBigLevelId));
            }

            ApplyMapState(data.mapInfo);
        }

        private void ApplyMapState(SingleMapInfo info)
        {
            if (imgCarrot != null)
            {
                imgCarrot.gameObject.SetActive(false);
            }

            if (imgAllClear != null)
            {
                imgAllClear.gameObject.SetActive(false);
            }

            if (info.unLocked == MapInfoType.UNLOCK_LEVEL)
            {
                if (imgLock != null)
                {
                    imgLock.gameObject.SetActive(false);
                }

                if (info.isAllClear == MapInfoType.ALL_CLEAR && imgAllClear != null)
                {
                    imgAllClear.gameObject.SetActive(true);
                }

                if (info.carrotState != 0 && imgCarrot != null && carrotImage != null)
                {
                    imgCarrot.gameObject.SetActive(true);
                    carrotImage.sprite = ResourceLoader.Instance.loadRes<Sprite>(
                        NormalLevelListItem.MapFilePathBase + "Carrot_" + info.carrotState);
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

                if (button != null)
                {
                    button.interactable = true;
                }
            }
        }

        public override void SetSelected(bool selected)
        {
        }
    }
}
