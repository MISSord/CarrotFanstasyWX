using UnityEngine.UI;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// 不绘制的 <see cref="Graphic"/>，仅开启射线检测。与 <see cref="Button"/> 同挂，并将 Button 的 Target Graphic 设为本组件。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class UIBlock : Graphic
    {
        protected override void Awake()
        {
            base.Awake();
            raycastTarget = true;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}
