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

        bool canLoad = spawner.data != null && spawner.palette != null;

        using (new EditorGUI.DisabledScope(!canLoad))
        {
            if (GUILayout.Button(new GUIContent("Load Board", "Clear children and instantiate tiles from the Board Data using the Tile Palette"), GUILayout.Height(32)))
            {
                spawner.Generate();
            }
        }

        if (!canLoad)
        {
            EditorGUILayout.HelpBox("Assign both Board Data and Tile Palette to enable Load Board.", MessageType.Info);
        }
        else if (spawner.data.cells == null || spawner.data.cells.Count == 0)
        {
            EditorGUILayout.HelpBox("Board Data has no cells. Paint cells in Tools → Board Editor first.", MessageType.Warning);
        }
    }
}
