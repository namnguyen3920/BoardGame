using UnityEngine;

[CreateAssetMenu(menuName = "BoardGame/Tile Palette", fileName = "TilePalette")]
public class TilePalette : ScriptableObject
{
    public GameObject[] prefabs;

    public int Count => prefabs == null ? 0 : prefabs.Length;

    public GameObject Get(int index)
    {
        if (prefabs == null || index < 0 || index >= prefabs.Length) return null;
        return prefabs[index];
    }
}
