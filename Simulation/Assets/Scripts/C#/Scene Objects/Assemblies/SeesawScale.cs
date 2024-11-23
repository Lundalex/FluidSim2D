using System;
using Resources2;
using UnityEngine;

public class SeesawScale : EditorLifeCycle
{
    [Header("Settings")]
    [Range(0.1f, 0.9f), SerializeField] private float leverArmJointLerpFactor = 0.5f;

    [Header("References")]
    [SerializeField] private SceneRigidBody leftBucket;
    [SerializeField] private SceneRigidBody rightBucket;
    [SerializeField] private SceneRigidBody plank;
    
    #if UNITY_EDITOR
        private void OnValidate() => OnEditorUpdate();

        public override void OnEditorUpdate()
        {
            if (leftBucket == null || rightBucket == null || plank == null)
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

            plank.RBInput.overrideCentroidPosition = lerpPos;
            leftBucket.RBInput.localLinkPosOtherRB = leftBucketPos;
            rightBucket.RBInput.localLinkPosOtherRB = rightBucketPos;
        }
    #endif
}