using UnityEngine;

public static class DragonLoader
{
    public static void LoadAndScaleDragon(bool renderFloor)
    {
        var dragonPrefab = Resources.Load<GameObject>("Dragon/Dragon-base-origin");
        if (dragonPrefab)
        {
            var dragon = Object.Instantiate(dragonPrefab);
            dragon.transform.position = Vector3.zero;
            dragon.transform.rotation = Quaternion.Euler(-90, 0, 180);
            dragon.transform.localScale = Vector3.one * 1f;
        }
        else
        {
            Debug.LogError("Dragon model not found in Resources/Dragon folder.");
        }

        if (!renderFloor) return;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(1, 1, 1);
    }
}
