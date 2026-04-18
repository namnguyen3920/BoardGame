using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoardSpawner))]
public class BoardSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BoardSpawner spawner = (BoardSpawner)target;

        EditorGUILayout.Space(10);

        BoardLayerData baseLayer = spawner.data?.layers?.Count > 0 ? spawner.data.layers[0] : null;
        bool canLoad = spawner.data != null && baseLayer != null && baseLayer.Palette != null;

        using (new EditorGUI.DisabledScope(!canLoad))
        {
            if (GUILayout.Button(new GUIContent("Load Board", "Clear children and instantiate tiles from the base layer"), GUILayout.Height(32)))
            {
                spawner.Generate();
            }
        }

        if (spawner.data == null)
            EditorGUILayout.HelpBox("Assign a Board Data asset.", MessageType.Info);
        else if (baseLayer == null)
            EditorGUILayout.HelpBox("Board Data has no layers. Add a base layer in Tools → Map Editor.", MessageType.Warning);
        else if (baseLayer.Palette == null)
            EditorGUILayout.HelpBox("Base layer has no Tile Palette assigned.", MessageType.Warning);
        else if (baseLayer.Cells == null || baseLayer.Cells.Count == 0)
            EditorGUILayout.HelpBox("Base layer has no cells. Paint cells in Tools → Map Editor first.", MessageType.Warning);
    }
}
