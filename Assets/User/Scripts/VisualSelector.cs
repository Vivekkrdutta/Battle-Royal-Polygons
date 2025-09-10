using System.Collections.Generic;
using UnityEngine;

public class VisualSelector : MonoBehaviour
{
    [SerializeField] private Transform[] visualsList;
    [SerializeField] private Transform[] equipmentsList;
    public void ActivateVisual(int index)
    {
        foreach (Transform visual in visualsList)
        {
            visual.gameObject.SetActive(false);
        }

        visualsList[index].gameObject.SetActive(true);
    }

    public void Equip(int index)
    {
        foreach(Transform visual in equipmentsList)
        {
            visual.gameObject.SetActive(false);
        }

        equipmentsList[index].gameObject.SetActive(true);
    }

    public void Equip(List<int> indices)
    {
        foreach(Transform visual in equipmentsList)
        {
            visual.gameObject.SetActive(false);
        }

        foreach(var index in indices)
        {
            equipmentsList[index].gameObject.SetActive(false);
        }
    }

    public void ActivateVisuals(List<int> indices)
    {
        foreach (Transform visual in visualsList)
        {
            visual.gameObject.SetActive(false);
        }

        foreach(var i in indices)
        {
            visualsList[i].gameObject.SetActive(true);
        }
    }
}
