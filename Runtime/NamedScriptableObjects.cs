using UnityEngine;

public class NamedScriptableObjects : MonoBehaviour
{
    public static NamedScriptableObjects Instance;

    public ScriptableObject[] Foo;

    void Awake()
    {
        Instance = this;
    }
}
