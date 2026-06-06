using UnityEngine;
using UnityEngine.UI;

namespace CarrotFantasy
{
    public class NormalLevelCellView : SelectableCellViewBase<NormalLevelListItem>
    {
        RawImage levelImage;
        Transform imgLock;
        Transform imgCarrot;
        Image carrotImage;
        Transform imgAllClear;
        Button button;

        protected override void OnAttach()
        {
            CacheUiRefs();
        }

        protected override void OnBind(NormalLevelListItem data)
        {
            if (data?.mapInfo == null)
            {
                return;
            }


            SingleMapInfo info = data.mapInfo;
            string asset = string.Format("Level_{0}_{1}", info.bigLevelId, info.levelId);
            string bundle = ResPath.GetRawImagePath(asset);
            levelImage.SetTexture(bundle, asset);

            ApplyMapState(data.mapInfo);
        }

        void CacheUiRefs()
        {
            UINameTableDic nt = Shell.NameTable;
            if (nt == null)
            {
                return;
            }

            GameObject levelGo = nt.GetGameObjectSafely("img_bg");
            if (levelGo != null)
            {
                levelImage = levelGo.GetComponent<RawImage>();
            }

            GameObject lockGo = nt.GetGameObjectSafely("img_lock");
            if (lockGo != null)
            {
                imgLock = lockGo.transform;
            }

            GameObject carrotGo = nt.GetGameObjectSafely("img_carrot");
            if (carrotGo != null)
            {
                imgCarrot = carrotGo.transform;
                carrotImage = carrotGo.GetComponent<Image>();
            }

            GameObject clearGo = nt.GetGameObjectSafely("img_all_clear");
            if (clearGo != null)
            {
                imgAllClear = clearGo.transform;
            }

            GameObject btnGo = nt.GetGameObjectSafely("btn");
            if (btnGo != null)
            {
                button = btnGo.GetComponent<Button>();
            }
        }

        void ApplyMapState(SingleMapInfo info)
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
                    string asset = "Carrot_" + info.carrotState;
                    string bundle = ResPath.GetGameOptionImagePath();
                    carrotImage.SetSprite(bundle, asset);
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
    }
}
