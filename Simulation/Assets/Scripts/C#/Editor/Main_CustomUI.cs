using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

[CustomEditor(typeof(Main))]
public class MainEditor : Editor
{
    public VisualTreeAsset m_UXML;

    public override VisualElement CreateInspectorGUI()
    {
        // Create a root VisualElement
        var root = new VisualElement();

        // Clone UXML layout into the root
        m_UXML.CloneTree(root);

        // Find the existing PropertyFields and TextField in the UXML by their name or binding path
        var maxSpringsPerParticleField = root.Q<PropertyField>("MaxSpringsPerParticle");
        var maxParticlesNumField = root.Q<PropertyField>("MaxParticlesNum");
        var totalParticleSpringsField = root.Q<TextField>("TotalParticleSprings");

        // Find the properties in the serialized object
        var serializedMaxSpringsPerParticle = serializedObject.FindProperty("MaxSpringsPerParticle");
        var serializedMaxParticlesNum = serializedObject.FindProperty("MaxParticlesNum");

        // Bind the PropertyFields to the serialized properties
        maxSpringsPerParticleField.BindProperty(serializedMaxSpringsPerParticle);
        maxParticlesNumField.BindProperty(serializedMaxParticlesNum);

        // Make the TextField read-only (so it behaves like a display field)
        totalParticleSpringsField.isReadOnly = true;

        // Create a function to update the result TextField dynamically
        void UpdateResult()
        {
            // Update the serialized object to get the latest values
            serializedObject.Update();

            int numSpringsPerParticle = serializedMaxSpringsPerParticle.intValue;
            int numParticles = serializedMaxParticlesNum.intValue;
            int numParticleSprings = numSpringsPerParticle * numParticles;

            totalParticleSpringsField.value = numParticleSprings.ToString(); // Display the result
        }

        // Subscribe to value changes to update the TextField dynamically
        maxSpringsPerParticleField.RegisterCallback<ChangeEvent<int>>(evt => UpdateResult());
        maxParticlesNumField.RegisterCallback<ChangeEvent<int>>(evt => UpdateResult());

        // Initialize the TextField value on creation
        UpdateResult();

        // --- Control the visibility of the AdvancedSettings foldout based on the ShowAdvanced toggle ---

        // Find the toggle and foldout in the UXML by their names
        var showAdvancedToggle = root.Q<Toggle>("ShowAdvanced");
        var advancedSettingsFoldout = root.Q<Foldout>("AdvancedSettings");

        var serializedShowAdvanced = serializedObject.FindProperty("ShowAdvanced");
        if (serializedShowAdvanced != null)
        {
            showAdvancedToggle.BindProperty(serializedShowAdvanced);
        }

        // Toggle the visibility of the foldout based of the toggle
        advancedSettingsFoldout.style.display = showAdvancedToggle.value ? DisplayStyle.Flex : DisplayStyle.None;
        showAdvancedToggle.RegisterValueChangedCallback(evt =>
        {
            advancedSettingsFoldout.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        });

        // #if true

        //     // Create a foldout for the full inspector (if needed)
        //     var foldOut = new Foldout() { viewDataKey = "MainFullInspectorFoldout", text = "Full Inspector" };
        //     InspectorElement.FillDefaultInspector(foldOut, serializedObject, this);
        //     root.Add(foldOut);

        // #endif

        return root;
    }
}
