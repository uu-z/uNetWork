// The Entity class is the base class for everything living like Players,
// Monsters, NPCs.
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections.Generic;

// note: no animator required, towers, dummies etc. may not have one
[RequireComponent(typeof(Rigidbody))] // kinematic, only needed for OnTrigger
[RequireComponent(typeof(NetworkProximityCheckerCustom))]
public abstract class Entity : NetworkBehaviour {
    // finite state machine
    // -> state only writable by entity class to avoid all kinds of confusion
    [Header("State")]
    [SyncVar, SerializeField] string _state = "IDLE";
    public string state { get { return _state; } }

    [Header("Health")]
    [SyncVar, SerializeField] protected bool invincible = false; // GMs, Npcs, ...
    [SyncVar, SerializeField] protected bool hpRecovery = true; // can be disabled in combat etc.
    [SyncVar, SerializeField] protected int hpRecoveryRate = 1; // per second
    [SyncVar                ] int _hp = 1;
    public int hp {
        get { return Mathf.Min(_hp, hpMax); } // min in case hp>hpmax after buff ends etc.
        set { _hp = Mathf.Clamp(value, 0, hpMax); }
    }
    public abstract int hpMax{ get; }

    [Header("Damage Popup")]
    [SerializeField] GameObject damagePopupPrefab;

    // other properties
    public abstract int damage { get; }
    public abstract int defense { get; }

    // cache
    [HideInInspector] public NetworkProximityChecker proxchecker;
    [HideInInspector] public NetworkIdentity netIdentity;
    [HideInInspector] public Animator animator;
    [HideInInspector] new public Collider collider;

    // networkbehaviour ////////////////////////////////////////////////////////
    // cache components on server and clients
    protected virtual void Awake() {
        proxchecker = GetComponent<NetworkProximityChecker>();
        netIdentity = GetComponent<NetworkIdentity>();
        animator = GetComponent<Animator>();
        // the collider can also be a child in case of animated entities (where
        // it sits on the pelvis for example). equipment colliders etc. aren't
        // a problem because they are added after awake in case
        collider = GetComponentInChildren<Collider>();
    }

    public override void OnStartServer() {
        // health recovery every second
        InvokeRepeating("Recover", 1, 1);

        // HpDecreaseBy changes to "DEAD" state when hp drops to 0, but there is
        // a case where someone might instantiated a Entity with hp set to 0,
        // hence we have to check that case once at start
        if (hp == 0) _state = "DEAD";
    }

    // entity logic will be implemented with a finite state machine
    // -> we should react to every state and to every event for correctness
    // -> we keep it functional for simplicity
    // note: can still use LateUpdate for Updates that should happen in any case
    void Update() {
        // monsters, npcs etc. don't have to be updated if no player is around
        // checking observers is enough, because lonely players have at least
        // themselves as observers, so players will always be updated
        // and dead monsters will respawn immediately in the first update call
        // even if we didn't update them in a long time (because of the 'end'
        // times)
        // -> update only if:
        //    - observers are null (they are null in clients)
        //    - if they are not null, then only if at least one (on server)
        //    - the entity is hidden, otherwise it would never be updated again
        //      because it would never get new observers
        if (netIdentity.observers == null || netIdentity.observers.Count > 0 || IsHidden()) {
            if (isClient) UpdateClient();
            if (isServer) _state = UpdateServer();
        }
    }

    // update for server. should return the new state.
    protected abstract string UpdateServer();

    // update for client.
    protected abstract void UpdateClient();

    // visibility //////////////////////////////////////////////////////////////
    // hide a entity
    // note: using SetActive won't work because its not synced and it would
    //       cause inactive objects to not receive any info anymore
    // note: this won't be visible on the server as it always sees everything.
    [Server]
    public void Hide() {
        proxchecker.forceHidden = true;
    }

    [Server]
    public void Show() {
        proxchecker.forceHidden = false;
    }

    // is the entity currently hidden?
    // note: usually the server is the only one who uses forceHidden, the
    //       client usually doesn't know about it and simply doesn't see the
    //       GameObject.
    public bool IsHidden() {
        return proxchecker.forceHidden;
    }

    public float VisRange() {
        return proxchecker.visRange;
    }

    // health & mana ///////////////////////////////////////////////////////////
    public float HpPercent() {
        return (hp != 0 && hpMax != 0) ? (float)hp / (float)hpMax : 0.0f;
    }

    [Server]
    public void Revive(float healthPercentage = 1) {
        hp = Mathf.RoundToInt(hpMax * healthPercentage);
    }

    // combat //////////////////////////////////////////////////////////////////
    // no need to instantiate damage popups on the server
    [ClientRpc]
    void RpcShowDamagePopup(int amount, Vector3 pos) {
        // spawn the damage popup (if any) and set the text
        if (damagePopupPrefab) {
            var popup = (GameObject)Instantiate(damagePopupPrefab, pos, Quaternion.identity);
            popup.GetComponentInChildren<TextMesh>().text = amount.ToString();
        }
    }

    // deal damage at another entity
    // (can be overwritten for players etc. that need custom functionality)
    // (can also return the set of entities that were hit, just in case they are
    //  needed when overwriting it etc.)
    [Server]
    public virtual HashSet<Entity> DealDamageAt(Entity entity, int n, float aoeRadius=0f) {
        // build the set of entities that were hit within AoE range
        var entities = new HashSet<Entity>();

        // add main target in any case, because non-AoE skills have radius=0
        entities.Add(entity);

        // add all targets in AoE radius around main target
        var colliders = Physics.OverlapSphere(entity.transform.position, aoeRadius); //, layerMask);
        foreach (var c in colliders) {
            var candidate = c.GetComponentInParent<Entity>();
            // overlapsphere cast uses the collider's bounding volume (see
            // Unity scripting reference), hence is often not exact enough
            // in our case (especially for radius 0.0). let's also check the
            // distance to be sure.
            if (candidate != null && candidate != this && candidate.hp > 0 &&
                Vector3.Distance(entity.transform.position, candidate.transform.position) < aoeRadius)
                entities.Add(candidate);
        }

        // now deal damage at each of them
        foreach (var e in entities) {
            // subtract defense (but leave at least 1 damage, otherwise it may be
            // frustrating for weaker players)
            // [dont deal any damage if invincible]
            int dmg = !e.invincible ? Mathf.Max(n-e.defense, 1) : 0;
            e.hp -= dmg;

            // show damage popup in observers via ClientRpc
            // showing them above their head looks best, and we don't have to
            // use a custom shader to draw world space UI in front of the entity
            // note: we send the RPC to ourselves because whatever we killed
            //       might disappear before the rpc reaches it
            var bounds = e.GetComponentInChildren<Collider>().bounds;
            RpcShowDamagePopup(dmg, new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
        }

        return entities;
    }

    // recovery ////////////////////////////////////////////////////////////////
    // receover health and mana once a second
    // (can be overwritten for players etc. that need custom functionality)
    // note: when stopping the server with the networkmanager gui, it will
    //       generate warnings that Recover was called on client because some
    //       entites will only be disabled but not destroyed. let's not worry
    //       about that for now.
    [Server]
    public virtual void Recover() {
        if (enabled && hp > 0)
        if (hpRecovery) hp += hpRecoveryRate;
    }
}
