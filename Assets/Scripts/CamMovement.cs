using UnityEngine;

public class CamMovement : MonoBehaviour
{
    public UIscript ui;

    private Vector3 start = new Vector3(0f, 0f, -10f);
    private float tPosition;

    void Start()
    {
        transform.position = start;
        tPosition = 0f;
    }

    void Update()
    {
        float upExtreme = 2f;
        float downExtreme = -ui.memorySize * UIscript.scale + 8f;

        if (ui.state == UIscript.State.running && Input.GetKey(KeyCode.UpArrow)) tPosition -= 0.01f / UIscript.scale;
        if (ui.state == UIscript.State.running && Input.GetKey(KeyCode.DownArrow)) tPosition += 0.01f / UIscript.scale;

        if (tPosition > 1f) tPosition = 1f;
        else if (tPosition < 0f) tPosition = 0f;

        transform.position = new Vector3(transform.position.x, Mathf.Lerp(upExtreme, downExtreme, tPosition), transform.position.z);
    }
}
