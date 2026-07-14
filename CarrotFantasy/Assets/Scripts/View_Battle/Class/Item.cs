using UnityEngine;


namespace CarrotFantasy
{
    /// <summary>
    /// 道具
    /// </summary>
    public class Item : MonoBehaviour
    {
        public BattleUnitView_Item itemView;

        private void OnMouseDown()
        {
            if (this.itemView == null)
            {
                return;
            }

            if (UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            this.itemView.RefreshTarget();
        }
    }
}
