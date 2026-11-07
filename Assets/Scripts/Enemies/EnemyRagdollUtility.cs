using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class EnemyRagdollUtility
{
    private readonly struct BoneDefinition
    {
        public readonly string Name;
        public readonly string Parent;
        public readonly string ColliderEnd;
        public readonly float RadiusScale;
        public readonly float Mass;

        public BoneDefinition(string name, string parent, string colliderEnd, float radiusScale, float mass)
        {
            Name = name;
            Parent = parent;
            ColliderEnd = colliderEnd;
            RadiusScale = radiusScale;
            Mass = mass;
        }
    }

    private static readonly BoneDefinition[] PassBones =
    {
        new("Hip", null, "Neck", 0.19f, 2.2f),
        new("Neck", "Hip", "Head", 0.16f, 0.8f),
        new("Head", "Neck", null, 0.28f, 0.6f),
        new("LeftHip", "Hip", "LeftKnee", 0.16f, 1.1f),
        new("LeftKnee", "LeftHip", "LeftFoot", 0.15f, 0.8f),
        new("LeftFoot", "LeftKnee", null, 0.27f, 0.4f),
        new("RightHip", "Hip", "RightKnee", 0.16f, 1.1f),
        new("RightKnee", "RightHip", "RightFoot", 0.15f, 0.8f),
        new("RightFoot", "RightKnee", null, 0.27f, 0.4f)
    };

    private static readonly BoneDefinition[] PontanBones =
    {
        new("Spine", null, "Spine1", 0.2f, 2.2f),
        new("Spine1", "Spine", null, 0.34f, 1.3f),
        new("LeftArm", "Spine", "LeftHand", 0.15f, 0.8f),
        new("LeftHand", "LeftArm", "LeftHand1", 0.2f, 0.45f),
        new("LeftHand1", "LeftHand", null, 0.25f, 0.3f),
        new("RightArm", "Spine", "RightHand", 0.15f, 0.8f),
        new("RightHand", "RightArm", "RightHand1", 0.2f, 0.45f),
        new("RightHand1", "RightHand", null, 0.25f, 0.3f),
        new("LeftLeg", "Spine", "LeftFoot", 0.17f, 1.1f),
        new("LeftFoot", "LeftLeg", "LeftFoot1", 0.18f, 0.7f),
        new("LeftFoot1", "LeftFoot", null, 0.25f, 0.35f),
        new("RightLeg", "Spine", "RightFoot", 0.17f, 1.1f),
        new("RightFoot", "RightLeg", "RightFoot1", 0.18f, 0.7f),
        new("RightFoot1", "RightFoot", null, 0.25f, 0.35f)
    };

    public static bool TryActivate(EnemyController enemy, Vector3 forceOrigin, bool fromBomb, float force,
        out Transform rootBone)
    {
        rootBone = null;
        BoneDefinition[] profile = enemy is PassEnemy ? PassBones
            : enemy is PontanEnemy ? PontanBones
            : null;
        if (profile == null) return false;

        HashSet<string> requiredNames = new(StringComparer.Ordinal);
        foreach (BoneDefinition definition in profile)
        {
            requiredNames.Add(definition.Name);
            if (definition.Parent != null) requiredNames.Add(definition.Parent);
            if (definition.ColliderEnd != null) requiredNames.Add(definition.ColliderEnd);
        }

        Dictionary<string, Transform> bones = new(StringComparer.Ordinal);
        foreach (SkinnedMeshRenderer renderer in enemy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach (Transform bone in renderer.bones)
                if (bone != null && requiredNames.Contains(bone.name) && !bones.ContainsKey(bone.name))
                    bones.Add(bone.name, bone);

        if (requiredNames.Any(name => !bones.ContainsKey(name))) return false;

        // The Animator has just been disabled. Flush its final bone pose before
        // creating colliders and joints so PhysX starts from the visible pose.
        Physics.SyncTransforms();

        Dictionary<string, Rigidbody> bodies = new(StringComparer.Ordinal);
        foreach (BoneDefinition definition in profile)
        {
            Transform bone = bones[definition.Name];
            if (bone.GetComponent<Rigidbody>() != null) return false;
            Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = definition.Mass;
            body.useGravity = true;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations = 12;
            body.solverVelocityIterations = 12;
            body.maxAngularVelocity = 20f;
            body.angularDamping = 1.2f;
            bodies.Add(definition.Name, body);
        }

        foreach (BoneDefinition definition in profile)
        {
            Transform bone = bones[definition.Name];
            AddCollider(bone, definition, bones);
            if (definition.Parent == null) continue;

            CharacterJoint joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = bodies[definition.Parent];
            joint.autoConfigureConnectedAnchor = true;
            joint.enableCollision = false;
            joint.enableProjection = true;
            joint.projectionDistance = 0.05f;
            joint.projectionAngle = 15f;
            joint.swing1Limit = new SoftJointLimit { limit = 38f };
            joint.swing2Limit = new SoftJointLimit { limit = 38f };
            joint.lowTwistLimit = new SoftJointLimit { limit = -28f };
            joint.highTwistLimit = new SoftJointLimit { limit = 28f };
        }

        foreach (BoneDefinition definition in profile)
        {
            Transform bone = bones[definition.Name];
            Collider collider = bone.GetComponent<Collider>();
            if (collider != null) collider.enabled = true;
            Rigidbody body = bodies[definition.Name];
            body.isKinematic = false;
            body.WakeUp();
        }
        Physics.SyncTransforms();

        rootBone = bones[profile[0].Name];
        Vector3 direction = enemy.transform.position - forceOrigin;
        direction.y = 0f;
        if (!fromBomb || direction.sqrMagnitude < 0.001f) direction = enemy.transform.forward;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        direction.Normalize();
        Rigidbody rootBody = bodies[profile[0].Name];
        // Give the whole connected chain a small common velocity. A torso-only
        // impulse is heavily absorbed by the jointed mass and can read as a freeze.
        Vector3 initialVelocity = direction * (force * 0.3f) + Vector3.up * (force * 0.18f);
        foreach (BoneDefinition definition in profile)
        {
            Rigidbody body = bodies[definition.Name];
            body.linearVelocity = initialVelocity;

            if (definition.Parent == null) continue;
            Vector3 limbOffset = body.worldCenterOfMass - rootBody.worldCenterOfMass;
            Vector3 tumbleAxis = Vector3.Cross(direction, limbOffset);
            if (tumbleAxis.sqrMagnitude < 0.001f) tumbleAxis = body.transform.right;
            body.AddTorque(tumbleAxis.normalized * force * 0.12f, ForceMode.Impulse);
        }
        rootBody.AddForce(direction * force + Vector3.up * (force * 0.28f), ForceMode.Impulse);
        Vector3 rootTumbleAxis = Vector3.Cross(direction, Vector3.up);
        if (rootTumbleAxis.sqrMagnitude > 0.001f)
            rootBody.AddTorque(rootTumbleAxis.normalized * force * 0.18f, ForceMode.Impulse);
        return true;
    }

    private static void AddCollider(Transform bone, BoneDefinition definition, Dictionary<string, Transform> bones)
    {
        float scale = Mathf.Max(0.001f, (Mathf.Abs(bone.lossyScale.x) + Mathf.Abs(bone.lossyScale.y)
            + Mathf.Abs(bone.lossyScale.z)) / 3f);
        if (definition.ColliderEnd == null)
        {
            Transform parent = definition.Parent != null ? bones[definition.Parent] : null;
            float segment = parent != null ? Vector3.Distance(bone.position, parent.position) : 0.2f;
            SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
            sphere.radius = Mathf.Clamp(segment * definition.RadiusScale, 0.035f, 0.14f) / scale;
            sphere.enabled = false;
            return;
        }

        Vector3 localEnd = bone.InverseTransformPoint(bones[definition.ColliderEnd].position);
        float localLength = localEnd.magnitude;
        float radius = Mathf.Clamp(localLength * definition.RadiusScale, 0.035f, 0.14f) / scale;
        CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
        capsule.direction = LargestAxis(localEnd);
        capsule.center = localEnd * 0.5f;
        capsule.radius = radius;
        capsule.height = Mathf.Max(localLength + radius * 1.5f, radius * 2f);
        capsule.enabled = false;
    }

    private static int LargestAxis(Vector3 direction)
    {
        Vector3 absolute = new(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
        if (absolute.x >= absolute.y && absolute.x >= absolute.z) return 0;
        return absolute.y >= absolute.z ? 1 : 2;
    }
}
