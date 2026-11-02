// 하이어라키에 있는 비활성 원본을 복제해 목록을 그린다. 원본은 그대로 두고, 복제본은 재사용한다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Colosseum.UI
{
    public class TemplateList<T> where T : Component
    {
        readonly T template;
        readonly List<T> items = new();

        public TemplateList(T template)
        {
            this.template = template;
            template.gameObject.SetActive(false);
        }

        public IReadOnlyList<T> Items => items;

        /// <summary>n개를 맞추고 각각 bind(복제본, 순서)로 채운다</summary>
        public void Sync(int n, Action<T, int> bind)
        {
            while (items.Count < n)
            {
                var c = UnityEngine.Object.Instantiate(template, template.transform.parent);
                c.name = template.name.Replace("Template", "") + items.Count;
                items.Add(c);
            }
            for (int i = 0; i < items.Count; i++)
            {
                bool on = i < n;
                items[i].gameObject.SetActive(on);
                if (on)
                {
                    items[i].transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1 + i);
                    bind(items[i], i);
                }
            }
        }
    }
}
