#if UNITY_EDITOR
using jp.ootr.ludo;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace jp.ootr.ludo.Editor
{
    [CustomEditor(typeof(LudoEditorOnlyNote))]
    public class LudoEditorOnlyNoteEditor : UnityEditor.Editor
    {
        private const string HeaderTitle = "ootr式ルドー盤";

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var title = new Label(HeaderTitle);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 24;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.paddingTop = 16;
            title.style.paddingBottom = 16;
            root.Add(title);

            root.Add(new HelpBox("この GameObject を無効化しないでください。\n表示非表示を切り替えたい場合は Ludo/Objects の IsActive を使用してください", HelpBoxMessageType.Warning));
            return root;
        }
    }
}
#endif
