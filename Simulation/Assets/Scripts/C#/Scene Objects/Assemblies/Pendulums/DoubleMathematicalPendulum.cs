using UnityEngine;

public class DoubleMathematicalPendulum : MathematicalPendulum
{
    public float secondPendulumLength = 25f;
    [SerializeField] private SceneRigidBody firstRodObject;
    [SerializeField] private SceneRigidBody secondLinkPointObject;
}
