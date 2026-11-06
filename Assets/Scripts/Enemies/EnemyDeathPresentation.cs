using System.Collections.Generic;
using UnityEngine;

public static class EnemyDeathPresentation
{
    private static readonly HumanBodyBones[] RagdollBones =
    {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
        HumanBodyBones.Neck, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot
    };

    public static void Begin(EnemyController enemy)
    {
        Animator animator = enemy.ModelAnimator;
        if (animator != null) animator.enabled = false;

        switch (enemy.DeathEffect)
        {
            case EnemyController.DeathPresentation.BalloonPop:
                Burst(enemy, new Color(0.18f, 0.72f, 0.95f), new Color(0.8f, 0.96f, 1f), "Valcom Balloon Pop");
                break;
            case EnemyController.DeathPresentation.FluidBurst:
                Burst(enemy, new Color(0.2f, 0.72f, 0.95f), new Color(0.78f, 0.92f, 1f), "Doria Fluid Burst");
                break;
            case EnemyController.DeathPresentation.DebrisBurnDissolve:
                CreateDebris(enemy);
                break;
            case EnemyController.DeathPresentation.RagdollBurnDissolve:
                ActivateRagdoll(enemy, animator);
                break;
        }
    }

    private static void Burst(EnemyController enemy, Color primary, Color secondary, string name)
    {
        GameObject effect = ExplosionParticleFactory.CreateColorBurst(name, enemy.transform.position, primary, secondary);
        if (enemy.transform.parent != null) effect.transform.SetParent(enemy.transform.parent, true);
        foreach (Renderer renderer in enemy.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
    }

    private static void CreateDebris(EnemyController enemy)
    {
        Color primary = Color.white;
        if (enemy.BodyRenderer != null && enemy.BodyRenderer.sharedMaterial != null)
            primary = enemy.BodyRenderer.sharedMaterial.color;
        Color secondary = Color.Lerp(primary, Color.white, 0.55f);
        GameObject effect = ExplosionParticleFactory.CreateColorBurst("Enemy Debris", enemy.transform.position, primary, secondary);
        if (enemy.transform.parent != null) effect.transform.SetParent(enemy.transform.parent, true);
    }

    private static void ActivateRagdoll(EnemyController enemy, Animator animator)
    {
        if (animator == null)
        {
            AddSimpleBody(enemy);
            return;
        }

        if (!animator.isHuman || animator.avatar == null)
        {
            ActivateGenericRagdoll(enemy, animator);
            return;
        }

        Dictionary<Transform, Rigidbody> bodies = new();
        foreach (HumanBodyBones bodyBone in RagdollBones)
        {
            Transform bone = animator.GetBoneTransform(bodyBone);
            if (bone == null || bodies.ContainsKey(bone)) continue;
            Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = bodyBone == HumanBodyBones.Hips ? 3f : 0.7f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            SphereCollider collider = bone.gameObject.AddComponent<SphereCollider>();
            collider.radius = RadiusFor(bodyBone) / Mathf.Max(0.01f, MaxAxis(bone.lossyScale));
            bodies.Add(bone, body);
        }

        foreach (KeyValuePair<Transform, Rigidbody> entry in bodies)
        {
            Transform ancestor = entry.Key.parent;
            while (ancestor != null && !bodies.ContainsKey(ancestor)) ancestor = ancestor.parent;
            if (ancestor == null) continue;
            CharacterJoint joint = entry.Key.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = bodies[ancestor];
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = ancestor.InverseTransformPoint(entry.Key.position);
            joint.enableProjection = true;
            joint.projectionDistance = 0.15f;
            joint.projectionAngle = 15f;
            SoftJointLimit low = joint.lowTwistLimit; low.limit = -35f; joint.lowTwistLimit = low;
            SoftJointLimit high = joint.highTwistLimit; high.limit = 35f; joint.highTwistLimit = high;
            SoftJointLimit swing = joint.swing1Limit; swing.limit = 45f; joint.swing1Limit = swing;
            swing = joint.swing2Limit; swing.limit = 45f; joint.swing2Limit = swing;
        }

        if (bodies.Count == 0)
        {
            AddSimpleBody(enemy);
            return;
        }

        Transform hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hipsBone != null && hipsBone.TryGetComponent(out Rigidbody hips))
            hips.AddForce(enemy.transform.forward * 1.2f + Vector3.up * 1.5f, ForceMode.Impulse);
    }

    private static void ActivateGenericRagdoll(EnemyController enemy, Animator animator)
    {
        SkinnedMeshRenderer[] skinned = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        HashSet<Transform> bones = new();
        foreach (SkinnedMeshRenderer renderer in skinned)
            if (renderer.bones != null)
                foreach (Transform bone in renderer.bones)
                    if (bone != null && bone != animator.transform && !ShouldSkipBone(bone.name)) bones.Add(bone);

        if (bones.Count == 0)
        {
            AddSimpleBody(enemy);
            return;
        }

        Dictionary<Transform, Rigidbody> bodies = new();
        foreach (Transform bone in bones)
        {
            Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = IsTorso(bone.name) ? 2.2f : 0.45f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            SphereCollider collider = bone.gameObject.AddComponent<SphereCollider>();
            collider.radius = IsTorso(bone.name) ? 0.14f : 0.075f;
            bodies.Add(bone, body);
        }

        Rigidbody rootBody = null;
        foreach (KeyValuePair<Transform, Rigidbody> entry in bodies)
        {
            Transform ancestor = entry.Key.parent;
            while (ancestor != null && !bodies.ContainsKey(ancestor)) ancestor = ancestor.parent;
            if (ancestor == null)
            {
                rootBody ??= entry.Value;
                continue;
            }

            CharacterJoint joint = entry.Key.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = bodies[ancestor];
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = ancestor.InverseTransformPoint(entry.Key.position);
            joint.enableProjection = true;
            joint.projectionDistance = 0.16f;
            joint.projectionAngle = 18f;
        }

        if (rootBody != null) rootBody.AddForce(enemy.transform.forward * 1.2f + Vector3.up * 1.5f, ForceMode.Impulse);
    }

    private static bool ShouldSkipBone(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("finger") || lower.Contains("thumb") || lower.Contains("toe")
            || lower.Contains("twist") || lower.Contains("weapon") || lower.Contains("prop");
    }

    private static bool IsTorso(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("hip") || lower.Contains("pelvis") || lower.Contains("spine") || lower.Contains("chest");
    }

    private static void AddSimpleBody(EnemyController enemy)
    {
        Transform model = enemy.ModelAnimator != null ? enemy.ModelAnimator.transform : enemy.transform;
        if (model == enemy.transform) return;
        SphereCollider collider = model.gameObject.AddComponent<SphereCollider>();
        collider.center = new Vector3(0f, 0.45f, 0f);
        collider.radius = 0.35f;
        Rigidbody body = model.gameObject.AddComponent<Rigidbody>();
        body.mass = 1f;
        body.AddForce(enemy.transform.forward + Vector3.up, ForceMode.Impulse);
    }

    private static float RadiusFor(HumanBodyBones bone)
    {
        return bone switch
        {
            HumanBodyBones.Head => 0.14f,
            HumanBodyBones.Hips or HumanBodyBones.Chest or HumanBodyBones.UpperChest => 0.2f,
            HumanBodyBones.LeftHand or HumanBodyBones.RightHand or HumanBodyBones.LeftFoot or HumanBodyBones.RightFoot => 0.08f,
            _ => 0.11f
        };
    }

    private static float MaxAxis(Vector3 scale) => Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
}
