using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GarageManager : MonoBehaviour
{
    [System.Serializable]
    public class CarEntry
    {
        public string carName;
        public string mapSceneName;
        [Tooltip("Display model shown in garage (can be null if not modeled yet)")]
        public GameObject displayModel;
        [Tooltip("Where the camera moves to view this car")]
        public Transform cameraPoint;
    }

    [System.Serializable]
    public class DriverEntry
    {
        public string driverName;
        [Tooltip("Display model shown in garage (can be null if not modeled yet)")]
        public GameObject displayModel;
    }

    [Header("Cars")]
    public CarEntry[] cars = new CarEntry[]
    {
        new CarEntry { carName = "Car 1", mapSceneName = "Map1" },
        new CarEntry { carName = "Car 2", mapSceneName = "Map2" },
        new CarEntry { carName = "Car 3", mapSceneName = "Map3" },
    };

    [Header("Drivers")]
    public DriverEntry[] drivers = new DriverEntry[]
    {
        new DriverEntry { driverName = "Driver 1" },
        new DriverEntry { driverName = "Driver 2" },
        new DriverEntry { driverName = "Driver 3" },
    };

    [Header("Camera")]
    [Tooltip("The main camera in the garage scene")]
    public Camera garageCamera;
    public float cameraMoveSpeed = 5f;
    

    [Tooltip("Display car GameObjects shown in the garage. Top arrows cycle which one is active. Index 0 = orange (Car 1), 1 = pink (Car 2), 2 = blue (Car 3). Place all at the same world position.")]
    public GameObject[] displayCars;
    private int selectedDisplayCarIndex = 0;
public float cameraRotateSpeed = 5f;

    [Header("Cutscene")]
    [Tooltip("Optional. If set, ConfirmAndRace plays the cutscene + fade before loading the map. If null, the map loads immediately.")]
    public CutsceneManager cutsceneManager;

    [Header("UI")]
    public TextMeshProUGUI carNameText;
    public TextMeshProUGUI driverNameText;
    public TextMeshProUGUI mapNameText;
    public GameObject confirmButton;

    private int selectedCarIndex = 0;
    private int selectedDriverIndex = 0;
    private Vector3 targetCamPos;
    private Quaternion targetCamRot;
    private bool isMoving = false;

    void Start()
    {
        if (garageCamera == null)
            garageCamera = Camera.main;

        // Self-wire buttons by name
        // Top "SELECT CAR" arrows (named PrevDriverButton/NextDriverButton in the scene)
        // switch which camera is active (Main vs Car2).
        WireButton("PrevDriverButton", PreviousCamera);
        WireButton("NextDriverButton", NextCamera);
        // Bottom map arrows (named PrevCarButton/NextCarButton in the scene)
        // cycle the car/map pairing.
        WireButton("PrevCarButton", PreviousCar);
        WireButton("NextCarButton", NextCar);
        WireButton("RaceButton", ConfirmAndRace);
        WireButton("BackButton", BackToMenu);

        // Auto-find text elements if not assigned
        if (carNameText == null) carNameText = FindText("CarNameText");
        if (driverNameText == null) driverNameText = FindText("DriverNameText");
        if (mapNameText == null) mapNameText = FindText("MapNameText");

        UpdateUI();
        UpdateDisplayModels();
        SnapCameraToCurrentCar();
        UpdateDisplayCarSelection();

        if (cutsceneManager == null) cutsceneManager = FindFirstObjectByType<CutsceneManager>();
    }

    void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        var btn = FindInScene(name);
        if (btn == null)
        {
            Debug.LogWarning($"[GarageManager] Button '{name}' not found in any Canvas.");
            return;
        }
        var button = btn.GetComponent<UnityEngine.UI.Button>();
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    TextMeshProUGUI FindText(string name)
    {
        var go = FindInScene(name);
        return go != null ? go.GetComponent<TextMeshProUGUI>() : null;
    }

    GameObject FindInScene(string name)
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            Transform t = FindDeep(canvas.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    void Update()
    {
        if (!isMoving || garageCamera == null) return;

        garageCamera.transform.position = Vector3.Lerp(
            garageCamera.transform.position, targetCamPos,
            Time.deltaTime * cameraMoveSpeed);

        garageCamera.transform.rotation = Quaternion.Slerp(
            garageCamera.transform.rotation, targetCamRot,
            Time.deltaTime * cameraRotateSpeed);

        float dist = Vector3.Distance(garageCamera.transform.position, targetCamPos);
        float angle = Quaternion.Angle(garageCamera.transform.rotation, targetCamRot);
        if (dist < 0.01f && angle < 0.5f)
        {
            garageCamera.transform.position = targetCamPos;
            garageCamera.transform.rotation = targetCamRot;
            isMoving = false;
        }
    }

    // --- Car Selection ---
    public void NextCar()
    {
        selectedCarIndex = (selectedCarIndex + 1) % cars.Length;
        OnCarChanged();
    }

    public void PreviousCar()
    {
        selectedCarIndex--;
        if (selectedCarIndex < 0) selectedCarIndex = cars.Length - 1;
        OnCarChanged();
    }

    // --- Driver Selection ---
    public void NextDriver()
    {
        selectedDriverIndex = (selectedDriverIndex + 1) % drivers.Length;
        UpdateUI();
        UpdateDisplayModels();
    }

    public void PreviousDriver()
    {
        selectedDriverIndex--;
        if (selectedDriverIndex < 0) selectedDriverIndex = drivers.Length - 1;
        UpdateUI();
        UpdateDisplayModels();
    }

    void OnCarChanged()
    {
        UpdateUI();
        UpdateDisplayModels();
        MoveCameraToCurrentCar();
    }

    // --- Display Car Switching (top arrows) ---
    public void NextCamera()
    {
        if (displayCars == null || displayCars.Length == 0) return;
        selectedDisplayCarIndex = (selectedDisplayCarIndex + 1) % displayCars.Length;
        UpdateDisplayCarSelection();
    }

    public void PreviousCamera()
    {
        if (displayCars == null || displayCars.Length == 0) return;
        selectedDisplayCarIndex--;
        if (selectedDisplayCarIndex < 0) selectedDisplayCarIndex = displayCars.Length - 1;
        UpdateDisplayCarSelection();
    }

    void UpdateDisplayCarSelection()
    {
        if (displayCars == null) return;
        for (int i = 0; i < displayCars.Length; i++)
        {
            if (displayCars[i] != null)
                displayCars[i].SetActive(i == selectedDisplayCarIndex);
        }
    }

    
// --- Confirm & Race ---
    public void ConfirmAndRace()
    {
        if (cars == null || cars.Length == 0)
        {
            Debug.LogError("[GarageManager] cars array is empty.");
            return;
        }
        if (selectedCarIndex < 0 || selectedCarIndex >= cars.Length)
        {
            Debug.LogError($"[GarageManager] selectedCarIndex {selectedCarIndex} out of range (cars.Length={cars.Length}).");
            return;
        }

        CarEntry car = cars[selectedCarIndex];
        if (string.IsNullOrEmpty(car.mapSceneName))
        {
            Debug.LogError($"[GarageManager] cars[{selectedCarIndex}] ({car.carName}) has no mapSceneName set in the inspector.");
            return;
        }

        PlayerPrefs.SetInt("SelectedCar", selectedCarIndex);
        PlayerPrefs.SetInt("SelectedDriver", selectedDriverIndex);
        // Top-arrow display car index: 0 = orange (Car 1), 1 = pink (Car 2), 2 = blue (Car 3).
        PlayerPrefs.SetInt("SelectedCarColor", selectedDisplayCarIndex);
        PlayerPrefs.SetString("SelectedCarName", car.carName);
        PlayerPrefs.SetString("SelectedDriverName", drivers[selectedDriverIndex].driverName);
        PlayerPrefs.Save();

        if (cutsceneManager != null)
        {
            cutsceneManager.SetSelectedScene(car.mapSceneName);
            cutsceneManager.StartCutscene();
        }
        else
        {
            SceneManager.LoadScene(car.mapSceneName);
        }
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    // --- Camera ---
    void MoveCameraToCurrentCar()
    {
        if (garageCamera == null) return;

        Transform camPoint = cars[selectedCarIndex].cameraPoint;
        if (camPoint != null)
        {
            targetCamPos = camPoint.position;
            targetCamRot = camPoint.rotation;
            isMoving = true;
        }
    }

    void SnapCameraToCurrentCar()
    {
        if (garageCamera == null) return;

        Transform camPoint = cars[selectedCarIndex].cameraPoint;
        if (camPoint != null)
        {
            garageCamera.transform.position = camPoint.position;
            garageCamera.transform.rotation = camPoint.rotation;
        }
    }

    // --- UI ---
    void UpdateUI()
    {
        if (carNameText != null)
            carNameText.text = cars[selectedCarIndex].carName;

        if (driverNameText != null)
            driverNameText.text = drivers[selectedDriverIndex].driverName;

        if (mapNameText != null)
            mapNameText.text = cars[selectedCarIndex].mapSceneName;
    }

    void UpdateDisplayModels()
    {
        for (int i = 0; i < cars.Length; i++)
        {
            if (cars[i].displayModel != null)
                cars[i].displayModel.SetActive(i == selectedCarIndex);
        }

        for (int i = 0; i < drivers.Length; i++)
        {
            if (drivers[i].displayModel != null)
                drivers[i].displayModel.SetActive(i == selectedDriverIndex);
        }
    }
}
