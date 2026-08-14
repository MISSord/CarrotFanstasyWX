using System;
using UnityEngine;

namespace CarrotFantasy
{
    /// <summary>
    /// AOT 启动期界面基类：从 Resources 加载预制体，绑定 <see cref="UINameTable"/>，不经过 ViewManager。
    /// </summary>
    public abstract class AotResourcesView
    {
        protected GameObject Root { get; private set; }

        protected UINameTableDic NameTable { get; private set; }

        public bool IsOpen
        {
            get { return this.Root != null; }
        }

        /// <summary>Resources.Load 路径（不含扩展名），例如 AotUI/DownloadConfirm。</summary>
        protected abstract string ResourcesPath { get; }

        /// <summary>
        /// 打开界面。失败返回 false（缺预制体等）。
        /// </summary>
        public bool Open(Transform parent = null)
        {
            if (this.Root != null)
            {
                return true;
            }

            GameObject prefab = Resources.Load<GameObject>(this.ResourcesPath);
            if (prefab == null)
            {
                Debug.LogError(
                    "[AotResourcesView] Resources 未找到预制体: " + this.ResourcesPath +
                    "\n请执行菜单 Tools/AOT UI/生成启动界面预制体");
                return false;
            }

            this.Root = UnityEngine.Object.Instantiate(prefab);
            this.Root.name = prefab.name;
            if (parent != null)
            {
                this.Root.transform.SetParent(parent, false);
            }
            else
            {
                UnityEngine.Object.DontDestroyOnLoad(this.Root);
            }

            this.NameTable = new UINameTableDic();
            UINameTable table = this.Root.GetComponent<UINameTable>();
            if (table == null)
            {
                table = this.Root.GetComponentInChildren<UINameTable>(true);
            }

            if (table != null)
            {
                this.NameTable.AddUINameTable(table.GetNameTableList());
            }
            else
            {
                Debug.LogWarning("[AotResourcesView] 预制体未挂 UINameTable: " + this.ResourcesPath);
            }

            this.OnOpen();
            return true;
        }

        public void Close()
        {
            try
            {
                this.OnClose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (this.NameTable != null)
            {
                this.NameTable.ClearAllInfo();
                this.NameTable = null;
            }

            if (this.Root != null)
            {
                UnityEngine.Object.Destroy(this.Root);
                this.Root = null;
            }
        }

        protected abstract void OnOpen();

        protected virtual void OnClose()
        {
        }
    }
}
