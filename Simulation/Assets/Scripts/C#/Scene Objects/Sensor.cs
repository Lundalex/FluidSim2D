using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

using Resources;
using System.Globalization;
public class Sensor : MonoBehaviour
{
    [Header("Measurement")]
    public SensorType sensorType;
    public int rbIndex;

    [Header("Display")]
    public float2 offset;
    public bool doInterpolation;
    [Range(1.0f, 20.0f)] public float moveSpeed;

    [Header("References")]
    public GameObject sensorPrefab;

    // Private references
    private Transform sensorContainer;
    private Main main;
    private SensorManager sensorManager;
    private Rect uiCanvasRect;
    private Vector2 canvasResolution;

    // Display
    private GameObject sensorUI;
    private Text sensorText;
    private RectTransform sensorUIRect;

    private Vector2 targetPosition;
    private void Start()
    {
        sensorContainer = GameObject.FindGameObjectWithTag("SensorUIContainer").GetComponent<Transform>();
        main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        sensorManager = GameObject.FindGameObjectWithTag("SensorManager").GetComponent<SensorManager>();
        uiCanvasRect = GameObject.FindGameObjectWithTag("UICanvas").GetComponent<RectTransform>().rect;
        canvasResolution = new Vector2(uiCanvasRect.width, uiCanvasRect.height);

        CreateSensorUI();
    }

    private void CreateSensorUI()
    {
        sensorUI = Instantiate(sensorPrefab, sensorContainer);
        sensorUI.name = "SensorUI, " + this.name;

        sensorUIRect = sensorUI.GetComponent<RectTransform>();
        sensorUIRect.localPosition = SimSpaceToCanvasSpace(new(-100, -100));

        sensorText = sensorUI.transform.Find("Label").GetComponent<Text>();
    }

    void Update()
    {
        Vector2 canvasTargetPosition = SimSpaceToCanvasSpace(targetPosition);
        
        // Interpolate the current position
        sensorUIRect.localPosition = doInterpolation ? Vector2.Lerp(sensorUIRect.localPosition, canvasTargetPosition, Time.deltaTime * moveSpeed) : canvasTargetPosition;
    }

    public void UpdateSensor()
    {
        RBData rbData = sensorManager.retrievedRBData[rbIndex];
        targetPosition = rbData.pos + offset;

        if (sensorText != null)
        {
            const int NumDecimals = 2;
            switch (sensorType)
            {
                case SensorType.RigidBodyVelocity:
                    Vector2 xy = Func.Int2ToFloat2(rbData.vel_AsInt2);
                    sensorText.text = FloatToStr(xy.magnitude, NumDecimals) + " h.e";
                    return;

                case SensorType.RigidBodyPosition:
                    sensorText.text = "X: " + rbData.pos.x.ToString($"F{NumDecimals}", CultureInfo.InvariantCulture) + ", Y: " + rbData.pos.y.ToString($"F{NumDecimals}", CultureInfo.InvariantCulture);
                    return;
                default:
                    Debug.LogWarning("Sensor algorithm fault");
                    return;
            }
        }
    }

    private static string FloatToStr(float value, int numDecimals) => value.ToString($"F{numDecimals}", CultureInfo.InvariantCulture);

    private Vector2 SimSpaceToCanvasSpace(Vector2 simCoords) => (simCoords / new Vector2(main.BoundaryDims.x, main.BoundaryDims.y) - new Vector2(0.5f, 0.5f)) * canvasResolution;
}
