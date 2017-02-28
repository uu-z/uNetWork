using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NetworkName))]
public class Player : Entity {
    [Header("Health")]
    [SerializeField] int baseHpMax = 100;
    public override int hpMax {
        get {
            return baseHpMax;
        }
    }

    [Header("Damage")]
    [SyncVar, SerializeField] int baseDamage = 1;
    public override int damage {
        get {
            return baseDamage;
        }
    }

    [Header("Defense")]
    [SyncVar, SerializeField] int baseDefense = 1;
    public override int defense {
        get {
            return baseDefense;
        }
    }

    [Header("Toolbar")]
    public int toolbarSize = 8;
    public KeyCode[] toolbarHotkeys = new KeyCode[] {KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8};
    [SyncVar] public int toolbarSelection = 0;

    protected override void Awake() {
        base.Awake();
    }

    void Start() {
        
    }

    public override void OnStartLocalPlayer() {
        Camera.main.GetComponent<CameraMMO>().target = transform;
     
    }
        
    [Server]
    protected override string UpdateServer() {
        return "IDLE";
    }
        
    [Client]
    protected override void UpdateClient() {
        if (isLocalPlayer) {
            LeftClickHandling();
            RightClickHandling();
        }
    }


    [Client]
    void LeftClickHandling() {

    }
        
    [Client]
    void RightClickHandling() {
        
    }

}