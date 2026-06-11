using UnityEngine;
using System.Collections.Generic;

public class SelectableUnit : MonoBehaviour
{
    [Tooltip("Object chứa Decal hoặc Sprite hình vòng tròn xanh dưới chân nhân vật")]
    public GameObject selectionDecal;

    private bool isSelected = false;

    // Danh sách chứa tất cả các unit trên bản đồ để tối ưu Box Selection
    public static List<SelectableUnit> AllUnits = new List<SelectableUnit>();

    void OnEnable()
    {
        AllUnits.Add(this);
    }

    void OnDisable()
    {
        AllUnits.Remove(this);
    }

    void Start()
    {
        if (selectionDecal == null)
        {
            Transform decal = transform.Find("SelectionDecal");
            if (decal != null)
            {
                selectionDecal = decal.gameObject;
            }
        }
        
        // Đảm bảo lúc mới sinh ra thì chưa được chọn
        Deselect();
    }

    public void Select()
    {
        isSelected = true;
        if (selectionDecal != null)
        {
            selectionDecal.SetActive(true);
        }
    }

    public void Deselect()
    {
        isSelected = false;
        if (selectionDecal != null)
        {
            selectionDecal.SetActive(false);
        }
    }

    public bool IsSelected => isSelected;
}
