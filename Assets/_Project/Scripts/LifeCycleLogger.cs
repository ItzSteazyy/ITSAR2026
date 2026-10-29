using UnityEngine;

public class LifeCycleLogger : MonoBehaviour
{
    void Awake()
    {
        Debug.Log($"Eseuo Awake {gameObject.name}");
    }

    //
    void OnEnable()
    {
        Debug.Log($"Eseuo OnEanble {gameObject.name}");
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log($"Eseuo Start {gameObject.name}");
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log($"Eseuo Update {gameObject.name}");
    }

    private void FixedUpdate()
    {
        Debug.Log($"Eseuo FixedUpdate {gameObject.name}");
    }

    private void LateUpdate()
    {
        Debug.Log($"Eseuo LateUpdate {gameObject.name}");
    }

    private void OnDestroy()
    {
        Debug.Log($"Eseuo OnDestroy {gameObject.name}");
    }

    private void OnDisable()
    {
        Debug.Log($"Eseuo OnDisable {gameObject.name}");
    }
}
