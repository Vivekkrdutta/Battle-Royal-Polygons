using System.Linq;
using UnityEngine;

public class ShowUI : MonoBehaviour
{
    [SerializeField] private RectTransform[] uiElementsToShow;
    [SerializeField] BarUI.LookAt LookAt;
    private void Update()
    {
        if (uiElementsToShow.Length == 0) return;

        var dir = (uiElementsToShow.FirstOrDefault(_ => _ != null).transform.position - Camera.main.transform.position).normalized;

        var lookAtPos = GetCameraOpposite();

        if (LookAt == BarUI.LookAt.CameraYAxisOnly) lookAtPos.y = 0f;

        foreach(var element in uiElementsToShow)
        {
            element.transform.LookAt(lookAtPos);
        }
    }

    private Vector3 GetCameraOpposite()
    {
        var distance = 10f;
        var dirFromCam = (transform.position - Camera.main.transform.position).normalized;
        var lookAtPos = transform.position + dirFromCam * distance;
        return lookAtPos;
    }
}
