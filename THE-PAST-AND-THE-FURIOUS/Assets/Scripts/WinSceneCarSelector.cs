using UnityEngine;

[DefaultExecutionOrder(-100)]
public class WinSceneCarSelector : MonoBehaviour
{
    [Tooltip("Index 0 = orange (Car 1), 1 = pink (Car 2), 2 = blue (Car 3). Stack at the same position; one is active at a time.")]
    public GameObject[] colorVariants;

    void Awake()
    {
        if (colorVariants == null || colorVariants.Length == 0) return;
        int idx = PlayerPrefs.GetInt("SelectedCarColor", 0);
        idx = Mathf.Clamp(idx, 0, colorVariants.Length - 1);
        for (int i = 0; i < colorVariants.Length; i++)
            if (colorVariants[i] != null) colorVariants[i].SetActive(i == idx);
    }
}
