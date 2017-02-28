// We use a custom NetworkManager that also takes care of login.
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

public class NewWorkController : NetworkManager {
    // <conn, charName> dict for the lobby
    // (people that are currently in the login handshake)
    Dictionary<NetworkConnection, string> lobby = new Dictionary<NetworkConnection, string>();

    [Header("Credentials")]
    public string id = "";

    // character database
    [Header("Database")] // dont show in inspector to avoid instantiation
    public int charNameMaxLength = 16;
    [SerializeField] float playerSaveInterval = 30f; // in seconds

    void Awake() {
        // handshake packet handlers
        NetworkServer.RegisterHandler(LoginMsg.MsgId, OnServerLogin);

        // headless mode? then automatically start a dedicated server
        // (because we can't click the button in headless mode)
        if (Utils.IsHeadless()) {
            print("headless mode detected, starting dedicated server");
            StartServer();
        }
    }

    // client popup messages ///////////////////////////////////////////////////
    void ClientSendPopup(NetworkConnection conn, string error, bool causesDisconnect) {
        var msg = new ErrorMsg();
        msg.text = error;
        msg.causesDisconnect = causesDisconnect;
        conn.Send(ErrorMsg.MsgId, msg);
    }

    void OnClientReceivePopup(NetworkMessage netMsg) {
        var msg = netMsg.ReadMessage<ErrorMsg>();
        print("OnClientReceivePopup: " + msg.text);

        // show a popup
        FindObjectOfType<UIPopup>().Show(msg.text);

        // disconnect if it was an important network error
        // (this is needed because the login failure message doesn't disconnect
        //  the client immediately (only after timeout))
        if (msg.causesDisconnect) {
            netMsg.conn.Disconnect();

            // also stop the host if running as host
            // (host shouldn't start server but disconnect client for invalid
            //  login, which would be pointless)
            if (NetworkServer.active) StopHost();
        }
    }

    // start & stop ////////////////////////////////////////////////////////////
    public override void OnStartServer() {
        FindObjectOfType<UILogin>().Hide();

        // call base function to guarantee proper functionality
        base.OnStartServer();
    }

    public override void OnStopServer() {

        // call base function to guarantee proper functionality
        base.OnStopServer();
    }

    // handshake: login ////////////////////////////////////////////////////////
    public bool IsConnecting() {
        return NetworkClient.active && !ClientScene.ready;
    }

    public override void OnClientConnect(NetworkConnection conn) {
        print("OnClientConnect");

        // setup handlers
        client.RegisterHandler(ErrorMsg.MsgId, OnClientReceivePopup);
        client.RegisterHandler(LoginOkMsg.MsgId, OnClientLoginOk);

        // send login packet
        var msg = new LoginMsg();
        msg.id = id;
        conn.Send(LoginMsg.MsgId, msg);
        print("login msg was sent");

        // call base function to make sure that client becomes "ready"
        //base.OnClientConnect(conn);
        ClientScene.Ready(conn); // from bitbucket OnClientConnect source
    }

    bool CharNameLoggedIn(string charName) {
        // in lobby or in world?
        return lobby.ContainsValue(charName) ||
            NetworkServer.objects.Any(e => e.Value.GetComponent<Player>() &&
                e.Value.GetComponent<Player>().name == charName);
    }

    bool IsValidCharName(string charName) {
        // not empty?
        // only contains letters, number and underscore and not empty (+)?
        // (important for database safety etc.)
        return !Utils.IsNullOrWhiteSpace(charName) &&
            Regex.IsMatch(charName, @"^[a-zA-Z0-9_]+$");
    }

    void OnServerLogin(NetworkMessage netMsg) {
        print("OnServerLogin " + netMsg.conn);
        var msg = netMsg.ReadMessage<LoginMsg>();

        // not too long?
        if (msg.id.Length <= charNameMaxLength) {
            // validate charname
            if (IsValidCharName(msg.id)) {
                // not in lobby and not in world yet?
                if (!CharNameLoggedIn(msg.id)) {
                    print("login successful: " + msg.id);

                    // add to logged in accounts
                    lobby[netMsg.conn] = msg.id;

                    // send login ok message to client
                    var msgOk = new LoginOkMsg();
                    netMsg.conn.Send(LoginOkMsg.MsgId, msgOk);
                } else {
                    print("charname already logged in: " + msg.id);
                    ClientSendPopup(netMsg.conn, "already logged in", true);

                    // note: we should disconnect the client here, but we can't as
                    // long as unity has no "SendAllAndThenDisconnect" function,
                    // because then the error message would never be sent.
                    //netMsg.conn.Disconnect();
                }
            } else {
                print("invalid charname for: " + msg.id);
                ClientSendPopup(netMsg.conn, "invalid id", true);
            }
        } else {
            print("charname too long: " + msg.id);
            ClientSendPopup(netMsg.conn, "charname too long", true);
        }
    }

    void OnClientLoginOk(NetworkMessage netMsg) {
        print("OnClientLoginOk " + netMsg.conn);
        //var msg = netMsg.ReadMessage<LoginOkMsg>();

        // hide login
        FindObjectOfType<UILogin>().Hide();

        // client is ready
        ClientScene.AddPlayer(client.connection, 0); //, msg);
    }

    // called after the client calls ClientScene.AddPlayer with a msg parameter
    public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId) {
        print("OnServerAddPlayer");

        // only while in lobby (aka after handshake and not ingame)
        if (lobby.ContainsKey(conn)) {
            // read the index and find the n-th character
            // (only if we know that he is not ingame, otherwise lobby has
            //  no netMsg.conn key)
            string charName = lobby[conn];
            print("add " + charName);

            // create and save if not exists yet
            if (!Database.CharacterExists(charName))
                Database.CharacterCreate(charName, playerPrefab, GetStartPosition());

            // load it (we load it after creating to have all the load logic in
            // one place)
            var go = Database.CharacterLoad(charName, playerPrefab);

            // did it work?
            if (go != null) {
                // add to client
                NetworkServer.AddPlayerForConnection(conn, go, playerControllerId);
            } else {
                print("OnSeverAddPlayer failed to load or create the player");
                ClientSendPopup(conn, "AddPlayer: failed to load or create", true);
            }

            // remove from lobby
            lobby.Remove(conn);
        } else {
            print("AddPlayer: not in lobby" + conn);
            ClientSendPopup(conn, "AddPlayer: not in lobby", true);
        }
    }

    // saving //////////////////////////////////////////////////////////////////
    // we have to save all players at once to make sure that item trading is
    // perfectly save. if we would invoke a save function every few minutes on
    // each player seperately then it could happen that two players trade items
    // and only one of them is saved before a server crash - hence causing item
    // duplicates.
    //
    // note: this function is called on clients too, but NetworkServer.objects
    // is empty there, so it doesn't matter.
    void SavePlayers() {
        // save all players
        foreach(var entry in NetworkServer.objects) {
            // is this object a player? (not a monster etc.)
            var player = entry.Value.GetComponent<Player>();
            if (player != null) {
                Database.CharacterSave(player);                
                print("saved:" + player.name);
            }
        }
    }

    // stop/disconnect /////////////////////////////////////////////////////////
    // called on the server when a client disconnects
    public override void OnServerDisconnect(NetworkConnection conn) {
        print("OnServerDisconnect " + conn);

        // save player (if any)
        // note: playerControllers.Count cant be used as check because
        // nothing is removed from that list, even after disconnect. It still
        // contains entries like: ID=0 NetworkIdentity NetID=null Player=null
        // (which might be a UNET bug)
        var go = conn.playerControllers.Find(pc => pc.gameObject != null);
        if (go != null) {
            Database.CharacterSave(go.gameObject.GetComponent<Player>());
            print("saved:" + go.gameObject.name);
        } else print("no player to save for: " + conn);

        // remove logged in account after everything else was done
        lobby.Remove(conn); // just returns false if not found

        // do whatever the base function did (destroy the player etc.)
        base.OnServerDisconnect(conn);
    }

    // called on the client if he disconnects
    public override void OnClientDisconnect(NetworkConnection conn) {
        print("OnClientDisconnect");

        // show a popup so that users now what happened
        FindObjectOfType<UIPopup>().Show("Disconnected.");

        // show login mask again
        FindObjectOfType<UILogin>().Show();

        // call base function to guarantee proper functionality
        base.OnClientDisconnect(conn);

        // call StopClient to clean everything up properly (otherwise
        // NetworkClient.active remains false after next login)
        StopClient();
    }

    // called when quitting the application by closing the window / pressing
    // stop in the editor
    // -> we want to send the quit packet to the server instead of waiting for a
    //    timeout
    // -> this also avoids the OnDisconnectError UNET bug (#838689) more often
    void OnApplicationQuit() {
        if (IsClientConnected()) {
            StopClient();
            print("OnApplicationQuit: stopped client");
        }
    }
}
