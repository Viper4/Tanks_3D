using UnityEngine;
using UnityEditor;
using System.Linq;

public class LevelEditor : EditorWindow
{
    [SerializeField] int times = 1;
    [SerializeField] Vector3 eulerAngles;
    [SerializeField] Vector3 scale = new Vector3(2, 2, 2);
    [SerializeField] private Vector3 direction;
    [SerializeField] float distanceAway;

    [SerializeField] float dissolveStrength = 0.25f;

    [MenuItem("Tools/Level Editor")]
    static void CreateReplaceWithPrefab()
    {
        GetWindow<LevelEditor>();
    }

    private void OnGUI()
    {
        times = EditorGUILayout.IntField("Times", times);
        eulerAngles = EditorGUILayout.Vector3Field("Euler Angles", eulerAngles);
        scale = EditorGUILayout.Vector3Field("Scale", scale);
        direction = EditorGUILayout.Vector3Field("Direction", direction);
        distanceAway = EditorGUILayout.FloatField("Distance Away", distanceAway);
        dissolveStrength = EditorGUILayout.FloatField("Dissolve Strength", dissolveStrength);

        if (GUILayout.Button("Clone"))
        {
            var selection = Selection.gameObjects;

            foreach (GameObject selected in selection)
            {
                // Iterate through all the times to clone this gameobject
                for (int i = 0; i < times; i++)
                {
                    // Instantiate this object at the given direction and distance away, reset its name and scale, and add the clone to the clonedObjects list
                    GameObject clone = Instantiate(selected, selected.transform.position + direction * (distanceAway * (i + 1)), Quaternion.Euler(eulerAngles), selected.transform.parent);
                    clone.name = selected.name;
                    clone.transform.localScale = scale;
                    Undo.RegisterCreatedObjectUndo(clone, "Cloned");
                }
            }
        }

        if (GUILayout.Button("Delete"))
        {
            var selection = Selection.gameObjects;

            foreach (GameObject selected in selection)
            {
                Undo.DestroyObjectImmediate(selected);
            }
        }

        if (GUILayout.Button("Dissolve"))
        {
            var selection = Selection.gameObjects;
            int dissolvedAmount = 0;

            foreach (GameObject parent in selection)
            {
                foreach (Transform child in parent.transform.Cast<Transform>().ToList())
                {
                    if (Random.value < dissolveStrength)
                    {
                        Undo.DestroyObjectImmediate(child.gameObject);

                        dissolvedAmount++;
                    }
                }
            }
            Debug.Log("Dissolved " + dissolvedAmount + " objects");
        }

        if (GUILayout.Button("Generate Random"))
        {
            LevelGenerator levelGenerator = FindObjectOfType<LevelGenerator>();
            levelGenerator.Generate();
        }

        GUI.enabled = false;
        EditorGUILayout.LabelField("Selection count: " + Selection.objects.Length);
    }
}

[CustomEditor(typeof(TankManager))]
public class TankEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Generate Random"))
        {
            FindObjectOfType<TankManager>().GenerateTanks();
        }

        GUILayout.EndHorizontal();
    }
}

