# Rake (2015) - Decompiled Architecture Notes

## Source: Assembly-CSharp.dll (Unity 5.0.0f4, Mono 3.0)

---

## 1. Player Architecture (FPSController)

### Hierarchy (from code, NOT transform.Find guesses)

```
FPSController (Root GameObject)
├── Camera (cam) - Main world camera
├── Camera (weapCam) - Weapon view model camera (renders hands/gun)
├── PlayerMovement (script)
├── CameraMouseLook (script) - attached to camera transform
├── WeaponManager (script)
├── Inventory (script)
├── FPSCamera (script) - interaction raycast
├── AudioListener
├── Light (flashlight)
├── Animator (cameraAnimator)
├── Animator (effectAnimator)
├── Animator (itemInfo)
└── Canvas (playerCanvas)
```

### Key Fields (FPSController)
```csharp
public Camera cam;           // Main camera (world)
public Camera weapCam;       // Weapon camera (view model - hands/gun)
public WeaponManager weaponManager;
public PlayerMovement playerMovement;
public CameraMouseLook camMouseLook;
public Inventory inventory;
public Animator cameraAnimator, effectAnimator, itemInfo;
public Light flashlight;
public AudioListener listener;
public GameObject playerCanvas;
```

### Critical: How View Model (Hands/Gun) Works

**There is NO "ThirdPersonObject" or "FirstPersonObject" in the hierarchy.**

The view model (hands + weapon) is rendered by **`weapCam`** (a separate Camera with higher depth).
The weapons are **Transform objects in `WeaponManager.playerWeapons` list** (e.g., `Rifle_weapon`, `SniperRifle_weapon`).

**To HIDE hands/weapon on remote players:**
```csharp
// Option 1: Disable the weapon camera
fpsController.weapCam.enabled = false;

// Option 2: Disable all weapon view models
foreach (var weapon in weaponManager.playerWeapons)
    weapon.gameObject.SetActive(false);

// Option 3: Use CameraRander(false) - disables BOTH cameras
fpsController.CameraRander(false);  // cam.enabled = false; weapCam.enabled = false;
```

**Original code (FPSController.CameraRander):**
```csharp
public void CameraRander(bool type)
{
    cam.enabled = type;
    weapCam.enabled = type;  // <--- THIS controls view model visibility
}
```

**Weapon switching (WeaponManager.changeWeapon):**
```csharp
((Component)weaponSelect).gameObject.SetActive(false);  // Hide old weapon
((Component)playerWeapons[i]).gameObject.SetActive(true);  // Show new weapon
```

---

## 2. Local Player Detection (Original Game)

**The original game has NO multiplayer - it's single-player only.**

There is **no `isMine`, `isLocalPlayer`, `NetworkView`, or `PhotonView`** in the original code.

The "local player" concept is simply:
```csharp
FPSController.instance  // Static singleton - THE player
```

All input, movement, camera control operates on this singleton.

**Our mod introduces `NetworkedPlayer.isLocalPlayer`** - this is OUR addition for multiplayer.

### How Our Mod Sets isLocalPlayer (Correctly)

**GameEntryPatch.Postfix** runs on BOTH host and client when FPSController.Start fires:
```csharp
var netPlayer = __instance.GetComponent<NetworkedPlayer>();
if (netPlayer != null) 
{
    netPlayer.SetAsLocalPlayer();  // Sets isLocalPlayer = true
}

// NetworkedPlayer.SetAsLocalPlayer()
public void SetAsLocalPlayer()
{
    isLocalPlayer = true;
    enabled = true;
    if (RakeMultiplayerPlugin.isServer) PlayerId = 1;  // Host = 1
    // Client gets ID later via PlayerConnectedPacket(IsYou=true)
}
```

**Weapon patches correctly check this:**
```csharp
// RifleWeaponUpdatePatch.Prefix / SniperRifleWeaponUpdatePatch.Prefix
var netPlayer = __instance.GetComponentInParent<NetworkedPlayer>();
if (netPlayer != null && !netPlayer.isLocalPlayer)
{
    return false;  // BLOCK only remote players
}
return true;  // ALLOW local player (isLocalPlayer == true)
```

**Conclusion**: Client's own player correctly gets `isLocalPlayer = true` in `GameEntryPatch.Postfix` BEFORE any input occurs. The client CAN move/look/shoot.

---

## 3. PlayerMovement.Update Logic

```csharp
private void Update()
{
    if (bCanMove && FPSController.instance.playerState == FPSController.Player_state.Full)
    {
        // Stamina management
        if (bRun) { playerStamina -= 0.1f; if (playerStamina <= 1f) bCanRun = false; }
        else if (playerStamina < 100f) { playerStamina += 0.3f; }
        
        // Movement input
        CharacterController component = GetComponent<CharacterController>();
        if (component.isGrounded)
        {
            if (Input.GetButton("Vertical") || Input.GetButton("Horizontal"))
            {
                anim.SetFloat("Speed", 1f);
                cameraAnim.SetFloat("Speed", 1f);
            }
            else
            {
                anim.SetFloat("Speed", 0f);
                cameraAnim.SetFloat("Speed", 0f);
            }
            
            if (Input.GetButton("Run") && bCanRun) { speed = 8f; bRun = true; }
            else { speed = 4.5f; bRun = false; }
            
            moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
            moveDirection = transform.TransformDirection(moveDirection);
            moveDirection *= speed;
        }
        moveDirection.y -= gravity * Time.deltaTime;
        component.Move(moveDirection * Time.deltaTime);
    }
    else
    {
        anim.SetFloat("Speed", 0f);
        cameraAnim.SetFloat("Speed", 0f);
    }
}
```

**Key Animator Parameters:**
- `Speed` (Float) - 0=idle, 1=walk, 2=run
- `Run` (Bool) - set by weapon animator, not here

---

## 4. Item Pickup System

### Base Class (Item.cs)
```csharp
public class Item : MonoBehaviour
{
    public string name;
    public int count;
    public int id;
    public AudioClip pickupSound;
    public virtual void PickupItem() { }  // Empty base
}
```

### Derived Example (Medkit_item.cs)
```csharp
public class Medkit_item : Item
{
    public override void PickupItem()
    {
        if (WeaponManager.instance.CheckWeapon(8))
        {
            if (WeaponManager.instance.bFullAmmo(8))
            {
                InformationUI.instance.DrawHint("Full count of med kits");
                return;
            }
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            WeaponManager.instance.AddAmmo(8, 1);
            InformationUI.instance.PickupInfo("+ Add medkit 1");
            Object.Destroy(gameObject);
        }
        else
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            WeaponManager.instance.AddWeapon(8, 1, type: true);
            InformationUI.instance.PickupInfo("+ Add medkit 1");
            Object.Destroy(gameObject);
        }
    }
}
```

**All pickups:**
1. Play sound at position
2. Add weapon/ammo to WeaponManager
3. Show UI info
4. **Destroy own GameObject** (`Object.Destroy(gameObject)`)

---

## 5. Weapon View Model Details (Rifle_weapon)

```csharp
public class Rifle_weapon : Weapon
{
    public Animator animator;
    public Animator UI;
    public GameObject UI_System;
    public Transform caster;          // Raycast origin
    public AudioSource SFX;
    public AudioClip fireSound;
    public GameObject muzzlePos;      // Muzzle flash spawn point
    public GameObject muzzleFire;     // Muzzle flash prefab
    
    private void OnEnable()
    {
        UI_System.SetActive(true);
        UI.SetInteger("Bullets", currentAmmo);
    }
    
    private void OnDisable()
    {
        UI_System.SetActive(false);
    }
    
    private void Update()
    {
        if (Input.GetButtonDown("Fire1") && bSight && bCanFire && currentAmmo > 0)
        {
            StartCoroutine(Fire());
            // Raycast + Event_PlayerFire notification
        }
    }
    
    private IEnumerator Fire()
    {
        bCanFire = false;
        Object.Instantiate(muzzleFire, muzzlePos.transform.position, muzzlePos.transform.rotation);
        animator.SetBool("Fire", true);
        SFX.PlayOneShot(fireSound);
        yield return new WaitForSeconds(0.2f);
        animator.SetBool("Fire", false);
        yield return new WaitForSeconds(1f);
        bCanFire = true;
    }
}
```

**Key private fields for muzzle flash sync:**
- `muzzlePos` (Transform/GameObject) - spawn position
- `muzzleFire` (GameObject) - flash prefab
- `SFX` (AudioSource) - sound source
- `fireSound` (AudioClip) - fire sound

---

## 6. Emergency Fixes Applied to Mod

### Fix 1: Debug Capsule in SpawnRemotePlayer
```csharp
// In PlayerSpawner.SpawnRemotePlayer
var debugCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
debugCapsule.name = "DebugCapsule";
debugCapsule.transform.SetParent(playerObj.transform, false);
debugCapsule.transform.localPosition = Vector3.up;  // Eye level
UnityEngine.Object.Destroy(debugCapsule.GetComponent<Collider>());
RakeMultiplayerPlugin.Log?.LogInfo("[3.5] Spawned DEBUG CAPSULE for remote player!");
```

### Fix 2: Proper View Model Disabling in SetAsRemotePlayer
```csharp
// In NetworkedPlayer.SetAsRemotePlayer

// 1. Disable ALL cameras (including weapCam)
var cameras = GetComponentsInChildren<Camera>(true);
foreach (var cam in cameras) cam.enabled = false;

// 2. Disable weapCam specifically via reflection
var weapCamField = typeof(FPSController).GetField("weapCam");
var weapCam = weapCamField.GetValue(fps) as Camera;
if (weapCam != null) weapCam.enabled = false;

// 3. Disable all weapon view models in WeaponManager
var playerWeaponsField = typeof(WeaponManager).GetField("playerWeapons");
var playerWeapons = playerWeaponsField.GetValue(wm) as List<Transform>;
foreach (var weapon in playerWeapons)
    weapon.gameObject.SetActive(false);

// 4. Keep Animator enabled for animation sync
var anim = GetComponentInChildren<Animator>();
if (anim) anim.enabled = true;
```

---

## 7. File Inventory

**Decompiled Source Location:** `D:\Rake Multiplayer\Decompiled_Source\`

**Total Files:** ~187 .cs files

**Key Files:**
- `FPSController.cs` (8,041 bytes) - Main player controller
- `PlayerMovement.cs` (3,816 bytes) - Movement & stamina
- `CameraMouseLook.cs` (4,801 bytes) - Camera rotation
- `WeaponManager.cs` (4,912 bytes) - Weapon switching, view models
- `Item.cs` (205 bytes) - Base item class
- `Medkit_item.cs` (998 bytes) - Medkit pickup
- `Rifle_weapon.cs` (5,304 bytes) - Rifle view model
- `SniperRifle_weapon.cs` (6,057 bytes) - Sniper view model
- `Enemy_AI.cs` (29,080 bytes) - Rake AI
- `Inventory.cs` (3,450 bytes) - Inventory UI
- `FPSCamera.cs` (1,844 bytes) - Interaction raycast

---

## 8. Summary for Multiplayer Implementation

| Feature | Original Implementation | Multiplayer Approach |
|---------|------------------------|---------------------|
| **View Model** | `weapCam` + `WeaponManager.playerWeapons` | Disable `weapCam` + all `playerWeapons` on remote |
| **Body Model** | NONE (pure FPS) | Debug capsule fallback; future: import 3P model |
| **Movement** | `PlayerMovement.Update` + `CharacterController` | Local: normal; Remote: interpolated transform |
| **Camera Look** | `CameraMouseLook.Update` (if `bControl`) | Local: normal; Remote: interpolated rotation |
| **Pickup** | `Item.PickupItem()` → `WeaponManager.AddWeapon` + `Destroy` | Server-authoritative: client requests, server confirms |
| **Damage** | `FPSController.TakeDamage()` / `Enemy_AI.TakeDamage()` | Server-authoritative Rake; client requests via DamageRequest |
| **Local Player** | `FPSController.instance` singleton | `NetworkedPlayer.isLocalPlayer` flag (set in GameEntryPatch) |

---

*Generated from ILSpy decompilation of Assembly-CSharp.dll*
*Date: 2026-11-06*