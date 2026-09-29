using UnityEditor;
using UnityEngine;

namespace Veerdonk.ProjectileSystem
{
    /// <summary>
    /// Grid inspector for <see cref="MultiplyPatternAsset"/>.
    /// Left-click an empty cell to assign the next number. Right-click removes it and compacts.
    /// + grows a bottom row and a right column. − drops those and compact-renumbers.
    /// </summary>
    [CustomEditor(typeof(MultiplyPatternAsset))]
    public sealed class MultiplyPatternAssetEditor : UnityEditor.Editor
    {
        const float Cell = 36f;
        const float Line = 2f;

        static GUIStyle cellLabel;

        public override void OnInspectorGUI()
        {
            var asset = (MultiplyPatternAsset)target;
            EditorGUILayout.LabelField("Grid", asset.Size + "×" + asset.Size);

            EditorGUI.BeginChangeCheck();
            float spacing = EditorGUILayout.FloatField("Spacing", asset.Spacing);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(asset, "Change Multiply Pattern Spacing");
                asset.Spacing = spacing;
                EditorUtility.SetDirty(asset);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+", GUILayout.Width(Cell), GUILayout.Height(22f)))
            {
                Undo.RecordObject(asset, "Grow Multiply Pattern");
                asset.Grow();
                EditorUtility.SetDirty(asset);
            }

            using (new EditorGUI.DisabledScope(asset.Size <= GridMultiplyPattern.MinSize))
            {
                if (GUILayout.Button("-", GUILayout.Width(Cell), GUILayout.Height(22f)))
                {
                    Undo.RecordObject(asset, "Shrink Multiply Pattern");
                    asset.Shrink();
                    EditorUtility.SetDirty(asset);
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);

            DrawGrid(asset);

            EditorGUILayout.HelpBox(
                "Left-click an empty cell for the next number. Left-click an occupied cell does nothing. Right-click removes that number and compacts.",
                MessageType.None);
        }

        static void DrawGrid(MultiplyPatternAsset asset)
        {
            int size = asset.Size;
            float span = size * Cell + (size + 1) * Line;
            Rect grid = GUILayoutUtility.GetRect(span, span, GUILayout.Width(span), GUILayout.Height(span));
            EditorGUI.DrawRect(grid, LineColor);

            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    var cell = new Rect(
                        grid.x + Line + column * (Cell + Line),
                        grid.y + Line + row * (Cell + Line),
                        Cell,
                        Cell);
                    DrawCell(asset, row, column, cell);
                }
            }
        }

        static void DrawCell(MultiplyPatternAsset asset, int row, int column, Rect rect)
        {
            int number = asset.Cell(row, column);
            Event current = Event.current;
            if (current.type == EventType.MouseDown && rect.Contains(current.mousePosition) && current.button <= 1)
            {
                Undo.RecordObject(asset, current.button == 1 ? "Clear Multiply Pattern Cell" : "Paint Multiply Pattern Cell");
                if (current.button == 1)
                    asset.RightClick(row, column);
                else
                    asset.LeftClick(row, column);
                EditorUtility.SetDirty(asset);
                current.Use();
            }

            EditorGUI.DrawRect(rect, number > 0 ? FilledColor : EmptyColor);
            if (number > 0)
                GUI.Label(rect, number.ToString(), CellLabel);
        }

        static GUIStyle CellLabel
        {
            get
            {
                if (cellLabel != null)
                    return cellLabel;

                cellLabel = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                cellLabel.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.1f, 0.1f, 0.1f);
                return cellLabel;
            }
        }

        static Color LineColor => EditorGUIUtility.isProSkin
            ? new Color(0.08f, 0.08f, 0.08f)
            : new Color(0.25f, 0.25f, 0.25f);

        static Color EmptyColor => EditorGUIUtility.isProSkin
            ? new Color(0.24f, 0.24f, 0.24f)
            : new Color(0.9f, 0.9f, 0.9f);

        static Color FilledColor => EditorGUIUtility.isProSkin
            ? new Color(0.27f, 0.42f, 0.55f)
            : new Color(0.62f, 0.78f, 0.9f);
    }
}
