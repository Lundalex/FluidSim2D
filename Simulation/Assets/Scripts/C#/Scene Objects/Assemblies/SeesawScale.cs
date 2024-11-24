using System;
using Resources2;
using UnityEngine;

public class SeesawScale : EditorLifeCycle
{
    [Header("Lever Arm Settings")]
    [Range(0.1f, 0.9f), SerializeField] private float leverArmJointLerpFactor = 0.5f;

    [Header("References")]
    [SerializeField] private SceneRigidBody leftBucket;
    [SerializeField] private SceneRigidBody rightBucket;
    [SerializeField] private SceneRigidBody plank;
    [SerializeField] private Transform rotationJoint;
    
    #if UNITY_EDITOR
        private void OnValidate() => OnEditorUpdate();

        public override void OnEditorUpdate()
        {
            if (leftBucket == null || rightBucket == null || plank == null || rotationJoint == null)
            {
                Debug.LogWarning("All references are not set. SeesawScale: " + this.name);
                return;
            }

            Vector2 leftPlank = new(-75, 0);
            Vector2 rightPlank = new(75, 0);

            Vector2 lerpPos = Func.LerpVector2(leftPlank, rightPlank, leverArmJointLerpFactor);
            Vector2 bucketOffset = -lerpPos;

            Vector2 leftBucketPos = new Vector2(-100, 0) + bucketOffset;
            Vector2 rightBucketPos = new Vector2(100, 0) + bucketOffset;

            plank.rbInput.overrideCentroidPosition = lerpPos;
            rotationJoint.localPosition = lerpPos;
            leftBucket.rbInput.localLinkPosOtherRB = leftBucketPos;
            rightBucket.rbInput.localLinkPosOtherRB = rightBucketPos;
        }
    #endif
}