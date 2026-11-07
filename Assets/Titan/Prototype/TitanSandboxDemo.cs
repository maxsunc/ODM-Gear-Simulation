using System.Collections.Generic;
using UnityEngine;

/// <summary>Only used in TitanPrototypeSandbox, never on the gameplay player or Titan prefab.</summary>
public class TitanSandboxDemo : MonoBehaviour
{
    public GameObject titanPrefab;
    public ModularTitanBody titan;
    public Camera previewCamera;
    bool walking;
    float orbit = 28f;
    GUIStyle title, small;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (titan == null) ResetTitan();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) ToggleWalk();
        if (Input.GetKeyDown(KeyCode.Alpha7)) PreviewRun();
        if (Input.GetKeyDown(KeyCode.Alpha2)) Slice(TitanHitRegion.LeftArm);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Slice(TitanHitRegion.RightArm);
        if (Input.GetKeyDown(KeyCode.Alpha4)) Slice(TitanHitRegion.LeftLeg);
        if (Input.GetKeyDown(KeyCode.Alpha5)) Slice(TitanHitRegion.RightLeg);
        if (Input.GetKeyDown(KeyCode.Alpha6) && titan != null) titan.StunEyes();
        if (Input.GetKeyDown(KeyCode.K) && titan != null) titan.Die(new Vector3(0, 0, -90));
        if (Input.GetKeyDown(KeyCode.R)) ResetTitan();
        orbit += Input.GetAxisRaw("Horizontal") * Time.deltaTime * 35;
        if (previewCamera != null)
        {
            Vector3 target = new Vector3(0, 5.5f, 0);
            previewCamera.transform.position = target + Quaternion.Euler(0, orbit, 0) * new Vector3(0, 3.2f, 22);
            previewCamera.transform.LookAt(target);
        }
    }

    void ToggleWalk()
    {
        walking = !walking;
        if (titan != null)
        {
            titan.SetWalking(walking);
            var motion = titan.GetComponent<TitanMotionStyle>();
            if (motion != null) motion.previewRun = false;
        }
    }

    void PreviewRun()
    {
        if (titan == null) return;
        walking = true;
        titan.SetWalking(true);
        var motion = titan.GetComponent<TitanMotionStyle>();
        if (motion != null) motion.previewRun = true;
    }

    void Slice(TitanHitRegion region)
    {
        if (titan != null) titan.ApplyDamage(region, 100, titan.transform.position, new Vector3(25, 10, 15));
    }

    public void ResetTitan()
    {
        if (titanPrefab == null) return;
        if (titan != null)
        {
            var detached = new HashSet<GameObject>();
            foreach (var limb in titan.limbs)
                if (limb.severed && limb.rootBone != null) detached.Add(limb.rootBone.root.gameObject);
            foreach (var root in detached) Destroy(root);
            Destroy(titan.gameObject);
        }
        var instance = Instantiate(titanPrefab, Vector3.zero, Quaternion.identity);
        instance.name = "Modular Titan Prototype";
        titan = instance.GetComponent<ModularTitanBody>();
        walking = false;
    }

    void OnGUI()
    {
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        }
        GUILayout.BeginArea(new Rect(18, 18, 305, 570), GUI.skin.box);
        GUILayout.Label("TITAN / BODY LAB", title);
        GUILayout.Label("12m modular blockout - Phase 1", small);
        GUILayout.Space(8);
        if (titan != null)
        {
            GUILayout.Label(titan.IsDead ? "DEAD / PHYSICS RAGDOLL" : titan.IsBlind ? "BLIND / " + titan.BlindTimeRemaining.ToString("0.0") + "s" : "ALIVE / ANIMATED");
            GUILayout.Label("Left grab: " + titan.CanGrabLeft + "   Right grab: " + titan.CanGrabRight, small);
            GUILayout.Label("Walking available: " + titan.CanWalk, small);
            var injury = titan.GetComponent<TitanInjuryPose>();
            if (injury != null) GUILayout.Label("Injury: " + injury.State, small);
        }
        GUILayout.Space(8);
        GUI.enabled = titan != null && !titan.IsDead;
        if (GUILayout.Button("1  Idle / walk animation", GUILayout.Height(29))) ToggleWalk();
        if (GUILayout.Button("7  Chase run preview", GUILayout.Height(29))) PreviewRun();
        if (GUILayout.Button("2  Slice left arm", GUILayout.Height(29))) Slice(TitanHitRegion.LeftArm);
        if (GUILayout.Button("3  Slice right arm", GUILayout.Height(29))) Slice(TitanHitRegion.RightArm);
        if (GUILayout.Button("4  Slice left leg", GUILayout.Height(29))) Slice(TitanHitRegion.LeftLeg);
        if (GUILayout.Button("5  Slice right leg", GUILayout.Height(29))) Slice(TitanHitRegion.RightLeg);
        if (GUILayout.Button("6  Eye slash / temporary stun", GUILayout.Height(29))) titan.StunEyes();
        if (GUILayout.Button("K  Nape cut / ragdoll", GUILayout.Height(29))) titan.ApplyDamage(TitanHitRegion.Nape, 1, Vector3.zero, new Vector3(0, 0, -90));
        GUI.enabled = true;
        if (GUILayout.Button("R  Fresh Titan", GUILayout.Height(29))) ResetTitan();
        GUILayout.Space(8);
        GUILayout.Label("A/D or arrow keys: orbit camera\nStanding figure = 1.8m human\nOne leg lost: kneel. Both legs lost: prone.\nSight/chase: open TitanAISandbox. Attacks are not implemented yet.", small);
        GUILayout.EndArea();
    }
}
